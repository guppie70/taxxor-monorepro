using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Links incoming HTTP calls to the /utilities/* to C# controllers based on the Page ID from site_structure.xml
        /// </summary>
        /// <returns>The sandbox test scripts.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task UtilitiesDispatcher(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Console.WriteLine($"*********** In Utilities Dispatcher with PageID: '{reqVars.pageId}' ***********");

            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {
                    case "cms-editor-iframecontent":
                        await WriteEditorIframeContent(request, response, routeData);
                        break;


                    case "udynamic_resources_utility":
                    case "udynamic_resources_cached_utility":
                        await HandleAnonymousDynamicContentRequest(request, response, routeData);
                        break;

                    case "udynamic_resources_utility_closed":
                    case "udynamic_resources_cached_utility_closed":
                        await HandleAuthenticatedDynamicContentRequest(request, response, routeData);
                        break;

                    case "u-cms_json_data":
                        await WriteCmsJsonData(request, response, routeData);
                        break;

                    case "u-cms_data-retriever":
                        await WriteRetrievedFilingComposerXmlData(request, response, routeData);
                        break;

                    case "u-cms_data-saver":
                        await SaveFilingComposerData(request, response, routeData);
                        break;


                    case "plugin-edgarfilerxhr":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await EdgarFilerXhr(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "retrieveoutputchannelinfo":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveOutputChannelInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "retrievehtmlstylerdocument":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderHtmlStylerContent(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "retrievegeneratorinput":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderGeneratorInputContent(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;
                


                    default:
                        // Start custom logic
                        bool foundControllerToProcess = await Extensions.RenderPageLogicByPageIdCustom(context, reqVars.pageId);

                        if (!foundControllerToProcess)
                        {
                            HandleError(reqVars, $"Could not process this request", $"UtilitiesDispatcher(): Could not find controller for page with id '{reqVars.pageId}'", 404);
                        }

                        break;

                }
            }
            else
            {
                HandleError(reqVars, $"No static content available", $"UtilitiesDispatcher(): The request for a static asset '{request.Path}' cannot be served because the path is reserved for dynamic page content");
            }
        }




    }
}