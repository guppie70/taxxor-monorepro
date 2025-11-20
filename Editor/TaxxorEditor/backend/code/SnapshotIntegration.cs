using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;

namespace Taxxor.Project
{

    /// <summary>
    /// Variables and logic specific for this project
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the XML of a complete snapshot file
        /// </summary>
        /// <param name="snapShotGuid"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveSnapshotData(string snapShotGuid)
        {
            var context = System.Web.Context.Current;
            var projectVars = RetrieveProjectVariables(context);

            XmlDocument xmlSnapshot = new XmlDocument();
            var xpath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/versions/version[@id=" + GenerateEscapedXPathString(projectVars.versionId) + "]/data/financial_data/snapshots//snapshot[@guid=" + GenerateEscapedXPathString(snapShotGuid) + "]/location";
            XmlNodeList? xmlNodeList = xmlApplicationConfiguration.SelectNodes(xpath);

            if (xmlNodeList.Count > 0)
            {
                var xmlNode = xmlNodeList.Item(0);
                var locationType = xmlNode.Attributes["type"].Value;
                var pathType = xmlNode.Attributes["path-type"].Value;

                if (locationType == "file")
                {
                    var strPathOs = CalculateFullPathOs(xmlNode);
                    if (File.Exists(strPathOs))
                    {
                        xmlSnapshot.Load(strPathOs);
                        //Response.Write(HtmlEncodeForDisplay(xmlSnapshot.DocumentElement.OuterXml));
                    }
                    else
                    {
                        await context.Response.WriteAsync("ERROR: Snapshot data could not be found");
                    }

                }
                else
                {
                    // Retrieve via webservice
                    if (pathType == "webservice")
                    {
                        var webserviceId = locationType;
                        XmlDocument xmlSoapMethodParameters = new XmlDocument();
                        xmlSoapMethodParameters.LoadXml("<data><guid>" + snapShotGuid + "</guid></data>");

                        if (DebugAllWebservices) AppendLogDataInFile("SOAP,ID=" + webserviceId + ",METHOD=RetrieveFullSnapshot", logRootPathOs + "/webservice-requests.log");
                        XmlDocument? xmlSoapResponse = await SoapRequest(webserviceId, "RetrieveFullSnapshot", xmlSoapMethodParameters, xmlApplicationConfiguration);

                        if (xmlSoapResponse.DocumentElement.LocalName != "error")
                        {
                            // Retrieve the xml snapshot information from the response
                            xmlSnapshot.LoadXml(xmlSoapResponse.SelectSingleNode("//data[system]").OuterXml);
                        }
                        else
                        {
                            HandleError(ReturnTypeEnum.Xml, "Error in RetrieveSnapshotData()", $"Webservice error (id: '{webserviceId}'), raw SOAP response: {xmlSoapResponse.OuterXml}");
                        }

                    }
                }
            }

            return xmlSnapshot;

        }

        /// <summary>
        /// Retrieves an XML document containing a list of the available snapshots
        /// </summary>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveAvailableSnapshotsUsingSoap()
        {
            XmlDocument xmlSoapMethodParameters = new XmlDocument();
            xmlSoapMethodParameters.LoadXml("<data><test>test-image-base64</test></data>");

            if (DebugAllWebservices) AppendLogDataInFile("SOAP,ID=" + "soap_retrieve-snapshot-data" + ",METHOD=RetrieveAvailableSnapshots", logRootPathOs + "/webservice-requests.log");
            XmlDocument? xmlSoapResponse = await SoapRequest("soap_retrieve-snapshot-data", "RetrieveAvailableSnapshots", xmlSoapMethodParameters, xmlApplicationConfiguration);

            if (xmlSoapResponse.DocumentElement.LocalName == "error")
            {

                HandleError(ReturnTypeEnum.Xml, "Error in RetrieveAvailableSnapshotsUsingSoap()", "Webservice error (id: 'RetrieveAvailableSnapshots'), SOAP response: " + xmlSoapResponse.OuterXml);
            }

            return xmlSoapResponse;

        }

        /// <summary>
        /// Retrieves the snapshot metadata of the current project and current version
        /// </summary>
        /// <param name="snapShotGuid"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveSnapshotMetadata(string snapShotGuid)
        {
            var context = System.Web.Context.Current;
            var projectVars = RetrieveProjectVariables(context);

            return await RetrieveSnapshotMetadata(projectVars.projectId, projectVars.versionId, snapShotGuid);
        }

        /// <summary>
        /// Retrieves the snapshot metadata of the current project and a specific version of the project
        /// </summary>
        /// <param name="versionId"></param>
        /// <param name="snapShotGuid"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveSnapshotMetadata(string versionId, string snapShotGuid)
        {
            var context = System.Web.Context.Current;
            var projectVars = RetrieveProjectVariables(context);

            return await RetrieveSnapshotMetadata(projectVars.projectId, versionId, snapShotGuid);
        }

        /// <summary>
        /// Retrieves the snapshot metadata of a specific project and a specific version of the project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="snapShotGuid"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveSnapshotMetadata(string projectId, string versionId, string snapShotGuid)
        {
            XmlDocument xmlSnapshotMetadata = new XmlDocument();
            var xpath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/versions/version[@id=" + GenerateEscapedXPathString(versionId) + "]/data/financial_data/snapshots//snapshot[@guid=" + GenerateEscapedXPathString(snapShotGuid) + "]/location";
            XmlNodeList? xmlNodeList = xmlApplicationConfiguration.SelectNodes(xpath);

            if (xmlNodeList.Count > 0)
            {
                var xmlNode = xmlNodeList.Item(0);
                var locationType = xmlNode.Attributes["type"].Value;
                var pathType = xmlNode.Attributes["path-type"].Value;
                var path = xmlNode.InnerText;

                xmlSnapshotMetadata = await RetrieveSnapshotMetadata(projectId, versionId, snapShotGuid, locationType, pathType, path);
            }

            return xmlSnapshotMetadata;
        }

        /// <summary>
        /// Retrieves the snapshot metadata
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="versionId"></param>
        /// <param name="snapShotGuid"></param>
        /// <param name="locationType"></param>
        /// <param name="pathType"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task<XmlDocument> RetrieveSnapshotMetadata(string projectId, string versionId, string snapShotGuid, string locationType, string pathType, string path)
        {
            var context = System.Web.Context.Current;

            XmlDocument xmlSnapshotMetadata = new XmlDocument();

            if (locationType == "file")
            {
                var strPathOs = CalculateFullPathOs(path, pathType);
                if (File.Exists(strPathOs))
                {
                    xmlSnapshotMetadata.Load(strPathOs);
                    xmlSnapshotMetadata.LoadXml(xmlSnapshotMetadata.SelectSingleNode("//system").OuterXml);
                    //Response.Write(HtmlEncodeForDisplay(xmlSnapshot.DocumentElement.OuterXml));
                }
                else
                {
                    WriteErrorMessageToConsole("ERROR: Snapshot data could not be found");

                }

            }
            else
            {
                //retrieve via webservice
                if (pathType == "webservice")
                {
                    var webserviceId = locationType;
                    XmlDocument xmlSoapMethodParameters = new XmlDocument();
                    xmlSoapMethodParameters.LoadXml("<data><guid>" + snapShotGuid + "</guid></data>");

                    if (DebugAllWebservices) AppendLogDataInFile("SOAP,ID=" + webserviceId + ",METHOD=RetrieveSnapshotMetadata", logRootPathOs + "/webservice-requests.log");
                    XmlDocument? xmlSoapResponse = await SoapRequest(webserviceId, "RetrieveSnapshotMetadata", xmlSoapMethodParameters, xmlApplicationConfiguration);

                    if (xmlSoapResponse.DocumentElement.LocalName == "error")
                    {
                        HandleError(ReturnTypeEnum.Xml, "Error in RetrieveSnapshotMetadata()", $"Webservice error (id: 'RetrieveSnapshotMetadata'), SOAP response: {xmlSoapResponse.OuterXml}");
                    }

                    //retrieve the xml snapshot information from the response
                    xmlSnapshotMetadata.LoadXml(xmlSoapResponse.SelectSingleNode("//system").OuterXml);

                    // await xmlSnapshot.SaveAsync(applicationConfigurationPathOs + "/temp/snapshot_retrieved.xml");

                }
            }

            return xmlSnapshotMetadata;
        }



    }
}