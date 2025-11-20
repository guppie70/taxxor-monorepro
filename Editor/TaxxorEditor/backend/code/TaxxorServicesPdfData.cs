using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor
{

    public partial class ConnectedServices
    {

        public static partial class DocumentStoreService
        {

            public static partial class PdfData
            {

                /// <summary>
                /// Loads the source html data for generating a PDF file
                /// </summary>
                /// <param name="projectVarsForPdfGeneration"></param>
                /// <param name="sections"></param>
                /// <param name="useContentStatus"></param>
                /// <param name="renderScope"></param>
                /// <param name="hideCurrentPeriodDatapoints"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> Load(ProjectVariables projectVarsForPdfGeneration, string sections, bool useContentStatus, string renderScope, bool hideCurrentPeriodDatapoints, bool debugRoutine)
                {
                    var context = System.Web.Context.Current;
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();
                    if (ValidateCmsPostedParameters(projectVarsForPdfGeneration.projectId, projectVarsForPdfGeneration.versionId, "text") && !string.IsNullOrEmpty(sections))
                    {
                        /*
                         * Call the Taxxor Document Store to retrieve the data
                         */

                        dataToPost.Add("pid", projectVarsForPdfGeneration.projectId);
                        dataToPost.Add("vid", projectVarsForPdfGeneration.versionId);
                        // Data types supported are "text", "config" - but since we need the content for the editor we will fix it to "text"
                        dataToPost.Add("type", "text");
                        dataToPost.Add("did", projectVarsForPdfGeneration.did ?? "");
                        dataToPost.Add("sections", sections);
                        dataToPost.Add("renderscope", renderScope);
                        dataToPost.Add("hidecurrentperioddatapoints", hideCurrentPeriodDatapoints.ToString().ToLower());
                        dataToPost.Add("ctype", projectVarsForPdfGeneration.editorContentType);
                        dataToPost.Add("rtype", projectVarsForPdfGeneration.reportTypeId);
                        dataToPost.Add("octype", projectVarsForPdfGeneration.outputChannelType);
                        dataToPost.Add("ocvariantid", projectVarsForPdfGeneration.outputChannelVariantId);
                        dataToPost.Add("oclang", projectVarsForPdfGeneration.outputChannelVariantLanguage);

                        XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorpdfdata", dataToPost, debugRoutine);

                        // Handle error
                        if (XmlContainsError(responseXml)) return responseXml;

                        // The XML content that we are looking for is in decoded form available in the message node
                        var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/message").InnerText);

                        try
                        {
                            var xmlSourceData = new XmlDocument();
                            xmlSourceData.LoadXml(xml);

                            //
                            // => Post process the data for content status
                            //
                            if (useContentStatus) Extensions.PostProcessPdfHtmlForContentStatus(ref xmlSourceData, projectVarsForPdfGeneration);

                            return xmlSourceData;
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Could not load the source data");
                            return GenerateErrorXml("Could not load the source data", $"error: {ex}, xml: {TruncateString(xml, 100)}, {_generateStandardDebugString(dataToPost)}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    else
                    {
                        return GenerateErrorXml("Did not supply enough input to load the source data", $"{_generateStandardDebugString(dataToPost)}, stack-trace: {GetStackTrace()}");
                    }

                }

                /// <summary>
                /// Retrieves historical XHTML data retrieved from an older commit or version in the version control repository
                /// </summary>
                /// <param name="projectVarsForPdfGeneration"></param>
                /// <param name="commitIdentifier">Tagname or hash of a commit</param>
                /// <param name="sections"></param>
                /// <param name="renderScope"></param>
                /// <param name="debugRoutine"></param>
                /// <returns></returns>
                public static async Task<XmlDocument> LoadHistorical(ProjectVariables projectVarsForPdfGeneration, string commitIdentifier, string sections, bool useContentStatus, string renderScope, bool debugRoutine = false)
                {
                    var context = System.Web.Context.Current;
                    ProjectVariables projectVars = RetrieveProjectVariables(context);

                    // The filing source data
                    var dataToPost = new Dictionary<string, string>();
                    if (ValidateCmsPostedParameters(projectVarsForPdfGeneration.projectId, projectVarsForPdfGeneration.versionId, "text") && !string.IsNullOrEmpty(sections))
                    {
                        /*
                         * Call the Taxxor Document Store to retrieve the data
                         */

                        dataToPost.Add("pid", projectVarsForPdfGeneration.projectId);
                        dataToPost.Add("vid", projectVarsForPdfGeneration.versionId);
                        // Data types supported are "text", "config" - but since we need the content for the editor we will fix it to "text"
                        dataToPost.Add("type", "text");
                        dataToPost.Add("did", projectVarsForPdfGeneration.did ?? "");
                        dataToPost.Add("sections", sections);
                        dataToPost.Add("renderscope", renderScope);
                        dataToPost.Add("ctype", projectVarsForPdfGeneration.editorContentType);
                        dataToPost.Add("rtype", projectVarsForPdfGeneration.reportTypeId);
                        dataToPost.Add("octype", projectVarsForPdfGeneration.outputChannelType);
                        dataToPost.Add("ocvariantid", projectVarsForPdfGeneration.outputChannelVariantId);
                        dataToPost.Add("oclang", projectVarsForPdfGeneration.outputChannelVariantLanguage);
                        dataToPost.Add("commithash", commitIdentifier);

                        XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Get, "taxxoreditorpdfhistoricaldata", dataToPost, debugRoutine);

                        // Handle error
                        if (XmlContainsError(responseXml)) return responseXml;

                        // The XML content that we are looking for is in decoded form available in the message node
                        var xml = HttpUtility.HtmlDecode(responseXml.SelectSingleNode("/result/message").InnerText);

                        try
                        {
                            var xmlSourceData = new XmlDocument();
                            xmlSourceData.LoadXml(xml);

                            //
                            // => Post process the data for content status
                            //
                            if (useContentStatus) Extensions.PostProcessPdfHtmlForContentStatus(ref xmlSourceData, projectVarsForPdfGeneration);

                            return xmlSourceData;
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Could not load the source data");
                            return GenerateErrorXml("Could not load the source data", $"error: {ex}, xml: {TruncateString(xml, 100)}, {_generateStandardDebugString(dataToPost)}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    else
                    {
                        return GenerateErrorXml("Did not supply enough input to load the source data", $"{_generateStandardDebugString(dataToPost)}, stack-trace: {GetStackTrace()}");
                    }

                }


            }

        }
    }
}