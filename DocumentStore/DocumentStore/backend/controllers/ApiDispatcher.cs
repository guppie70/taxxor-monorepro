using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Links routes to API routes to C# controllers based on the Page ID from site_structure.xml
        /// </summary>
        /// <returns>The pages.</returns>
        /// <param name="request">Request.</param>
        /// <param name="response">Response.</param>
        /// <param name="routeData">Route data.</param>
        public async static Task DispatchApi(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);

            // Console.WriteLine($"*********** In API Dispatcher with PageID: '{reqVars.pageId}' ***********");

            if (!reqVars.isStaticAsset)
            {
                switch (reqVars.pageId)
                {
                    case "serviceinformation":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderServiceDescription(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "systemstate":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveSystemState(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await SetSystemState(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "projectconfiguration":
                    case "dataconfiguration":
                        routeData.Values.Add("fileid", Path.GetFileName(reqVars.thisUrlPath));
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveXmlConfiguration(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "projecttemplatesconfiguration":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveCmsTemlatesXmlConfiguration(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "cmsprojectmanagement":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListAvailableProjects(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CreateProjectStructure(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await DeleteProject(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "cloneproject":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await CloneExistingProject(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "projectproperties":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await SaveProjectProperties(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorsyncexternaltables":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await SyncExternalTables(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorsyncstructureddatasnapshot":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Delete:
                                await SyncStructuredDataElementsRemoveCache(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await SyncStructuredDataElementsCreateLog(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorsyncstructureddatasnapshotperdataref":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await SyncStructuredDataElementsPerDataReference(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "structureddataelementcachestate":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await UpdateCachedStructuredDataElementStatus(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditoruserdata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveUserData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                            case RequestMethodEnum.Post:
                                await SetUserData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorusertempdata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveUserTempData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                            case RequestMethodEnum.Post:
                                await StoreUserTempData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await DeleteUserTempData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorcalendardata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveCalendarData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                            case RequestMethodEnum.Post:
                                await StoreCalendarData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorauditordatamessage":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Put:
                                await StoreAuditorViewMessage(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorauditordataoverview":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveAuditorOverviewData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxorservicesinformation":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveTaxxorServicesConfiguration(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorfilingimage":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveImageData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorfilingbinary":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveFileBinary(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorversionmanager":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveVersionManagerData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CreateVersion(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await DeleteVersion(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorcachefiles":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListVersionCacheFiles(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "taxxoreditorcomposerschema":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveFilingComposerSchema(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorpdfdata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await RetrievePdfSourceData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorpdfhistoricaldata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrievePdfHistoricalSourceData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorprojectassetsinformation":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveProjectAssetsInformation(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await CreateProjectAssetsInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorallcontent":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveAllSourceData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "footnote":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveFootnote(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await UpdateFootnote(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CreateFootnote(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await RemoveFootnote(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "listfootnotes":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListFootnotes(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "contentversion":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveContentVersion(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CommitFilingContentRevision(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorcontentanalysis":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await DataAnalysisListScenarios(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await AnalyzeEditorData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditordataservicetools":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await DataServiceToolsList(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await ExecuteDataServiceTool(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "taxxoreditorfilingmetadata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveCmsMetadata(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await SetCmsMetadata(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CompileCmsMetadata(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorwebsitegeneratorexportassets":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await ExportWebsiteContent(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorfileexists":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await FileExists(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditordirectorycontents":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await DirectoryContents(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorfilecontents":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveFileContents(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditordeltree":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await DelTree(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorcopydirectory":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await CopyDirectory(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorfileproperties":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await FileProperties(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorimagerenditions":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await CreateImageRenditions(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorremoveimagerenditions":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await RemoveImageRenditions(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditormoveimagerenditions":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await MoveImageRenditions(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorrendergraphs":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await RetrieveContentForGraphGeneration(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "gitpull":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitPull(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitcommitinfo":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitRetrieveFullMessage(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitcommitfiles":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitRetrieveFileListFromCommit(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitactive":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await GitIsActiveRepository(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitcommit":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitCommit(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitextractall":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitExtractAll(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "gitextractsingle":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitExtractSingleFile(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitdiff":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GitDiffBetweenCommits(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "filemanagerapi":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await FileOperations(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await HandleFileUpload(request, response, routeData);
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
                            HandleError(reqVars, $"Could not process this request", $"DispatchApi(): Could not find controller for page with id '{reqVars.pageId}'", 404);
                        }

                        break;

                }
            }
            else
            {
                HandleError(reqVars, $"No static content available", $"DispatchApi(): The request for a static asset '{request.Path}' cannot be served because the path is reserved for dynamic page content");
            }
        }

    }
}