using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Logic used for localization (multi lingual systems)
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Retrieves the locale for the site (page) currently open in the browser
    /// </summary>
    /// <param name="xmlConfiguration"></param>
    /// <returns></returns>
    public static void RetrieveSiteLocale(RequestVariables? requestVariables, XmlDocument xmlConfiguration)
    {
        // Fallback
        if (requestVariables == null)
        {
            appLogger.LogInformation("Could not dynamically retrieve site locale in RetrieveSiteLocale(), because requestVariables was null");
        }
        else
        {
            //check localization mechanism
            var mechanism = RetrieveNodeValueIfExists("//settings/setting[@id='localization_mechanism']", xmlConfiguration) ?? "domain";

            switch (mechanism)
            {
                case "domain":
                    //domain
                    foreach (XmlNode xmlNode in xmlConfiguration.SelectNodes("/configuration/general/domains/domain[@locale-selector='domain' and ./text()='" + requestVariables.domainName + "']"))
                    {
                        requestVariables.siteLocale = xmlNode.Attributes["locale"].Value;
                        requestVariables.siteLanguage = RetrieveAttributeValueIfExists("/configuration/locales/locale[@id='" + requestVariables.siteLocale + "']/language/@code", xmlConfiguration);
                        requestVariables.siteCountry = RetrieveAttributeValueIfExists("/configuration/locales/locale[@id='" + requestVariables.siteLocale + "']/country/@code", xmlConfiguration);
                    }

                    //JT: shouldn't this be part of a specific "case 'querystring'"?
                    foreach (XmlNode xmlNode in xmlConfiguration.SelectNodes("/configuration/general/domains/domain[@locale-selector='querystring' and ./text()='" + requestVariables.domainName + "']"))
                    {
                        String pattern = xmlNode.Attributes["locale-match"].Value;
                        String queryString = requestVariables.querystringVariables.ToString();

                        // TODO: Check if this actually makes sense - does queryString now contain the complete querystring as it was used in the request??
                        if (RegExpTest(pattern, queryString))
                        {
                            requestVariables.siteLocale = xmlNode.Attributes["locale"].Value;
                            requestVariables.siteLanguage = RetrieveAttributeValueIfExists("/configuration/locales/locale[@id='" + requestVariables.siteLocale + "']/language/@code", xmlConfiguration);
                            requestVariables.siteCountry = RetrieveAttributeValueIfExists("/configuration/locales/locale[@id='" + requestVariables.siteLocale + "']/country/@code", xmlConfiguration);
                        }
                    }
                    break;

                case "cookie":
                    // TODO: Fix
                    //cookie
                    // HttpCookie objCookieLocalization = Request.Cookies["localization"];
                    // //if cookie exists get the values, otherwise set the defaults
                    // if (objCookieLocalization != null)
                    // {
                    //  siteLocale = objCookieLocalization.Value;
                    //  siteLanguage = RetrieveAttributeValueIfExists("/configuration/locales/locale[@id=" + GenerateEscapedXPathString(siteLocale) + "]/language/@code", xmlConfiguration);
                    //  siteCountry = RetrieveAttributeValueIfExists("/configuration/locales/locale[@id=" + GenerateEscapedXPathString(siteLocale) + "]/country/@code", xmlConfiguration);
                    // }
                    // else
                    // {
                    //  Response.Cookies["localization"].HttpOnly = false;
                    //  Response.Cookies["localization"].Value = siteLocale;
                    // }
                    break;
                default:
                    break;
            }
        }


    }

    /// <summary>
    /// Get localized value by key. It will return the localized value based on the passed language.
    /// </summary>
    /// <param name="textFragmentId"></param>
    /// <param name="languageCode"></param>
    /// <returns></returns>
    public static string RetrieveLocalizedValueByKey(String textFragmentId, string languageCode = "en")
    {
        XmlNode? nodeTextFragment = xmlApplicationConfiguration.SelectSingleNode("/configuration/localizations/translations/textfragment[@id='" + textFragmentId + "']");
        if (nodeTextFragment != null)
        {
            var customizedTextFragment = RetrieveCustomizedLocalizedValueByKey(textFragmentId, languageCode);
            if (string.IsNullOrEmpty(customizedTextFragment))
            {
                //retrieve the translated value and convert it into a char array
                var textFragment = RetrieveNodeValueIfExists("value[@lang='" + languageCode + "']", nodeTextFragment, true);
                if (!string.IsNullOrEmpty(textFragment))
                {
                    return textFragment;
                }
            }
            else
            {
                return customizedTextFragment;
            }
        }

        // If textFragmentId is not found in config the id will be used as text
        return "[" + textFragmentId + "]";
    }


    /// <summary>
    /// Writes the localized value by key to the output stream
    /// </summary>
    /// <param name="textFragmentId"></param>
    public static void RenderLocalizedValueByKey(HttpResponse Response, String textFragmentId, string siteLanguage = "en")
    {
        Response.WriteAsync(RetrieveLocalizedValueByKey(textFragmentId, siteLanguage));
    }

    /// <summary>
    /// Replaces all localized strings in a piece of text with the language passed
    /// </summary>
    /// <param name="source"></param>
    /// <param name="langId"></param>
    /// <returns></returns>
    public static string ReplaceLocalizedValuesInHtml(string source, string langId = "en")
    {
        string textFragmentId = "";
        string? textFragment = "";
        string placeHolder = "";
        List<char> placeholderList = new List<char>();
        List<char> result = new List<char>();
        bool inside = false;
        bool findReplacement = false;
        bool startCapture = false;
        bool useTextFragment = false;
        XmlNode? nodeTextFragment = null;
        char[] charArray;
        char[] placeHolderCharArray;
        char currentChar;

        //walk through the characters in the string
        for (int i = 0; i < source.Length; i++)
        {
            currentChar = source[i];
            if (currentChar == '[')
            {
                inside = true;
                startCapture = true;
            }

            if (startCapture && inside)
            {
                placeholderList.Add(currentChar);
            }

            if (currentChar == ']')
            {
                inside = false;
                startCapture = false;
                findReplacement = true;
            }
            if (!inside)
            {
                if (findReplacement)
                {
                    placeHolderCharArray = placeholderList.ToArray();
                    placeHolder = new string(placeHolderCharArray);

                    //capture the textfragment id
                    //List<char> textFragmentIdList = placeholderListNew.GetRange(1, placeholderListNew.Count - 2);
                    //textFragmentId = placeHolder.Substring(1, placeHolder.Length-2);
                    textFragmentId = placeHolder.Replace("[", "").Replace("]", "");


                    if (RegExpTest(@"^[a-z0-9_\-]+$", textFragmentId))
                    {
                        nodeTextFragment = xmlApplicationConfiguration.SelectSingleNode("/configuration/localizations/translations/textfragment[@id='" + textFragmentId + "']");
                        if (nodeTextFragment != null)
                        {
                            var customizedTextFragment = RetrieveCustomizedLocalizedValueByKey(textFragmentId, langId);
                            if (string.IsNullOrEmpty(customizedTextFragment))
                            {

                                //retrieve the translated value and convert it into a char array
                                textFragment = RetrieveNodeValueIfExists("value[@lang='" + langId + "']", nodeTextFragment, true);
                                if (!string.IsNullOrEmpty(textFragment))
                                {
                                    charArray = textFragment.Trim().Replace("/n", "").ToCharArray();

                                    for (int j = 0; j < charArray.Length; j++)
                                    {
                                        result.Add(charArray[j]);
                                    }
                                    useTextFragment = true;
                                }
                            }
                            else
                            {
                                charArray = customizedTextFragment.Trim().Replace("/n", "").ToCharArray();

                                for (int j = 0; j < charArray.Length; j++)
                                {
                                    result.Add(charArray[j]);
                                }
                                useTextFragment = true;
                            }
                        }

                        //Response.Write("- textFragmentId= -" + textFragmentId + "-<br/>");

                        if (!useTextFragment)
                        {
                            for (int j = 0; j < placeHolderCharArray.Length; j++)
                            {
                                result.Add(placeHolderCharArray[j]);
                            }
                        }

                    }

                    //reset
                    findReplacement = false;
                    useTextFragment = false;
                    placeholderList.Clear();

                }
                else
                {
                    result.Add(currentChar);
                }

            }
        }
        return new string(result.ToArray());
    }

    /// <summary>
    /// Attempts to retrieve a customized version of a translation fragment
    /// </summary>
    /// <param name="textFragmentId"></param>
    /// <param name="languageCode"></param>
    /// <returns></returns>
    private static string? RetrieveCustomizedLocalizedValueByKey(String textFragmentId, String languageCode)
    {
        XmlNode? nodeTextFragment = xmlApplicationConfiguration.SelectSingleNode("/configuration/customizations/translations/textfragment[@id='" + textFragmentId + "']");
        if (nodeTextFragment != null)
        {
            //retrieve the translated value and convert it into a char array
            var textFragment = RetrieveNodeValueIfExists("value[@lang='" + languageCode + "']", nodeTextFragment, true);
            if (!string.IsNullOrEmpty(textFragment))
            {
                return textFragment;
            }
        }

        // If textFragmentId is not found in config return null
        return null;

    }

}