using System;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Compiles information about the connected Taxxor services and stores that in the application configuration in-memory XmlDocument object.
        /// </summary>
        /// <returns>The connected services information.</returns>
        /// <param name="siteType">Site type.</param>
        public async static Task CompileConnectedServicesInformation(string siteType)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Loop through the services and retrieve the remote information
            var nodeListServices = xmlApplicationConfiguration.SelectNodes("/configuration/taxxor/components/service//*[local-name()='service' or local-name()='web-application']");
            foreach (XmlNode nodeService in nodeListServices)
            {
                var serviceStatus = "OK";
                var serviceId = GetAttribute(nodeService, "id");
                var disableHealthCheck = GetAttribute(nodeService, "disablehealthcheck") ?? "false";
                if (disableHealthCheck == "false")
                {
                    var nodeLocation = (nodeService.SelectSingleNode("uri/domain") == null) ? nodeService.SelectSingleNode("uri") : nodeService.SelectSingleNode($"uri/domain[@type='{siteType}']");
                    var baseUri = nodeLocation?.InnerText?? "";
                    var useServiceDescription = true;
                    var serviceDescriptionAttributeValue = GetAttribute(nodeService, "servicedescription");
                    if (!string.IsNullOrEmpty(serviceDescriptionAttributeValue))
                    {
                        if (serviceDescriptionAttributeValue == "false") useServiceDescription = false;
                    }

                    if (debugRoutine)
                    {
                        // AddDebugVariable(() => serviceId);
                        // AddDebugVariable(() => baseUri);
                    }

                    CustomHttpHeaders customHttpHeaders = new CustomHttpHeaders();
                    if (!string.IsNullOrEmpty(SystemUser))
                    {
                        customHttpHeaders.AddTaxxorUserInformation(SystemUser);
                    }

                    if (useServiceDescription)
                    {
                        // Retrieve the service description by requesting the base URI
                        XmlDocument xmlServiceDescription = new XmlDocument();

                        if (serviceId == applicationId)
                        {
                            // Retrieve file from application
                            xmlServiceDescription = GenerateServiceDescription();

                            // Console.WriteLine(xmlServiceDescription.OuterXml);
                        }
                        else
                        {
                            var serviceInformationUri = $"{baseUri}/api/configuration/serviceinformation";
                            if (UriLogEnabled)
                            {
                                if (!UriLogBackend.Contains(serviceInformationUri)) UriLogBackend.Add(serviceInformationUri);
                            }

                            // Retrieve the service description using HTTP
                            xmlServiceDescription = await RestRequest<XmlDocument>(RequestMethodEnum.Get, serviceInformationUri, customHttpHeaders, true, true);
                        }

                        if (XmlContainsError(xmlServiceDescription))
                        {
                            // Handle the error
                            WriteErrorMessageToConsole($"Unable to reach service '{serviceId}' on uri: '{baseUri}'", $"Error message: {xmlServiceDescription.SelectSingleNode("/error/message")?.InnerText ?? ""} (HTTP status code: {xmlServiceDescription.SelectSingleNode("/error/httpstatuscode")?.InnerText??""})");

                            // Convert the HTTP response code of the error to a readable message
                            var responseStatusCode = RetrieveNodeValueIfExists("//httpstatuscode", xmlServiceDescription);
                            serviceStatus = HttpStatusCodeToMessage(responseStatusCode);
                        }
                        else
                        {
                            // Mark existing service information so that it can be removed
                            var nodeListServiceDetails = nodeService.SelectNodes("*");
                            foreach (XmlNode nodeServiceDetails in nodeListServiceDetails)
                            {
                                if (nodeServiceDetails.LocalName != "uri")
                                {
                                    SetAttribute(nodeServiceDetails, "delete", "true");
                                }
                            }

                            // Normalize the received information
                            var xsltArgumentList = new XsltArgumentList();
                            var xmlServiceDescriptionNormalized = TransformXmlToDocument(xmlServiceDescription, $"xsl_normalize-service-description", xsltArgumentList);
                            // Console.WriteLine("-------- service description --------");
                            // Console.WriteLine(xmlServiceDescription.OuterXml);
                            // Console.WriteLine("");

                            // Console.WriteLine("-------- service description normalized --------");
                            // Console.WriteLine(xmlServiceDescriptionNormalized.OuterXml);
                            // Console.WriteLine("");

                            // Add the information of the services to the XML document
                            var nodeListServiceDescription = xmlServiceDescriptionNormalized.SelectNodes("/application/*");
                            foreach (XmlNode nodeServiceDescription in nodeListServiceDescription)
                            {
                                nodeService.AppendChild(xmlApplicationConfiguration.ImportNode(nodeServiceDescription, true));
                            }

                            // Remove the old nodes
                            nodeListServiceDetails = nodeService.SelectNodes("*[@delete='true']");
                            foreach (XmlNode nodeServiceDetails in nodeListServiceDetails)
                            {
                                RemoveXmlNode(nodeServiceDetails);
                            }

                        }
                    }
                    else
                    {
                        // TODO: Use HTTP health check to test the status of the services that do not expose a service description

                    }


                }
                else
                {
                    serviceStatus = "Not checked";
                }



                // Add the status of the connected service to the XML document
                SetAttribute(nodeService, "status", serviceStatus);

                if (serviceId == "documentstore")
                {
                    nodeService.SetAttribute("runningindocker", IsRunningInDocker().ToString().ToLower());
                }
            }

        }
    }
}