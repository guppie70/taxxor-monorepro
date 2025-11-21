using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using DocumentStore.Protos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor
{

    public partial class ConnectedServices
    {

        public static partial class DocumentStoreService
        {

            /// <summary>
            /// Interfaces with the Taxxor Document Store for filing data
            /// </summary>
            public static partial class FilingData
            {

                /// <summary>
                /// Loads the source XML/XHTML data of the filing from the server
                /// </summary>
                /// <returns>The source data.</returns>
                /// <param name="projectId">Project identifier.</param>
                /// <param name="did">Did.</param>
                /// <param name="versionId">Version identifier.</param>
                /// <param name="debugRoutine">If set to <c>true</c> debug routine.</param>
                public static async Task<XmlDocument> LoadSourceData(string projectId, string did, string versionId = "latest", bool debugRoutine = false)
                {
                    return await LoadSourceData(RetrieveProjectVariables(System.Web.Context.Current), projectId, did, versionId, debugRoutine);
                }

                /// <summary>
                /// Loads the source XML/XHTML data of the filing from the server
                /// </summary>
                /// <param name="projectVars"></param>
                /// <param name="projectId"></param>
                /// <param name="did"></param>
                /// <param name="versionId"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns> 
                public static async Task<XmlDocument> LoadSourceData(ProjectVariables projectVars, string projectId, string did, string versionId = "latest", bool debugRoutine = false)
                {
                    try
                    {
                        // Get the gRPC client service
                        FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient = System.Web.Context.Current.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();
                        return await LoadSourceData(filingComposerDataClient, projectVars, projectId, did, versionId, debugRoutine);
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error occurred while loading the source data: {ex.Message}";
                        appLogger.LogError(ex, errorMessage);
                        return GenerateErrorXml(errorMessage, $"stack-trace: {GetStackTrace()}");
                    }

                }

                /// <summary>
                /// Loads the source XML/XHTML data of the filing from the server
                /// </summary>
                /// <param name="filingComposerDataClient"></param>
                /// <param name="projectVars"></param>
                /// <param name="projectId"></param>
                /// <param name="did"></param>
                /// <param name="versionId"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> LoadSourceData(
                    FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient,
                    ProjectVariables projectVars,
                    string projectId,
                    string did,
                    string versionId = "latest",
                    bool debugRoutine = false)
                {
                    if (ValidateCmsPostedParameters(projectId, versionId, "text") && did != null)
                    {
                        // Create the request for the gRPC service
                        var grpcRequest = new GetFilingComposerDataRequest
                        {
                            DataType = "text",
                            Did = did,
                            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars)
                        };

                        // Call the gRPC service using the provided client
                        var grpcResponse = await filingComposerDataClient.GetFilingComposerDataAsync(grpcRequest);

                        if (grpcResponse.Success)
                        {
                            try
                            {
                                var xmlSourceData = new XmlDocument();
                                xmlSourceData.LoadXml(grpcResponse.Data);
                                return xmlSourceData;
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, "Could not load the source data");
                                return GenerateErrorXml("Could not load the source data", $"error: {ex}, xml: {TruncateString(grpcResponse.Data, 100)}, {_generateStandardDebugString(grpcRequest)}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Could not load the source data (message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo}, stack-trace: {GetStackTrace()}");
                            return GenerateErrorXml("Could not load the source data", $"message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo}, xml: {TruncateString(grpcResponse.Data, 100)}, {_generateStandardDebugString(grpcRequest)}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    else
                    {
                        return GenerateErrorXml("Did not supply enough input to load the source data", $"stack-trace: {GetStackTrace()}");
                    }
                }




                /// <summary>
                /// Loads the full (unprocessed) source data file from the Taxxor Data Store
                /// </summary>
                /// <returns>The complete source data.</returns>
                /// <param name="projectId">Project identifier.</param>
                /// <param name="versionId">Version identifier.</param>
                /// <param name="debugRoutine">If set to <c>true</c> debug routine.</param>
                public static async Task<XmlDocument> LoadCompleteSourceData(string projectId, string versionId = "latest", bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    if (ValidateCmsPostedParameters(projectId, versionId, "text"))
                    {
                        /*
                         * Call the Taxxor Document Store to retrieve the data
                         */
                        // Simply retrieve the complete documemnt from the Taxxor Data Store
                        var nodeSourceDataLocation = xmlApplicationConfiguration.SelectSingleNode("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/content_types/content_management/type[@id=" + GenerateEscapedXPathString(projectVars.editorContentType) + "]/xml");
                        if (nodeSourceDataLocation == null) return GenerateErrorXml("Could not locate the node pointing to the filing source data", $"projectId: {projectId}, versionId: {versionId}, stack-trace: {GetStackTrace()}");
                        return await Load<XmlDocument>(nodeSourceDataLocation, debugRoutine);

                    }
                    else
                    {
                        return GenerateErrorXml("Did not supply enough input to load the source data", $"projectId: {projectId}, versionId: {versionId}, stack-trace: {GetStackTrace()}");
                    }

                }

                /// <summary>
                /// Saves the filing editor source data on the Taxxor Document Store
                /// </summary>
                /// <returns>The source data.</returns>
                /// <param name="xmlDoc">Xml document.</param>
                /// <param name="projectId">Project identifier.</param>
                /// <param name="versionId">Version identifier.</param>
                /// <param name="dataType">Data type.</param>
                /// <param name="id">Identifier.</param>
                /// <param name="contentLanguage">Content language.</param>
                /// <param name="debugRoutine">If set to <c>true</c> debug routine.</param>
                public static async Task<XmlDocument> SaveSourceData(XmlDocument xmlDoc, string id, string contentLanguage, bool debugRoutine = false)
                {
                    try
                    {
                        var context = System.Web.Context.Current;
                        ProjectVariables projectVars = RetrieveProjectVariables(context);

                        if (ValidateCmsPostedParameters(projectVars.projectId, "latest", "text") && id != null)
                        {
                            // Get the gRPC client service
                            FilingComposerDataService.FilingComposerDataServiceClient filingDataClient = context.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                            // Create the gRPC request
                            var grpcRequest = new SaveSourceDataRequest
                            {
                                GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
                                Data = xmlDoc.OuterXml,
                                Did = id,
                                ContentLanguage = contentLanguage
                            };

                            // Call the gRPC service
                            var grpcResponse = await filingDataClient.SaveSourceDataAsync(grpcRequest);

                            if (grpcResponse.Success)
                            {
                                try
                                {
                                    var xmlResult = new XmlDocument();
                                    xmlResult.LoadXml(grpcResponse.Data);
                                    return xmlResult;
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, "Could not parse SaveSourceData response");
                                    return GenerateErrorXml("Could not parse the response data", $"error: {ex}, xml: {TruncateString(grpcResponse.Data, 100)}, stack-trace: {GetStackTrace()}");
                                }
                            }
                            else
                            {
                                appLogger.LogError($"Could not save the source data (message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo})");
                                return GenerateErrorXml("Could not save the source data", $"message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            return GenerateErrorXml("Did not supply enough input to save your source data", $"projectId: {projectVars.projectId}, did: {id}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error occurred while saving the source data: {ex.Message}";
                        appLogger.LogError(ex, errorMessage);
                        return GenerateErrorXml(errorMessage, $"stack-trace: {GetStackTrace()}");
                    }
                }

                /// <summary>
                /// Deletes a filing section source data fragment from the Taxxor Data Store based on the information passed in the XML document
                /// </summary>
                /// <param name="xmlDeleteActions">Includes information about the section itself, but also about the output channels where it is referenced</param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> DeleteSourceData(XmlDocument xmlDeleteActions, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    ProjectVariables projectVars = RetrieveProjectVariables(context);
                    return await DeleteSourceData(xmlDeleteActions, projectVars.projectId, projectVars.versionId, "text", projectVars.outputChannelDefaultLanguage, debugRoutine);
                }

                /// <summary>
                /// Deletes a filing section source data fragment from the Taxxor Data Store based on the information passed in the XML document
                /// </summary>
                /// <param name="xmlDeleteActions"></param>
                /// <param name="projectId"></param>
                /// <param name="versionId"></param>
                /// <param name="dataType"></param>
                /// <param name="contentLanguage"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> DeleteSourceData(XmlDocument xmlDeleteActions, string projectId, string versionId, string dataType, string contentLanguage, bool debugRoutine = false)
                {
                    try
                    {
                        var context = System.Web.Context.Current;
                        ProjectVariables projectVars = RetrieveProjectVariables(context);

                        if (ValidateCmsPostedParameters(projectId, versionId, dataType))
                        {
                            // Get the gRPC client service
                            FilingComposerDataService.FilingComposerDataServiceClient filingDataClient = context.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                            // Create the gRPC request
                            var grpcRequest = new DeleteSourceDataRequest
                            {
                                GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
                                XmlDeleteActions = xmlDeleteActions.OuterXml,
                                DataType = dataType
                            };

                            // Call the gRPC service
                            var grpcResponse = await filingDataClient.DeleteSourceDataAsync(grpcRequest);

                            if (grpcResponse.Success)
                            {
                                try
                                {
                                    var xmlResult = new XmlDocument();
                                    xmlResult.LoadXml(grpcResponse.Data);
                                    return xmlResult;
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, "Could not parse DeleteSourceData response");
                                    return GenerateErrorXml("Could not parse the response data", $"error: {ex}, xml: {TruncateString(grpcResponse.Data, 100)}, stack-trace: {GetStackTrace()}");
                                }
                            }
                            else
                            {
                                appLogger.LogError($"Could not delete the source data (message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo})");
                                return GenerateErrorXml("Could not delete the source data", $"message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            return GenerateErrorXml("Did not supply enough input to delete your source data", $"projectId: {projectId}, versionId: {versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error occurred while deleting the source data: {ex.Message}";
                        appLogger.LogError(ex, errorMessage);
                        return GenerateErrorXml(errorMessage, $"stack-trace: {GetStackTrace()}");
                    }

                }

                /// <summary>
                /// Creates a new source data section for a filing document
                /// </summary>
                /// <param name="sectionId"></param>
                /// <param name="xmlDoc"></param>
                /// <param name="debugRoutine"></param>
                /// <returns>An xml object containing an overview of all the filing sections available</returns>
                public static async Task<XmlDocument> CreateSourceData(string sectionId, XmlDocument xmlDoc, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    ProjectVariables projectVars = RetrieveProjectVariables(context);
                    return await CreateSourceData(sectionId, xmlDoc, projectVars.projectId, projectVars.versionId, "text", projectVars.outputChannelDefaultLanguage, debugRoutine);
                }

                /// <summary>
                /// Creates a new source data section for a filing document
                /// </summary>
                /// <param name="sectionId"></param>
                /// <param name="xmlDoc"></param>
                /// <param name="projectId"></param>
                /// <param name="versionId"></param>
                /// <param name="dataType"></param>
                /// <param name="contentLanguage"></param>
                /// <param name="debugRoutine"></param>
                /// <returns>An xml object containing an overview of all the filing sections available</returns>
                public static async Task<XmlDocument> CreateSourceData(string sectionId, XmlDocument xmlDoc, string projectId, string versionId, string dataType, string contentLanguage, bool debugRoutine = false)
                {
                    try
                    {
                        var context = System.Web.Context.Current;
                        ProjectVariables projectVars = RetrieveProjectVariables(context);

                        if (ValidateCmsPostedParameters(projectId, versionId, dataType))
                        {
                            // Get the gRPC client service
                            FilingComposerDataService.FilingComposerDataServiceClient filingDataClient = context.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                            // Create the gRPC request
                            var grpcRequest = new CreateSourceDataRequest
                            {
                                GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
                                SectionId = sectionId,
                                Data = xmlDoc.OuterXml,
                                DataType = dataType
                            };

                            // Call the gRPC service
                            var grpcResponse = await filingDataClient.CreateSourceDataAsync(grpcRequest);

                            if (grpcResponse.Success)
                            {
                                try
                                {
                                    var xmlSourceDataOverview = new XmlDocument();
                                    xmlSourceDataOverview.LoadXml(grpcResponse.Data);
                                    return xmlSourceDataOverview;
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, "Could not parse CreateSourceData response");
                                    return GenerateErrorXml("Could not parse the response data", $"error: {ex}, xml: {TruncateString(grpcResponse.Data, 100)}, stack-trace: {GetStackTrace()}");
                                }
                            }
                            else
                            {
                                appLogger.LogError($"Could not create the source data (message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo})");
                                return GenerateErrorXml("Could not create the source data", $"message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            return GenerateErrorXml("Did not supply enough input to save your source data", $"sectionId: {sectionId}, projectId: {projectId}, versionId: {versionId}, dataType: {dataType}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error occurred while creating the source data: {ex.Message}";
                        appLogger.LogError(ex, errorMessage);
                        return GenerateErrorXml(errorMessage, $"stack-trace: {GetStackTrace()}");
                    }
                }

                /// <summary>
                /// Renders an overview of all the available section XHTML files if this specific project
                /// </summary>
                /// <param name="projectVars"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> SourceDataOverview(ProjectVariables projectVars, bool debugRoutine = false)
                {
                    try
                    {
                        var context = System.Web.Context.Current;

                        // Get the gRPC client service via DI
                        var client = context.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                        // Create the request
                        var grpcRequest = new GetSourceDataOverviewRequest
                        {
                            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars)
                        };

                        // Call the gRPC service
                        var grpcResponse = await client.GetSourceDataOverviewAsync(grpcRequest);

                        if (grpcResponse.Success)
                        {
                            // Parse the XML data from the response
                            var xmlSourceDataOverview = new XmlDocument();
                            xmlSourceDataOverview.LoadXml(grpcResponse.Data);
                            return xmlSourceDataOverview;
                        }
                        else
                        {
                            // Handle error
                            return GenerateErrorXml(grpcResponse.Message, $"debuginfo: {grpcResponse.Debuginfo}");
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Error in SourceDataOverview");
                        return GenerateErrorXml($"Error in SourceDataOverview: {ex.Message}", $"stack-trace: {GetStackTrace()}");
                    }
                }

                /// <summary>
                /// Clone the content of one language to another in the same data reference
                /// </summary>
                /// <param name="projectVars"></param>
                /// <param name="sourceLang"></param>
                /// <param name="targetLang"></param>
                /// <param name="includeChildren"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> CloneSectionLanguageData(ProjectVariables projectVars, string sourceLang, string targetLang, bool includeChildren, bool debugRoutine = false)
                {
                    try
                    {
                        var context = System.Web.Context.Current;

                        if (!ValidateCmsPostedParameters(projectVars.projectId, "latest", "text") || projectVars.did == null)
                        {
                            return GenerateErrorXml("Did not supply enough input to clone section language data", $"stack-trace: {GetStackTrace()}");
                        }

                        // Get the gRPC client service via DI
                        var client = context.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                        // Create the request
                        var grpcRequest = new CloneSectionLanguageDataRequest
                        {
                            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
                            SourceLang = sourceLang,
                            TargetLang = targetLang,
                            IncludeChildren = includeChildren
                        };

                        // Call the gRPC service
                        var grpcResponse = await client.CloneSectionLanguageDataAsync(grpcRequest);

                        if (grpcResponse.Success)
                        {
                            // Return success XML
                            return GenerateSuccessXml(grpcResponse.Message, grpcResponse.Debuginfo);
                        }
                        else
                        {
                            // Handle error
                            return GenerateErrorXml(grpcResponse.Message, $"debuginfo: {grpcResponse.Debuginfo}");
                        }
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Error in CloneSectionLanguageData");
                        return GenerateErrorXml($"Error in CloneSectionLanguageData: {ex.Message}", $"stack-trace: {GetStackTrace()}");
                    }
                }

                /// <summary>
                /// Loads the filing hierarchy from the Taxxor Data Store.
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="outputChannelVariantId"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> LoadHierarchy(string projectId, string outputChannelVariantId, bool debugRoutine = false)
                {
                    var editorId = RetrieveEditorIdFromProjectId(projectId);
                    var outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectId, outputChannelVariantId);
                    var outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectId, outputChannelVariantId);

                    return await LoadHierarchy(projectId, "latest", editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage, debugRoutine);
                }

                /// <summary>
                /// Loads the hierarchy of the filing hierarchy from the Taxxor Data Store.
                /// </summary>
                /// <returns>The hierarchy.</returns>
                /// <param name="projectVars">Project variables.</param>
                /// <param name="debugRoutine">If set to <c>true</c> debug routine.</param>
                public static async Task<XmlDocument> LoadHierarchy(ProjectVariables projectVars, bool debugRoutine = false)
                {
                    return await LoadHierarchy(projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage, debugRoutine);
                }

                /// <summary>
                /// Loads the hierarchy of the filing hierarchy from the Taxxor Data Store.
                /// </summary>
                /// <returns>The hierarchy.</returns>
                /// <param name="projectId">Project identifier.</param>
                /// <param name="versionId">Version identifier.</param>
                /// <param name="editorId">Editor identifier.</param>
                /// <param name="outputChannelType">Output channel type.</param>
                /// <param name="outputChannelVariantId">Output channel variant identifier.</param>
                /// <param name="outputChannelVariantLanguage">Output channel variant language.</param>
                /// <param name="debugRoutine">If set to <c>true</c> debug routine.</param>
                public static async Task<XmlDocument> LoadHierarchy(string projectId, string versionId, string editorId, string outputChannelType, string outputChannelVariantId, string outputChannelVariantLanguage, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    try
                    {
                        // Get the gRPC client service via DI
                        var client = context.RequestServices.GetRequiredService<FilingHierarchyService.FilingHierarchyServiceClient>();

                        // Populate projectVars with the provided parameters
                        projectVars.projectId = projectId;
                        projectVars.versionId = versionId;
                        projectVars.editorId = editorId;
                        projectVars.outputChannelType = outputChannelType;
                        projectVars.outputChannelVariantId = outputChannelVariantId;
                        projectVars.outputChannelVariantLanguage = outputChannelVariantLanguage;

                        // Set content type and report type
                        if (string.IsNullOrEmpty(projectVars.editorContentType))
                            projectVars.editorContentType = "regular";
                        if (string.IsNullOrEmpty(projectVars.reportTypeId))
                            projectVars.reportTypeId = RetrieveReportTypeIdFromProjectId(projectId);

                        // Create the gRPC request
                        var grpcRequest = new LoadHierarchyRequest
                        {
                            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars)
                        };

                        // Call the gRPC service
                        var grpcResponse = await client.LoadHierarchyAsync(grpcRequest);

                        if (grpcResponse.Success)
                        {
                            // The XML content is HTML-encoded in the data field
                            var xml = HttpUtility.HtmlDecode(grpcResponse.Data);

                            var xmlHierarchyData = new XmlDocument();
                            xmlHierarchyData.LoadXml(xml);
                            return xmlHierarchyData;
                        }
                        else
                        {
                            return GenerateErrorXml($"Could not load the hierarchy data: {grpcResponse.Message}",
                                $"debuginfo: {grpcResponse.Debuginfo}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml($"Error in LoadHierarchy: {ex.Message}", $"stack-trace: {GetStackTrace()}");
                    }
                }

                /// <summary>
                /// Stores a filing hierarchy in the Taxxor Data Store
                /// </summary>
                /// <param name="hierarchy"></param>
                /// <param name="projectId"></param>
                /// <param name="versionId"></param>
                /// <param name="editorId"></param>
                /// <param name="outputChannelType"></param>
                /// <param name="outputChannelVariantId"></param>
                /// <param name="outputChannelVariantLanguage"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> SaveHierarchy(XmlDocument hierarchy, string projectId, string versionId, string editorId, string outputChannelType, string outputChannelVariantId, string outputChannelVariantLanguage, bool commitChanges = true, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    try
                    {
                        // Get the gRPC client service via DI
                        var client = context.RequestServices.GetRequiredService<FilingHierarchyService.FilingHierarchyServiceClient>();

                        // Populate projectVars with the provided parameters
                        projectVars.projectId = projectId;
                        projectVars.versionId = versionId;
                        projectVars.editorId = editorId;
                        projectVars.outputChannelType = outputChannelType;
                        projectVars.outputChannelVariantId = outputChannelVariantId;
                        projectVars.outputChannelVariantLanguage = outputChannelVariantLanguage;

                        // Create the gRPC request
                        var grpcRequest = new SaveHierarchyRequest
                        {
                            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
                            Hierarchy = hierarchy.OuterXml,
                            CommitChanges = commitChanges
                        };

                        // Call the gRPC service
                        var grpcResponse = await client.SaveHierarchyAsync(grpcRequest);

                        if (grpcResponse.Success)
                        {
                            // Return success XML
                            var xmlResponse = new XmlDocument();
                            xmlResponse.LoadXml($"<result><success>true</success><message>{grpcResponse.Message}</message></result>");
                            return xmlResponse;
                        }
                        else
                        {
                            return GenerateErrorXml($"Could not save the hierarchy: {grpcResponse.Message}",
                                $"debuginfo: {grpcResponse.Debuginfo}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        return GenerateErrorXml($"Error in SaveHierarchy: {ex.Message}", $"stack-trace: {GetStackTrace()}");
                    }
                }


                /// <summary>
                /// Finds and replaces text in all the data files of a project
                /// </summary>
                /// <param name="searchFragment"></param>
                /// <param name="replaceFragment"></param>
                /// <param name="includeFootnotes"></param>
                /// <param name="dryRun"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> FindReplace(string searchFragment, string replaceFragment, bool onlyInUse = true, bool includeFootnotes = true, bool dryRun = false, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);
                    var dataToPost = new Dictionary<string, string>();

                    if (ValidateCmsPostedParameters(projectVars.projectId, "latest", "text"))
                    {
                        dataToPost.Add("searchfragment", searchFragment);
                        dataToPost.Add("replacefragment", replaceFragment);
                        dataToPost.Add("onlyinuse", onlyInUse.ToString().ToLower());
                        dataToPost.Add("includefootnotes", includeFootnotes.ToString().ToLower());
                        dataToPost.Add("dryrun", dryRun.ToString().ToLower());


                        dataToPost.Add("vid", "latest");
                        dataToPost.Add("type", "text");

                        dataToPost.Add("pid", projectVars.projectId);
                        dataToPost.Add("octype", projectVars.outputChannelType);
                        dataToPost.Add("ocvariantid", projectVars.outputChannelVariantId);
                        dataToPost.Add("oclang", projectVars.outputChannelVariantLanguage);
                        dataToPost.Add("ctype", projectVars.editorContentType);
                        dataToPost.Add("rtype", projectVars.reportTypeId);

                        return await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "findreplace", dataToPost, debugRoutine);
                    }
                    else
                    {
                        return GenerateErrorXml("Did not supply enough input to search and replace text", $"projectId: {projectVars.projectId}, searchFragment: {searchFragment}, replaceFragment: {replaceFragment}, stack-trace: {GetStackTrace()}");
                    }
                }

                /// <summary>
                /// Clears the (memory) cache on the Document Store service
                /// </summary>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> ClearCache(bool debugRoutine)
                {
                    return await CallTaxxorDataService<TaxxorReturnMessage>(RequestMethodEnum.Delete, "clearcache", debugRoutine);
                }

                /// <summary>
                /// Clears the (memory) cache on the Document Store service
                /// </summary>
                /// <param name="projectVars"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> ClearCache(ProjectVariables projectVars, bool debugRoutine)
                {
                    // Convert the Dictionary to a List containing KeyValuePair objects
                    var requestKeyValueData = new List<KeyValuePair<string, string>>();
                    var requestEncodedData = new FormUrlEncodedContent(requestKeyValueData);

                    return await CallTaxxorConnectedService<TaxxorReturnMessage>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Delete, "clearcache", requestEncodedData, debugRoutine, false, projectVars);
                }

            }

            /// <summary>
            /// Interfaces with the Document Store service for the Generated Reports repository
            /// </summary>
            public static partial class GeneratedReportsRepository
            {
                /// <summary>
                /// Adds a new generated report to the repository
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="path"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> Add(string path, string reportRequirementScheme, Dictionary<string, string> xbrlValidationInformation, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();

                    dataToPost.Add("pid", projectVars.projectId);
                    dataToPost.Add("user", projectVars.currentUser.Id);
                    dataToPost.Add("path", path);
                    dataToPost.Add("scheme", reportRequirementScheme);

                    // Convert the xbrl validation information to an XML string that we can easily inject in the repository information in the Document Store
                    var xmlXbrlProperties = new XmlDocument();
                    var nodeValidationLinksRoot = xmlXbrlProperties.CreateElement("validationinformation");
                    foreach (var item in xbrlValidationInformation)
                    {
                        var nodeLink = xmlXbrlProperties.CreateElementWithText("item", item.Value);
                        nodeLink.SetAttribute("id", item.Key);
                        nodeValidationLinksRoot.AppendChild(nodeLink);
                    }
                    xmlXbrlProperties.AppendChild(nodeValidationLinksRoot);
                    dataToPost.Add("xmlvalidationinformation", xmlXbrlProperties.OuterXml);

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Put, "generatedreportsrepository", dataToPost, debugRoutine);

                    if (debugRoutine)
                    {
                        // appLogger.LogInformation($"GeneratedReportsRepository.Add() - {PrettyPrintXml(responseXml)}");
                    }

                    return new TaxxorReturnMessage(responseXml);
                }

                /// <summary>
                /// Retrieves repository content
                /// </summary>
                /// <param name="filterScheme"></param>
                /// <param name="filterUser"></param>
                /// <param name="filterGuid"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> RetrieveContent(string filterScheme = null, string filterUser = null, string filterGuid = null, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();

                    dataToPost.Add("pid", projectVars.projectId);
                    dataToPost.Add("filterscheme", filterScheme);
                    dataToPost.Add("filteruser", filterUser);
                    dataToPost.Add("filterguid", filterGuid);

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "generatedreportsrepository", dataToPost, debugRoutine);

                    if (debugRoutine)
                    {
                        // appLogger.LogInformation($"GeneratedReportsRepository.RetrieveContent() - {PrettyPrintXml(responseXml)}");
                    }

                    var result = new TaxxorReturnMessage(responseXml);

                    return result;
                }

            }


            /// <summary>
            /// Interfaces with the Taxxor Document Store for (GIT) version control operations
            /// </summary>
            public static partial class VersionControl
            {

                /// <summary>
                /// Retrieves the difference between two commits
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="locationId"></param>
                /// <param name="baseCommitHash"></param>
                /// <param name="latestCommitHash"></param>
                /// <param name="gitFilePath"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> GitDiffBetweenCommits(string projectId, string locationId, string baseCommitHash, string latestCommitHash, string gitFilePath = null, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();

                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("basecommithash", baseCommitHash);
                    dataToPost.Add("latestcommithash", latestCommitHash);
                    dataToPost.Add("locationid", locationId);
                    if (!string.IsNullOrEmpty(gitFilePath)) dataToPost.Add("gitfilepath", gitFilePath);

                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitdiff", dataToPost, debugRoutine);

                    if (debugRoutine)
                    {
                        appLogger.LogInformation($"GitDiffBetweenCommits() - {PrettyPrintXml(responseXml)}");
                    }

                    return new TaxxorReturnMessage(responseXml);
                }


                /// <summary>
                /// Extracts a single file from a GIT commit
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="locationId"></param>
                /// <param name="commitHash"></param>
                /// <param name="sourceFilePath"></param>
                /// <param name="extractLocationFolderPath"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> GitExtractSingleFile(string projectId, string locationId, string commitHash, string sourceFilePath, string extractLocationFolderPath, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>
                    {
                        { "pid", projectId },
                        { "commithash", commitHash },
                        { "locationid", locationId },
                        { "sourcefilepath", sourceFilePath },
                        { "extractfolderpath", extractLocationFolderPath }
                    };


                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitextractsingle", dataToPost, debugRoutine);

                    // if (debugRoutine)
                    // {
                    //     appLogger.LogInformation($"GitExtractSingleFile() - {PrettyPrintXml(responseXml)}");
                    // }

                    return new TaxxorReturnMessage(responseXml);
                }

                /// <summary>
                /// Extracts all files from a git commit
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="locationId"></param>
                /// <param name="commitHash"></param>
                /// <param name="extractLocationFolderPathOs"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> GitExtractAll(string projectId, string locationId, string commitHash, string extractLocationFolderPathOs, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();

                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("commithash", commitHash);
                    dataToPost.Add("locationid", locationId);
                    dataToPost.Add("extractfolderpathos", extractLocationFolderPathOs);


                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitextractall", dataToPost, debugRoutine);

                    // if (debugRoutine)
                    // {
                    //     appLogger.LogInformation($"GitExtractAll() - {PrettyPrintXml(responseXml)}");
                    // }

                    return new TaxxorReturnMessage(responseXml);
                }

                /// <summary>
                /// Adds a commit in the versioning system
                /// </summary>
                /// <param name="projectId"></param>
                /// <param name="locationId"></param>
                /// <param name="message"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<TaxxorReturnMessage> GitCommit(string projectId, string locationId, string message, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    RequestVariables reqVars = RetrieveRequestVariables(context);
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();

                    dataToPost.Add("pid", projectId);
                    dataToPost.Add("locationid", locationId);
                    dataToPost.Add("message", message);


                    XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "gitcommit", dataToPost, debugRoutine);

                    // if (debugRoutine)
                    // {
                    //     appLogger.LogInformation($"GitCommit() - {PrettyPrintXml(responseXml)}");
                    // }

                    return new TaxxorReturnMessage(responseXml);
                }
            }
        }
    }
}