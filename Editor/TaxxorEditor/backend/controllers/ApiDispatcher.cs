using System.Threading.Tasks;
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

                    case "u-cms_api-test":
                        await TestApi(request, response, routeData);
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

                    case "project":
                        switch (reqVars.method)
                        {

                            case RequestMethodEnum.Put:
                                await CreateNewProject(request, response, routeData);
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

                    case "listprojects":
                        switch (reqVars.method)
                        {

                            case RequestMethodEnum.Get:
                                await ListProjects(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "availablereporttypes":
                        switch (reqVars.method)
                        {

                            case RequestMethodEnum.Get:
                                await ListAvailableReportTypes(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;


                    case "projectproperties":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ViewProjectProperties(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await SaveProjectProperties(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "projectlanguages":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveProjectLanguages(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "accesscontrol":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderAccessControlOverview(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await EditAccessRecord(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await AddAccessRecordToPage(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await RemoveAccessRecordFromPage(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "accesscontrol-resource":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Put:
                                await EditResourceRecord(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "accesscontrol-bulk":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await BulkUpdateRbacInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "accesscontrolmanager":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await RenderAccessControlHierarchyOverview(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "usersectioninformation":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                            case RequestMethodEnum.Post:
                                await RetrieveUserSectionInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "syncexternaltables":
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


                    case "taxxoreditorcomposerdata":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await WriteRetrievedFilingComposerXmlData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await SaveFilingComposerData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CreateFilingComposerData(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await DeleteFilingComposerData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "taxxoreditorcomposerdataclone":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await CloneSection(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "pdfgenerator":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                            case RequestMethodEnum.Post:
                                await RenderPdf(reqVars.returnType);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "pdfgeneratorsourcenormal":
                    case "pdfgeneratorsourcediff":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderPdfHtml(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "pagedmediasource":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderPagedMediaHtml(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;



                    case "mswordgenerator":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                            case RequestMethodEnum.Post:
                                await RenderMsWord(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "msexcelgenerator":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                            case RequestMethodEnum.Post:
                                await RenderMsExcel(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "xbrlgenerator":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                            case RequestMethodEnum.Post:
                                await RenderXbrl(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "datalineagegenerator":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await RenderDataLineageReport(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "sdereports":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderCollisionReport(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "versionmanager":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                            case RequestMethodEnum.Post:
                                await GenerateVersionManagerContent(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await GenerateVersion(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "versionmanagerdownload":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                var pdfFileName = request.RetrievePostedValue("filename", RegexEnum.FileName, true, reqVars.returnType);
                                var pdfBinaryName = request.RetrievePostedValue("binaryname", RegexEnum.FileName, true, reqVars.returnType);
                                await DownloadFile(pdfFileName, "pdfcompare", pdfBinaryName);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "hierarchymanagerhierarchy":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await WriteCmsHierarchyManagerBody(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await SaveFilingHierarchy(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "hierarchymanagerhierarchyitem":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await GetCmsHierarchyItem(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await UpdateCmsHierarchyItem(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "hierarchymanagerhierarchydeletecheck":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderCmsHierarchyDeleteResultReport(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "hierarchymanageroutputchanneloverview":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderAvailableOutputChannelOverview(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }


                        break;

                    case "contentlanguageclone":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await CloneSectionContentLanguage(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "importhierarchy":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ImportHierarchy(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "filingeditorlock":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveLock(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await UpdateLock(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await CreateLock(request, response, routeData);
                                break;

                            case RequestMethodEnum.Delete:
                                await RemoveLock(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "filingeditorlistlocks":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListLocks(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "javascriptlogger":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await LogClientSideJavaScriptError(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "about":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await AboutTaxxor(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "connectedservices":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ConnectedServices(request, response, routeData);
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

                    case "tablepicker":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveExternalTable(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "sdetablepicker":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveSdeTablePreview(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await RetrieveSdeTableValues(request, response, routeData);
                                break;

                            case RequestMethodEnum.Put:
                                await RetrieveSdeTable(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "tablepickerlist":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListExternalTables(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "supportemail":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await SendSupportEmail(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "staticassetssource":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListStaticAssetsSources(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await SetStaticAssetsSources(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "importexportsectionxml":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ExportSectionXml(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                            case RequestMethodEnum.Put:
                                await ImportSectionXml(request, response, routeData);
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

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "mappingservice":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Put:
                                await StoreMappingInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "mappingserviceclonemappingcluster":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await MappingServiceCloneCluster(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;

                    case "mappingservicesearchreplace":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await MappingServiceClusterSearchReplace(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }

                        break;


                    case "mappingservicefilterentries":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await FilterMappingEntries(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "mappingservicefiltertaxonomy":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await FilterTaxonomy(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "updateimagelibrary":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await UpdateImageLibaries(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "datatransformation":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await BulkTransformListScenarios(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await BulkTransformFilingContentData(request, response, routeData);
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
                                await AnalyzeEditorDataListScenarios(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await AnalyzeEditorData(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "taxxoreditorcontentdownload":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await DownloadCompleteProjectContent(request, response, routeData);
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
                                await RetrieveDataServiceTools(request, response, routeData);
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
                            case RequestMethodEnum.Post:
                                await SetFilingDataReportingRequirementMeta(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "websitegenerator":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await GenerateWebsite(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "quickpublishwebpage":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await QuickPublishWebPage(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "quickpublishwebpageallprojects":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await QuickPublishWebPageAllProjects(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "websitegeneratorexport":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ExportWebsite(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "websitegeneratoreditor":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderSingleHtmlSiteWebPageForEditor(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "findreplace":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await FindReplace(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "gitpull":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ListGitRepositories(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await GitPull(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "auditorviewcontent":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RenderAuditorViewStartContent(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await FilterAuditorViewContent();
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

                    case "structureddataelementvalue":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveStructuredDataElementValue(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;


                    case "syncstructureddatasnapshotdataref":
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

                    case "syncstructureddatasnapshotdatastart":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await SyncStructuredDataElementsStart(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;



                    case "auditorviewcomparisondownload":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                var pdfFileName = request.RetrievePostedValue("filename", RegexEnum.FileName, true, reqVars.returnType);
                                var pdfBinaryName = request.RetrievePostedValue("binaryname", RegexEnum.FileName, true, reqVars.returnType);
                                await DownloadFile(pdfFileName, "pdfsectioncompare", pdfBinaryName);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "listdatafiles":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await RetrieveCmsDataFiles(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "dateshiftsinglesetwrappers":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await CreateDynamicDateWrappersSingleDataFile(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "dateshiftsinglemigrate":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await ShiftDatesSingleDataFile(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "dateshiftsdeperiod":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Post:
                                await ShiftStructuredDataPeriodsSingleDataFile(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "filemanagerinfoapi":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await FileManagerStartupInformation(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "filemanagerapi":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await ImageManagerRetrieveImage(request, response, routeData);
                                break;

                            case RequestMethodEnum.Post:
                                await FileManagerOperations(request, response, routeData);
                                break;

                            default:
                                _handleMethodNotSupported(reqVars);
                                break;
                        }
                        break;

                    case "session":
                        switch (reqVars.method)
                        {
                            case RequestMethodEnum.Get:
                                await DisplaySessionInfo(request, response, routeData);
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