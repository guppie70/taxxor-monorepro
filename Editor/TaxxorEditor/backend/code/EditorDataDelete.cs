using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
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
        /// Removes a filing section data fragment from the Taxxor Data Store
        /// </summary>
        public static async Task DeleteFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted data
            var dataref = request.RetrievePostedValue("dataref", @"^[a-zA-Z_\|\-!\?@#',\.\s\d:\/\(\)\p{IsCJKUnifiedIdeographs}]{1,4000}$", true, reqVars.returnType);
            var renderHierarchyOverviewString = request.RetrievePostedValue("renderhierarchyoverview", RegexEnum.Boolean, true, reqVars.returnType);
            var renderHierarchyOverview = (renderHierarchyOverviewString == "true");

            // Create an XML report that contains all the potential elements that we need to delete
            List<string> dataRefs = new List<string>();
            switch (dataref)
            {
                case string a when a.Contains("|"):
                    dataRefs = dataref.Split('|').ToList();
                    break;

                case string b when b.Contains(","):
                    dataRefs = dataref.Split(',').ToList();
                    break;

                default:
                    dataRefs.Add(dataref);
                    break;
            }

            var deleteFilingSectionsResult = await RenderDeleteFilingSectionReport(dataRefs);
            if (!deleteFilingSectionsResult.Success) HandleError(deleteFilingSectionsResult);
            // Console.WriteLine("****************");
            // Console.WriteLine(deleteFilingSectionsResult.ToString());
            // Console.WriteLine("****************");
            // HandleError("Thrown on purpose");




            // Call the Taxxor Data store to save the new section
            var xmlResult = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.DeleteSourceData(deleteFilingSectionsResult.XmlPayload, reqVars.isDebugMode);

            if (XmlContainsError(xmlResult)) HandleError("There was an error removing your filing data", $"debuginfo: {xmlResult.SelectSingleNode("/error/debuginfo").InnerText}, stack-trace: {GetStackTrace()}");

            var htmlAllItems = "";
            var htmlHierarchyItems = "";

            if (renderHierarchyOverview)
            {
                // Render the complete hierarchy, but only return the full list
                var xhtml = await RenderCmsHierarchyManagerBody(false);

                // Load the rendered XHTML in an XmlDocument, so that we can extract the "all items" list from the HTML and return it to the client
                var xhtmlDoc = new XmlDocument();
                xhtmlDoc.LoadXml(xhtml);

                var nodeAllItemsList = xhtmlDoc.SelectSingleNode("/div/div[3]/div[1]/ul");
                if (nodeAllItemsList == null) HandleError("Could not find complete list of elements", $"stack-trace: {GetStackTrace()}");

                var nodeHierarchyItemsList = xhtmlDoc.SelectSingleNode("/div/div[1]/div[1]/ul");
                if (nodeHierarchyItemsList == null) HandleError("Could not find hierarchical list of elements", $"stack-trace: {GetStackTrace()}");

                // Grab the XHTML for the "all items" list
                htmlAllItems = nodeAllItemsList.OuterXml;

                // Grab the XHTML for the "hierarchy items" list
                htmlHierarchyItems = nodeHierarchyItemsList.OuterXml;
            }

            //
            // => Update the local CMS metadata object
            //
            await UpdateCmsMetadata(projectVars.projectId);

            // Construct a response message for the client
            dynamic jsonData = new ExpandoObject();
            jsonData.result = new ExpandoObject();
            jsonData.result.message = "Successfully removed filing section(s)";
            if (isDevelopmentEnvironment)
            {
                jsonData.result.debuginfo = "You can place debug information here";
            }
            jsonData.result.htmlallitems = htmlAllItems;
            jsonData.result.htmlhierarchyitems = htmlHierarchyItems;

            string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

            //HandleError("Needs to be implemented", $"xmlAllItems: {HtmlEncodeForDisplay(xmlAllItems.OuterXml)}, xmlStoreResult: {HtmlEncodeForDisplay(html)}, stack-trace: {GetStackTrace()}");

            await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
        }

    }
}