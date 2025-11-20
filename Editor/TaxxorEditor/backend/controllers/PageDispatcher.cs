using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Links routes to CMS pages to C# controllers based on the Page ID from site_structure.xml
        /// </summary>
        /// <returns>The pages.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task DispatchPages(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {

                    case "cms-overview":
                        await DefaultPageRenderer(request, response, routeData);
                        //LoadCalendarDataXml();
                        break;

                    case "cms_auditor-view":
                    case "cms_version-manager":
                    case "cms_hierarchy-manager":
                    case "cms_development-page":
                    case "cms_publicationvariants":
                    case "cms_administration-page":
                    case "cms_html-styler":
                    case "cms_generatorinput":
                    case "cms_accesscontrolmanager":
                    case "cms_vscode-reportdesignpackages":
                        await DefaultPageRenderer(request, response, routeData);
                        break;

                    case "cms_content-editor":
                        // Mark the application configuration so that we know the first editable page that this user is allowed to edit
                        await SetFirstEditablePageIdsForAllOutputChannels();

                        projectVars.idFirstEditablePage = context.Request.RetrievePostedValue("did", RegexEnum.Loose);

                        await DefaultPageRenderer(request, response, routeData);
                        break;

                    case "cms_load-cache-files":
                        await RetrieveFileFromCache(request, response, routeData);
                        break;

                    case "cms_preview-pdfdocument":
                        await SetFirstEditablePageIdsForAllOutputChannels();
                        await DefaultPageRenderer(request, response, routeData);
                        break;

                    case "filingcomposer-redirect":
                        await SetFirstEditablePageIdsForAllOutputChannels();
                        await FilingComposerRedirect(request, response, routeData);
                        break;

                    case "results-hub-webpage":
                        // XML version is the HTML page converted by Tidy HTML
                        var xmlPathOs = $"{websiteRootPathOs}{reqVars.thisUrlPath.Replace(".html", ".xml")}";

                        var xmlWebDoc = new XmlDocument();
                        xmlWebDoc.Load(xmlPathOs);

                        // Use the extensions to change the HTML as we load the page
                        xmlWebDoc = Extensions.RenderWebPageEditorContent(context, xmlWebDoc);

                        // Fix CDATA stuff
                        var htmlWebPage = xmlWebDoc.OuterXml.Replace("<![CDATA[", "").Replace("]]>", "").Replace("/*<![CDATA[*/", "").Replace("/*]]>*/", "").Replace("/**/", "");
                        //var htmlWebPage = xmlWebDoc.OuterXml;
                        await response.OK($"<!DOCTYPE html>{htmlWebPage}", ReturnTypeEnum.Html, true);
                        break;

                    default:
                        // Start custom logic
                        bool foundControllerToProcess = await Extensions.RenderPageLogicByPageIdCustom(context, reqVars.pageId);

                        if (!foundControllerToProcess)
                        {
                            HandleError(reqVars, $"Could not process this request", $"DispatchPages(): Could not find controller for page with id '{reqVars.pageId}'", 404);
                        }

                        break;

                }
            }
            else
            {
                HandleError(reqVars, $"No static content available", $"DispatchPages(): The request for a static asset '{request.Path}' cannot be served because the path is reserved for dynamic page content");
            }

        }

    }
}