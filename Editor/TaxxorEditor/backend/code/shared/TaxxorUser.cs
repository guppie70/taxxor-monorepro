using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        public class AppUserTaxxor : Framework.AppUser
        {

            public XmlDocument XmlUserPreferences = new XmlDocument();

            /// <summary>
            /// Default permissions of the user in Taxxor Disclosure Manager
            /// </summary>
            /// <value></value>
            public TaxxorPermissions Permissions { get; set; }

            /// <summary>
            /// Constructor
            /// </summary>
            public AppUserTaxxor()
            {
                Permissions = new TaxxorPermissions();
            }

            /// <summary>
            /// Retrieves a user preference value
            /// </summary>
            /// <param name="key"></param>
            /// <param name="defaultValue"></param>
            /// <returns></returns>
            public string? RetrieveUserPreferenceKey(string key, string? defaultValue = null)
            {
                string? value = defaultValue;

                var nodeSetting = XmlUserPreferences.SelectSingleNode("/settings/setting[@id=" + GenerateEscapedXPathString(key) + "]");
                if (nodeSetting != null)
                {
                    value = nodeSetting.SelectSingleNode("value").InnerText;
                }

                return value;
            }

            /// <summary>
            /// Updates or creates a user preference setting
            /// </summary>
            /// <param name="key"></param>
            /// <param name="value"></param>
            /// <returns></returns>
            public async Task<bool> UpdateUserPreferenceKey(string key, string value)
            {
                //TODO: test the key value against a list of allowed key values (to avoid it being hacked)

                //test if the key exists
                var nodeSetting = XmlUserPreferences.SelectSingleNode("/settings/setting[@id=" + GenerateEscapedXPathString(key) + "]");
                if (nodeSetting == null)
                {
                    //create the new setting node in the user preferences XML file
                    try
                    {
                        XmlElement nodeNewSetting = XmlUserPreferences.CreateElement("setting");
                        nodeNewSetting.SetAttribute("id", key);
                        XmlElement nodeNewValue = XmlUserPreferences.CreateElement("value");
                        nodeNewValue.InnerText = value;
                        nodeNewSetting.AppendChild(nodeNewValue);
                        XmlUserPreferences.DocumentElement.AppendChild(nodeNewSetting);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Problem in UpdateUserPreferenceKey('{key}', '{value}')");
                        return false;
                    }
                }
                else
                {
                    //update the setting
                    nodeSetting.SelectSingleNode("value").InnerText = value;
                }

                return await _storeUserPreferencesXmlData();
            }

            /// <summary>
            /// Removes a user setting from the preferences
            /// </summary>
            /// <param name="key"></param>
            /// <returns></returns>
            public async Task<bool> DeleteUserPreferenceKey(string key)
            {
                try
                {
                    var nodeSetting = XmlUserPreferences.SelectSingleNode("/settings/setting[@id=" + GenerateEscapedXPathString(key) + "]");
                    if (nodeSetting != null)
                    {
                        Framework.RemoveXmlNode(nodeSetting);
                    }
                }
                catch (Exception ex)
                {
                    WriteErrorMessageToConsole($"Could not delete user preference key: '{key}'", ex.ToString());
                    return false;
                }

                return await _storeUserPreferencesXmlData();
            }

            /// <summary>
            /// Stores the user preferences file on the Taxxor Document Store
            /// </summary>
            /// <returns>The user preferences xml data.</returns>
            private async Task<bool> _storeUserPreferencesXmlData()
            {
                // Get the HTTP context
                var context = System.Web.Context.Current;

                // Save the information for this reporting requirement on the Taxxor Document Store                              
                var dataToPost = new Dictionary<string, string>();
                dataToPost.Add("type", "userpreferences");
                dataToPost.Add("value", XmlUserPreferences.OuterXml);

                // Store the user preferences XML file on the remote Taxxor service and return a boolean indicating if we succeeded or not
                bool userPreferenceStoreResult = await CallTaxxorDataService<bool>(RequestMethodEnum.Put, "taxxoreditoruserdata", dataToPost, true);

                if (userPreferenceStoreResult)
                {
                    // Update the session
                    var sessionKey = "user_xmldata";
                    if (!string.IsNullOrEmpty(context.Session.GetString(sessionKey)))
                    {
                        try
                        {
                            var xmlAllUserData = new XmlDocument();
                            xmlAllUserData.LoadXml(context.Session.GetString(sessionKey));


                            var newUserPreferenceNode = XmlUserPreferences.DocumentElement;
                            var sessionUserPreferenceNode = xmlAllUserData.SelectSingleNode("/userdata/preferences/*");

                            ReplaceXmlNode(sessionUserPreferenceNode, newUserPreferenceNode);

                            context.Session.SetString(sessionKey, xmlAllUserData.OuterXml);
                        }
                        catch (Exception ex)
                        {
                            WriteErrorMessageToConsole("Could not update user preferences in session", ex.ToString());
                        }
                    }
                }

                // Return the result
                return userPreferenceStoreResult;
            }
        }

    }
}