using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Copies data for the website on a shared location so that it can be used to render the website with
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ExportWebsiteContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            await DummyAwaiter();

            try
            {
                // Retrieve posted values
                var tempFolderName = request.RetrievePostedValue("tempfoldername", RegexEnum.RelativeUri, true, ReturnTypeEnum.Xml);
                var htmlWebsiteAssetsFolderPath = request.RetrievePostedValue("htmlassets", RegexEnum.RelativeUri, true, ReturnTypeEnum.Xml);
                var imagesWebsiteAssetsFolderPath = request.RetrievePostedValue("imageassets", RegexEnum.RelativeUri, true, ReturnTypeEnum.Xml);
                var downloadsWebsiteAssetsFolderPath = request.RetrievePostedValue("downloadassets", RegexEnum.RelativeUri, true, ReturnTypeEnum.Xml);

                var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";
                var htmlWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{htmlWebsiteAssetsFolderPath}".Replace("[language]", projectVars.outputChannelVariantLanguage);
                var imagesWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{imagesWebsiteAssetsFolderPath}".Replace("[language]", projectVars.outputChannelVariantLanguage);
                var downloadsWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{downloadsWebsiteAssetsFolderPath}".Replace("[language]", projectVars.outputChannelVariantLanguage);

                if (!Directory.Exists(tempFolderPathOs))
                {
                    HandleError("Could not find folder to export the website assets to", $"tempFolderPathOs: {tempFolderPathOs}, {GetStackTrace()}");
                }

                // A) Website XHTML files
                var xmlFilingHierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, projectVars.outputChannelType, projectVars.outputChannelVariantId, projectVars.outputChannelVariantLanguage);
                var xmlFilingHierarchy = new XmlDocument();
                xmlFilingHierarchy.Load(xmlFilingHierarchyPathOs);

                if (debugRoutine)
                {
                    Console.WriteLine("==============================");
                    Console.WriteLine(PrettyPrintXml(xmlFilingHierarchy));
                    Console.WriteLine("==============================");
                }


                // Calculate the path to the folder that contains the data files
                var xmlSectionFolderPathOs = RetrieveInlineFilingComposerXmlRootFolderPathOs(projectVars.projectId);

                var nodeListSourceDataFiles = xmlFilingHierarchy.SelectNodes("//item");
                foreach (XmlNode nodeItem in nodeListSourceDataFiles)
                {
                    var dataRef = GetAttribute(nodeItem, "data-ref");
                    if (dataRef != null)
                    {
                        var sourceDataPathOs = $"{xmlSectionFolderPathOs}/{dataRef}";
                        if (File.Exists(sourceDataPathOs))
                        {
                            // Re-use the composer content rerieve function to resolve stuff
                            var xmlSectionDataContent = new XmlDocument();
                            try
                            {
                                xmlSectionDataContent = LoadAndResolveInlineFilingComposerData(reqVars, projectVars, sourceDataPathOs);

                                xmlSectionDataContent.Save($"{htmlWebsiteAssetsFolderPathOs}/{dataRef}");
                            }
                            catch (Exception ex)
                            {
                                HandleError(ReturnTypeEnum.Xml, "There was an error converting website section content", $"error: {ex}, stack-trace: {GetStackTrace()}");
                            }
                        }
                        else
                        {
                            appLogger.LogError($"Could not find source data file to copysourceDataPathOs: {sourceDataPathOs}");
                            HandleError("Could not find source data file to copy", $"sourceDataPathOs: {sourceDataPathOs}, stack-trace: {GetStackTrace()}");
                        }
                    }
                }

                // B) Image binaries
                CopyDirectoryRecursive($"{projectVars.cmsContentRootPathOs}/images", imagesWebsiteAssetsFolderPathOs, true);

                // C) Download binaries
                // => The files in the root directory first
                var downloadsTargetRootFolderOs = Path.GetDirectoryName(downloadsWebsiteAssetsFolderPathOs);
                var downloadFiles = Directory.EnumerateFiles($"{projectVars.cmsContentRootPathOs}/downloads", "*.*", SearchOption.TopDirectoryOnly);
                foreach (string sourceDownloadFilePathOs in downloadFiles)
                {
                    var targetDownloadFilePathOs = $"{downloadsTargetRootFolderOs}/{Path.GetFileName(sourceDownloadFilePathOs)}";
                    // appLogger.LogCritical($"- sourceDownloadFilePathOs: {sourceDownloadFilePathOs}");
                    // appLogger.LogCritical($"- targetDownloadFilePathOs: {targetDownloadFilePathOs}");
                    File.Copy(sourceDownloadFilePathOs, targetDownloadFilePathOs);
                }


                // => Then all the files in the language specific directory
                var sourceLangDownloadsFolderPathOs = $"{projectVars.cmsContentRootPathOs}/downloads/{projectVars.outputChannelVariantLanguage}";
                var targetLangDownloadsFolderPathOs = $"{downloadsWebsiteAssetsFolderPathOs}";
                // appLogger.LogCritical($"- sourceLangDownloadsFolderPathOs: {sourceLangDownloadsFolderPathOs}");
                // appLogger.LogCritical($"- targetLangDownloadsFolderPathOs: {targetLangDownloadsFolderPathOs}");
                if (!Directory.Exists(sourceLangDownloadsFolderPathOs))
                {
                    try
                    {
                        Directory.CreateDirectory(sourceLangDownloadsFolderPathOs);
                    }
                    catch (Exception ex)
                    {
                        HandleError("Could not create downloads folder in the Taxxor Data Store", $"sourceLangDownloadsFolderPathOs: {sourceLangDownloadsFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                    }
                }
                CopyDirectoryRecursive(sourceLangDownloadsFolderPathOs, targetLangDownloadsFolderPathOs, true);

                // Write a response
                await response.OK(GenerateSuccessXml("Successfully copied website files", ""), reqVars.returnType, true);
            }
            // To make sure HandleError is correctly executed
            catch (HttpStatusCodeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HandleError(ReturnTypeEnum.Xml, "There was an error copying the website data", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
        }

    }
}