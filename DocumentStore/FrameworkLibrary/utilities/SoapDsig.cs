using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using FrameworkLibrary.models;
using static FrameworkLibrary.FrameworkEncryption;

namespace FrameworkLibrary.utilities
{
    public class XmlDsig
    {
        private readonly XmlDocument xdXmlApplicationConfiguration;
        private readonly string? strWebserviceId;

        //constructor
        public XmlDsig(XmlDocument xdXmlApplicationConfiguration)
        {
            this.xdXmlApplicationConfiguration = xdXmlApplicationConfiguration;
            strWebserviceId = null;
        }

        public XmlDsig(XmlDocument xdXmlApplicationConfiguration, string strWebserviceId)
        {
            this.xdXmlApplicationConfiguration = xdXmlApplicationConfiguration;
            this.strWebserviceId = strWebserviceId;
        }


        public bool XmlSignatureValidate(XmlDocument xd)
        {
            bool bolPassedTokenValidation = false;
            bool bolPassedContentValidation = false;
            string strDebug = "";

            //exit if this is an open soap connection
            if (strWebserviceId != null)
            {
                if (!IsSecuredWebservice())
                    return true;
            }

            //retrieve data from the application configuration
            string? strSharedKey = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/shared_key/pattern")?.InnerText;
            string? strAlgorithmSignatureUsername = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/shared_key/digest_algoritm")?.InnerText;
            string? strAlgorithmSignatureContent = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/signature_content/digest_algoritm")?.InnerText;
            string? strNameSpacePrefix = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/namespace/@prefix")?.InnerText;
            string? strNameSpaceUri = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/namespace/@uri")?.InnerText;

            //retrieve data from the xml doc passed
            XmlNamespaceManager nsManager = new XmlNamespaceManager(xd.NameTable);
            nsManager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            string? strUserName = xd.SelectSingleNode("//security/UsernameToken/Username")?.InnerText;
            string? strNonce = xd.SelectSingleNode("//security/UsernameToken/Nonce")?.InnerText;
            string? strCreated = xd.SelectSingleNode("//security/UsernameToken/Created")?.InnerText;
            string? strDigestUsernameToken = xd.SelectSingleNode("//security/ds:Signature/ds:SignatureValue", nsManager)?.InnerText;
            string? strDigestContent = xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestValue", nsManager)?.InnerText;
            string? strXpath = xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:Reference/ds:Transforms/ds:Transform/ds:XPath", nsManager)?.InnerText;
            XmlNode? objContentNode = xd.SelectSingleNode(strXpath);
            string? strContent = objContentNode?.OuterXml;

            if (strContent != null)
                //strip the ascii characters above 127 from the string to avoid issues with content signature calculation
                strContent = Regex.Replace(strContent, @"[^\x00-\x7F]", "");

            //generate the shared key
            if (strSharedKey != null)
            {
                strSharedKey = strSharedKey.Replace(@"[nonce]", strNonce);
                strSharedKey = strSharedKey.Replace(@"[created]", strCreated);
                strSharedKey = strSharedKey.Replace(@"[username]", strUserName);
            }

            string strDigestComputed = strAlgorithmSignatureUsername switch
            {
                "md5" => md5(strSharedKey),
                "sha1" => sha1(strSharedKey),
                "hmac-sha1" => hmacsha1(strUserName, strSharedKey),
                _ => "",
            };

            //validate the username token
            if (strDigestUsernameToken == strDigestComputed)
            {
                bolPassedTokenValidation = true;
                strDebug += "- PASSED token validation";
            }
            else
            {
                strDebug += "- FAILED token validation (" + strAlgorithmSignatureUsername + ")(" + strSharedKey + ")";
            }
            strDebug += "*" + strDigestUsernameToken + " - " + strDigestComputed;

            //2) generate the content hash
            strDigestComputed = strAlgorithmSignatureContent switch
            {
                "md5" => md5(strContent),
                "sha1" => sha1(strContent),
                _ => "",
            };

            //validate content signature
            if (strDigestContent == strDigestComputed)
            {
                bolPassedContentValidation = true;
                strDebug += "- PASSED content validation";
            }

            strDebug += "*" + strDigestContent + " - " + strDigestComputed;
            //throw RaiseException(sThisUrlPath, "http://webservice.wdka.hro.nl", strDebug, "500", "ValidateXmlSignature()");

            if (bolPassedContentValidation && bolPassedTokenValidation)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //signs the xml document before it's returned
        public XmlDocument XmlSignatureCreate(XmlDocument xd)
        {
            if (IsSecuredWebservice() == false) 
                return xd;

            string strDigestComputed;

            //retrieve data from the application configuration
            string? strSharedKey = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/shared_key/pattern")?.InnerText;
            string? strAlgorithmSignatureUsername = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/shared_key/digest_algoritm")?.InnerText;
            string? strAlgorithmSignatureContent = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/signature_content/digest_algoritm")?.InnerText;
            string? strNameSpacePrefix = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/namespace/@prefix")?.InnerText;
            string? strNameSpaceUri = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/namespace/@uri")?.InnerText;

            //retrieve data from the xml doc passed
            XmlNamespaceManager nsManager = new XmlNamespaceManager(xd.NameTable);
            nsManager.AddNamespace("ds", Contants.NAMESPACE_DSIG);
            string? strUserName = strWebserviceId;
            string strNonce = RandomString(24, true);
            DateTime CurrTime = DateTime.Now;
            string strCreated = CurrTime.ToString();

            //generate the shared key
            if (strSharedKey != null)
            {
                strSharedKey = strSharedKey.Replace(@"[nonce]", strNonce);
                strSharedKey = strSharedKey.Replace(@"[created]", strCreated);
                strSharedKey = strSharedKey.Replace(@"[username]", strUserName);
                //strSharedKey += "kk";
            }

            //retrieve the template containig the base structure for the signature
            XmlNode objNodeSecurity = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/template/security").CloneNode(true);
            XmlNode objNodeSecurityImported = xd.ImportNode(objNodeSecurity, true);

            //insert it in the xml document
            XmlNode? objNodeReference = xd.SelectSingleNode("//data", nsManager);
            if (objNodeReference != null)
            {

                objNodeReference.ParentNode.InsertBefore(objNodeSecurityImported, objNodeReference);

                //insert base info
                xd.SelectSingleNode("//security/UsernameToken/Username").InnerText = strUserName ?? string.Empty;
                xd.SelectSingleNode("//security/UsernameToken/Nonce").InnerText = strNonce;
                xd.SelectSingleNode("//security/UsernameToken/Created").InnerText = strCreated;

                //sign username token
                strDigestComputed = strAlgorithmSignatureUsername switch
                {
                    "md5" => md5(strSharedKey),
                    "sha1" => sha1(strSharedKey),
                    "hmac-sha1" => hmacsha1(strUserName, strSharedKey),
                    _ => "",
                };

                string? strTemp = xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:SignatureMethod/@Algorithm", nsManager)?.InnerText;
                xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:SignatureMethod/@Algorithm", nsManager).InnerText = strTemp + strAlgorithmSignatureUsername;
                xd.SelectSingleNode("//security/ds:Signature/ds:SignatureValue", nsManager).InnerText = strDigestComputed;

                //sign the content
                string? strXpathContent = xdXmlApplicationConfiguration.SelectSingleNode("/configuration/applications/application[@id='" + strWebserviceId + "']/security/signature_content/xpath")?.InnerText;
                xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:Reference/ds:Transforms/ds:Transform/ds:XPath", nsManager).InnerText = strXpathContent;
                string? strContent = xd.SelectSingleNode(strXpathContent)?.OuterXml;

                if (strContent != null)
                    //strip the ascii characters above 127 from the string to avoid issues with content signature calculation
                    strContent = Regex.Replace(strContent, @"[^\x00-\x7F]", "");

                //PageUtilities oPageUtils = new PageUtilities();
                //oPageUtils.TextFileCreate(strContent, "D:/my_documents/content_to-be-signed.txt");

                //convert the string to ascii befor calculating the digest
                //byte[] utf8String = Encoding.UTF8.GetBytes(strContent);
                //byte[] asciiString = Encoding.ASCII.GetBytes(strContent);
                //byte[] arrDoubleByte=Encoding.Unicode.GetBytes(strContent);

                //strContent = Encoding.ASCII.GetString(asciiString);
                //strContent = Encoding.UTF8.GetString(utf8String);
                //strContent = Encoding.Unicode.GetString(arrDoubleByte);


                strDigestComputed = strAlgorithmSignatureContent switch
                {
                    "md5" => md5(strContent),
                    "sha1" => sha1(strContent),
                    _ => "",
                };

                strTemp = xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestMethod/@Algorithm", nsManager)?.InnerText;
                xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestMethod/@Algorithm", nsManager).InnerText = strTemp + strAlgorithmSignatureContent;
                xd.SelectSingleNode("//security/ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestValue", nsManager).InnerText = strDigestComputed;
                //throw RaiseException(sThisUrlPath, "http://webservice.wdka.hro.nl", xd.OuterXml, "500", "ValidateXmlSignature()");
            }
            return xd;
        }

        private bool IsSecuredWebservice()
        {
            XmlNodeList? objNodeList = xdXmlApplicationConfiguration.SelectNodes("/configuration/applications/application[@id='" + strWebserviceId + "']/security");
            if (objNodeList?.Count > 0)
                return true;
            return false;
        }

        /// <summary>
        /// Generates a random string with the given length
        /// </summary>
        /// <param name="size">Size of the string</param>
        /// <param name="lowerCase">If true, generate lowercase string</param>
        /// <returns>Random string</returns>
        private static string RandomString(int size, bool lowerCase)
        {
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }
            if (lowerCase)
                return builder.ToString().ToLower();
            return builder.ToString();
        }
    }
}