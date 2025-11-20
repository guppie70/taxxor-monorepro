using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
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

            // Retrieve posted data
            var sectionTitle = request.RetrievePostedValue("sectiontitle", @"^.{1,256}$", true, reqVars.returnType);
            var sectionId = request.RetrievePostedValue("sectionid", true, reqVars.returnType);

            // Current local time with offset
            var createTimeStamp = createIsoTimestamp();

            // Retrieve the basic structure for a section in the document
            var filingDataTemplate = new XmlDocument();
            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
            switch (editorId)
            {
                case "xyz":
                    filingDataTemplate = RetrieveTemplate("inline-editor-content");
                    break;
                default:
                    filingDataTemplate = RetrieveTemplate("inline-editor-content");
                    break;
            }

            // Fill the template with the data that we have received
            var nodeDateCreated = filingDataTemplate.SelectSingleNode("/data/system/date_created");
            var nodeDateModified = filingDataTemplate.SelectSingleNode("/data/system/date_modified");

            // Find the original content node
            var nodeContentOriginal = filingDataTemplate.SelectSingleNode("/data/content");

            // Create one content node per language that is defined in the configuration for this project
            var languages = RetrieveProjectLanguages(projectVars.editorId);
            foreach (var lang in languages)
            {
                var nodeContent = nodeContentOriginal.CloneNode(true);
                var nodeContentArticle = nodeContent.FirstChild;
                var nodeContentHeader = nodeContentArticle.SelectSingleNode("div/section/h1");
                if (nodeDateCreated == null || nodeDateModified == null || nodeContentArticle == null || nodeContentHeader == null)
                {
                    HandleError("Could not find elements in data template", $"stack-trace: {GetStackTrace()}");
                }

                nodeDateCreated.InnerText = createTimeStamp;
                nodeDateModified.InnerText = createTimeStamp;
                SetAttribute(nodeContent, "lang", lang);
                // Temporary ID is set - in the project datastore we will test if the ID is unique and if it may need to be replaced by something else
                SetAttribute(nodeContentArticle, "id", sectionId);
                SetAttribute(nodeContentArticle, "data-guid", sectionId);
                SetAttribute(nodeContentArticle, "data-last-modified", createTimeStamp);
                nodeContentHeader.InnerText = sectionTitle;

                filingDataTemplate.DocumentElement.AppendChild(nodeContent);
            }

            // Remove the original node
            RemoveXmlNode(nodeContentOriginal);

            // Call the Taxxor Data store to save the new section
            var xmlResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.CreateSourceData(sectionId, filingDataTemplate, reqVars.isDebugMode);

            if (XmlContainsError(xmlResult)) HandleError("There was an error creating your filing data", $"debuginfo: {xmlResult.SelectSingleNode("/error/debuginfo").InnerText}, stack-trace: {GetStackTrace()}");

            //
            // => Update the local CMS metadata object
            //
            await UpdateCmsMetadata(projectVars.projectId);

            // Render the complete hierarchy, but only return the full list
            var xhtml = await RenderCmsHierarchyManagerBody(false);

            // Load the rendered XHTML in an XmlDocument, so that we can extract the "all items" list from the HTML and return it to the client
            var xhtmlDoc = new XmlDocument();
            xhtmlDoc.LoadXml(xhtml);

            var nodeAllItemsList = xhtmlDoc.SelectSingleNode("/div/div[3]/div[1]/ul");
            if (nodeAllItemsList == null) HandleError("Could not find complete list of elements", $"stack-trace: {GetStackTrace()}");

            // Grab the XHTML for the "all items" list
            var html = nodeAllItemsList.OuterXml;

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.result = new ExpandoObject();
            jsonData.result.message = "Successfully created filing section";
            if (isDevelopmentEnvironment)
            {
                jsonData.result.debuginfo = "You can place debug information here";
            }
            jsonData.result.html = html;

            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

            await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
        }

    }
}