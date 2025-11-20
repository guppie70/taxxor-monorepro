using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in saving editor data to the server
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Load XML data into the editor (text, config or other XML data)
        /// </summary>
        public static async Task CreateFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var versionId = request.RetrievePostedValue("vid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var dataType = request.RetrievePostedValue("type", true, ReturnTypeEnum.Xml);

            // Retrieve the data that we want to store
            var sectionId = request.RetrievePostedValue("sectionid", true, ReturnTypeEnum.Xml);

            var dataToStore = request.RetrievePostedValue("data", RegexEnum.None, true, ReturnTypeEnum.Xml);

            if (debugRoutine) Console.WriteLine($"** Received Data: {dataToStore}");

            if (ValidateCmsPostedParameters(projectId, versionId, dataType))
            {

                try
                {
                    // Load the data that we have received
                    var xmlFilingSectionData = new XmlDocument();
                    xmlFilingSectionData.LoadXml(dataToStore);

                    var dataFolderPathOs = RetrieveFilingDataFolderPathOs();

                    // Calculate a new unique filename
                    List<string> filenames = new List<string>();
                    string[] sectionFilePathOs = Directory.GetFiles(dataFolderPathOs, "*.xml", SearchOption.TopDirectoryOnly);
                    foreach (string xmlPathOs in sectionFilePathOs)
                    {
                        try
                        {
                            filenames.Add(Path.GetFileName(xmlPathOs));
                        }
                        catch (Exception ex)
                        {
                            HandleError(ReturnTypeEnum.Xml, "There was an error reading the section xml files on the Taxxor Document Store", $"xmlPathOs: {xmlPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                        }
                    }

                    var postfix = 1;
                    var newSectionId = sectionId;
                    var newSectionFileName = $"{sectionId}.xml";
                    while (filenames.Exists(element => element == newSectionFileName))
                    {
                        newSectionId = $"{sectionId}-{postfix}";
                        newSectionFileName = $"{newSectionId}.xml";
                        postfix++;
                    }

                    // Update the section id's in the file to match the filename -> that way we know it's unique
                    var nodeListArticles = xmlFilingSectionData.SelectNodes("//article");
                    foreach (XmlNode nodeArticle in nodeListArticles)
                    {
                        nodeArticle.SetAttribute("id", newSectionId);
                        nodeArticle.SetAttribute("data-guid", newSectionId);
                        nodeArticle.SetAttribute("data-fact-id", newSectionId);
                    }

                    // Store the new section XML file
                    TaxxorReturnMessage postProcessResult = null;
                    string xmlFilePathOs = $"{dataFolderPathOs}/{newSectionFileName}";
                    postProcessResult = await SaveXmlInlineFilingComposerData(xmlFilingSectionData, xmlFilePathOs, projectVars);
                    if (!postProcessResult.Success) HandleError(ReturnTypeEnum.Xml, postProcessResult.Message, postProcessResult.DebugInfo);

                    //
                    // => Update the metadata entry
                    //
                    await UpdateCmsMetadataEntry(projectId, Path.GetFileName(xmlFilePathOs));

                    // Commit in GIT
                    var sectionTitle = "[unknown]";
                    var nodeContentHeader = xmlFilingSectionData.SelectSingleNode("/data/content/article//section/h1") ?? xmlFilingSectionData.SelectSingleNode("/data/content//title");
                    if (nodeContentHeader != null) sectionTitle = nodeContentHeader.InnerText;

                    XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                    xmlCommitMessage.SelectSingleNode("/root/crud").InnerText = "c"; // Create
                    xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = sectionTitle; // Linkname
                    xmlCommitMessage.SelectSingleNode("/root/id").InnerText = sectionId; // Section ID
                    var message = xmlCommitMessage.DocumentElement.InnerXml;

                    // Commit the result in the GIT repository
                    CommitFilingData(message, ReturnTypeEnum.Xml, true);

                    // Render the overview and return it
                    await RetrieveFilingDataOverview(request, response, routeData);
                }
                catch (Exception ex)
                {
                    HandleError(ReturnTypeEnum.Xml, "There was an error creating new filing composer data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

            }
            else
            {
                HandleError(ReturnTypeEnum.Json, GenerateErrorXml("Could not validate posted data", $"projectId: {projectId}, versionId: {versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}"));
            }
        }

    }
}