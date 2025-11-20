using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using DocumentStore.Protos;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {



        /// <summary>
        /// Performs a system wide check if all the section data used in PDF files is XHTML compliant
        /// </summary>
        /// <param name="processOnlyOpenProjects"></param>
        /// <returns></returns>
        public static async Task CheckAllXhtmlSections(FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient, bool processOnlyOpenProjects = true)
        {
            var testMode = false;
            var logResults = false;
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var xPathProjects = processOnlyOpenProjects ? "/configuration/cms_projects/cms_project[versions/version/status/text() = 'open']" : "/configuration/cms_projects/cms_project";
                var nodeListProjects = xmlApplicationConfiguration.SelectNodes(xPathProjects);
                if (nodeListProjects != null)
                {

                    foreach (XmlNode nodeCurrentProject in nodeListProjects)
                    {
                        var currentProjectId = GetAttribute(nodeCurrentProject, "id") ?? "";
                        // appLogger.LogInformation($"!!ecurrentProjectId: {currentProjectId}");
                        if (currentProjectId != "")
                        {

                            var currentEditorId = RetrieveEditorIdFromProjectId(currentProjectId);
                            string[] currentProjectLanguages = RetrieveProjectLanguages(currentEditorId);
                            var reportTypeId = RetrieveReportTypeIdFromProjectId(currentProjectId);
                            var outputChannelVariantId = RetrieveFirstOutputChannelVariantIdFromEditorId(currentEditorId);
                            var outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(currentProjectId, outputChannelVariantId);


                            var projectVarsForXhtmlCheck = new ProjectVariables
                            {
                                projectId = currentProjectId,
                                versionId = "latest",
                                editorContentType = "regular",
                                reportTypeId = RetrieveReportTypeIdFromProjectId(currentProjectId),
                                outputChannelType = "pdf",
                                editorId = currentEditorId,
                                outputChannelVariantId = outputChannelVariantId,
                                outputChannelVariantLanguage = outputChannelVariantLanguage?? "en"
                            };

                            // Find the data references that we should not include in the check
                            // /configuration/general/schemachecker/cms_project[@report-type='']/exclude/dataref
                            var nodeListDataReferencesToExclude = xmlApplicationConfiguration.SelectNodes($"/configuration/general/schemachecker/cms_project[@report-type='{reportTypeId}']/exclude/dataref");
                            var dataReferencesToExclude = new List<string>();
                            if (nodeListDataReferencesToExclude != null)
                            {
                                foreach (XmlNode nodeDataReferenceToExclude in nodeListDataReferencesToExclude)
                                {
                                    var dataRef = nodeDataReferenceToExclude.GetAttribute("name") ?? "";
                                    if (!string.IsNullOrEmpty(dataRef) && !dataReferencesToExclude.Contains(dataRef))
                                    {
                                        dataReferencesToExclude.Add(dataRef);
                                    }
                                }
                            }


                            // appLogger.LogCritical($"- dataReferencesToExclude: {string.Join(", ", dataReferencesToExclude)}");

                            // Find the section data files in use
                            var dataReferences = new Dictionary<string, string[]>();
                            var xPathForDataReferences = $"/projects/cms_project[@id='{currentProjectId}']/data/content[@datatype='sectiondata' and metadata/entry[@key='inuse']/text()='true']";

                            var nodeListContentDataFiles = XmlCmsContentMetadata.SelectNodes(xPathForDataReferences);
                            if (nodeListContentDataFiles != null)
                            {
                                foreach (XmlNode nodeContentDataFile in nodeListContentDataFiles)
                                {
                                    var dataRef = nodeContentDataFile.GetAttribute("ref");
                                    if (!string.IsNullOrEmpty(dataRef) && !dataReferencesToExclude.Contains(dataRef))
                                    {
                                        var languages = nodeContentDataFile.SelectSingleNode("metadata/entry[@key='languages']")?.InnerText ?? "";
                                        if (!dataReferences.ContainsKey(dataRef))
                                        {
                                            dataReferences.Add(dataRef, languages.Split(',')); // Add an empty value initially
                                        }
                                    }
                                    else
                                    {
                                        // appLogger.LogWarning($"Data reference {dataRef} was excluded from the xhtml check");
                                    }
                                }
                            }


                            // Loop through the currentProjectLanguages array
                            foreach (var currentProjectLanguage in currentProjectLanguages)
                            {
                                // Loop through the dataReferences list
                                foreach (var dataReference in dataReferences)
                                {
                                    var dataRef = dataReference.Key;
                                    var languages = dataReference.Value;

                                    // Only check the dataReference if currentProjectLanguage is in the languages array
                                    if (languages.Contains(currentProjectLanguage))
                                    {
                                        try
                                        {
                                            projectVarsForXhtmlCheck.outputChannelVariantLanguage = currentProjectLanguage;

                                            var xmlFilingContent = await Taxxor.ConnectedServices.DocumentStoreService.FilingData.LoadSourceData(filingComposerDataClient, projectVarsForXhtmlCheck, projectVarsForXhtmlCheck.projectId, dataRef, "latest", true);

                                            // var xmlFilingContent = await Taxxor.Project.ProjectLogic.RetrieveFilingComposerXmlData(filingComposerDataClient, projectVarsForXhtmlCheck, dataRef, true, true);
                                            // appLogger.LogInformation($"Retrieved {dataReference} for {currentProjectLanguage}:\n{TruncateString(PrettyPrintXml(xmlFilingContent), 1000)}");
                                            if (xmlFilingContent.DocumentElement != null)
                                            {
                                                var xhtmlValidationResult = await ValidateXhtml(xmlFilingContent.DocumentElement, true);

                                                if (!xhtmlValidationResult.Success)
                                                {
                                                    if (logResults) appLogger.LogWarning($"!!!!! XHTML validation error!\nprojectId: {projectVarsForXhtmlCheck.projectId}, data-ref: {dataReference}, language: {projectVarsForXhtmlCheck.outputChannelVariantLanguage}\n" + xhtmlValidationResult.Message + "\n" + string.Join("\n", xhtmlValidationResult.ErrorLog) + "\n!!!!");

                                                    // Add the validation error details to the list so that we can process these errors later
                                                    var validationErrorDetails = new ValidationErrorDetails(projectVarsForXhtmlCheck.projectId, dataRef, projectVarsForXhtmlCheck.outputChannelVariantLanguage, "system@taxxor.com", xhtmlValidationResult.ErrorLog);
                                                    if (!validationErrorDetailsList.Any(v => v.ProjectId == projectVarsForXhtmlCheck.projectId && v.DataReference == dataRef && v.Lang == projectVarsForXhtmlCheck.outputChannelVariantLanguage))
                                                    {
                                                        validationErrorDetailsList.Add(validationErrorDetails);
                                                    }
                                                }
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            // Add the validation error details to the list so that we can process these errors later
                                            var errorLog = new List<string>
                                            {
                                                ex.ToString()
                                            };

                                            if (ex.Message.ToLower().Contains("status code of the remote response is not 'ok' or 'created'"))
                                            {
                                                // It looks like the DocumentStore is not accessible or there is a problem with the DocumentStore URL.
                                                appLogger.LogWarning($"Failed to retrieve filing composer data for {dataRef} (projectId: {projectVarsForXhtmlCheck.projectId}, lang: {projectVarsForXhtmlCheck.outputChannelVariantLanguage}) because DocumentStore is not accessible or the DocumentStore URL is incorrect");
                                                var validationErrorDetails = new ValidationErrorDetails(projectVarsForXhtmlCheck.projectId, dataRef, projectVarsForXhtmlCheck.outputChannelVariantLanguage, "system@taxxor.com", ["documentstore not ready"]);
                                                if (!validationErrorDetailsList.Any(v => v.ProjectId == projectVarsForXhtmlCheck.projectId && v.DataReference == dataRef && v.Lang == projectVarsForXhtmlCheck.outputChannelVariantLanguage))
                                                {
                                                    validationErrorDetailsList.Add(validationErrorDetails);
                                                }
                                                if (logResults) appLogger.LogWarning(ex, $"Failed to retrieve filing composer data for {dataReference} (projectId: {projectVarsForXhtmlCheck.projectId}, lang: {projectVarsForXhtmlCheck.outputChannelVariantLanguage}) because DocumentStore is not accessible or the DocumentStore URL is incorrect");

                                                break;
                                            }
                                            else
                                            {
                                                var validationErrorDetails = new ValidationErrorDetails(projectVarsForXhtmlCheck.projectId, dataRef, projectVarsForXhtmlCheck.outputChannelVariantLanguage, "system@taxxor.com", errorLog);
                                                if (!validationErrorDetailsList.Any(v => v.ProjectId == projectVarsForXhtmlCheck.projectId && v.DataReference == dataRef && v.Lang == projectVarsForXhtmlCheck.outputChannelVariantLanguage))
                                                {
                                                    validationErrorDetailsList.Add(validationErrorDetails);
                                                }
                                                if (logResults) appLogger.LogWarning(ex, $"Failed to retrieve filing composer data for {dataReference} (projectId: {projectVarsForXhtmlCheck.projectId}, lang: {projectVarsForXhtmlCheck.outputChannelVariantLanguage})");

                                            }

                                        }
                                    }
                                    else
                                    {
                                        appLogger.LogInformation($"Skipping {dataRef} because it is not used in lang {currentProjectLanguage} (projectId: {projectVarsForXhtmlCheck.projectId})");
                                    }



                                    // break out of the dataReferences loop
                                    if (testMode) break;
                                }


                            }

                            // break out of the projects loop
                            if (testMode) break;


                        }
                    }


                }

                watch.Stop();
                Console.WriteLine($"Found {validationErrorDetailsList.Count} validation errors");
                Console.WriteLine($"** Finished checking XHTML conformance in {watch.ElapsedMilliseconds.ToString()} ms **");

            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: an error occurred while validating xhtml sections:");
                Console.WriteLine(ex.ToString());
            }

        }


        /// <summary>
        /// Render a report based on the validation errors found
        /// </summary>
        /// <param name="validationErrorDetailsList"></param>
        /// <param name="returnType"></param>
        /// <returns></returns>
        public static string RenderXhtmlValidationReport(ConcurrentBag<ValidationErrorDetails> validationErrorDetailsList, bool renderHtmlChrome = false, ReturnTypeEnum returnType = ReturnTypeEnum.Html)
        {

            // bool containsDocumentstoreNotReady = validationErrorDetailsList.Any(details => details.ErrorLog.Contains("documentstore not ready"));

            // if (containsDocumentstoreNotReady)
            // {
            //     // Documentstore is not ready, handle the situation accordingly
            //     // ...
            // }

            var documentstoreReady = WasDocumentStoreReadyDuringXhtmlValidation(validationErrorDetailsList);
            // var groupedByProjectId = validationErrorDetailsList.GroupBy(v => v.ProjectId);

            // foreach (var group in groupedByProjectId)
            // {
            //     if (group.Count() == 1)
            //     {
            //         bool containsDocumentstoreNotReady = group.Any(details => details.ErrorLog.Contains("documentstore not ready"));

            //         if (containsDocumentstoreNotReady)
            //         {
            //             documentstoreNotReady = true;
            //         }
            //     }
            // }

            var reportContents = "";
            if (!documentstoreReady)
            {
                reportContents = "<p>Skipped because document store not ready.</p>";
            }
            else
            {
                var counter = 1;
                reportContents = string.Join("\n", validationErrorDetailsList.OrderBy(v => v.ProjectId).ThenBy(v => v.DataReference).Select(v => GenerateHtmlString(counter++, v)));

                if (string.IsNullOrEmpty(reportContents))
                {
                    reportContents = "<p>No XHTML validation errors found.</p>";
                }
            }

            if (renderHtmlChrome)
            {
                reportContents = $@"
                <html>
                    <head>
                        <title>XHTML valication results</title>
                    </head>
                    <body>
                        <h1>{TaxxorClientId}: XHTML validation issues</h1>
                        <h2>System information</h2>
                        <p>* Generated on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</p>
                        <p>* Taxxor Client Id: {UpperFirst(TaxxorClientId)}<br/>* Stack: {GetStackType()}</p>
                        <h2>Details</h2>
                        {reportContents}
                    </body>
                </html>";
            }

            string GetStackType()
            {
                switch (siteType)
                {
                    case "prod": return "PROD";
                    case "prev": return "STAGING";
                    case "dev": return "DEV";
                    default: return "UNKNOWN";


                }
            }

            string GenerateHtmlString(int counter, ValidationErrorDetails v)
            {
                var nodeCurrentDomain = xmlApplicationConfiguration.SelectSingleNode($"/configuration/general/domains/domain[@type='{siteType}']");
                var currentDomainName = nodeCurrentDomain?.InnerText ?? "unknown";
                if (siteType == "local" || siteType == "dev") currentDomainName = "editor";
                var protocol = ((nodeCurrentDomain?.GetAttribute("ssl") ?? "false") == "true") ? "https" : "http";
                var fullBaseUri = $"{protocol}://{currentDomainName}";

                var query = HttpUtility.ParseQueryString(string.Empty);
                query["pid"] = v.ProjectId;
                query["oclang"] = v.Lang;
                query["dataref"] = v.DataReference;
                query["killcache"] = "true";
                string queryString = query.ToString() ?? string.Empty;

                // https://editor.taxxor.taxxordm.com/report_editors/default/filingcomposer/redirect.html?pid=ar22-2&killcache=true

                var redirectUrl = $"{fullBaseUri}/report_editors/default/filingcomposer/redirect.html?{queryString}";


                return $"<h3>({counter}) <a href=\"{redirectUrl}\" target=\"_blank\">{v.ProjectId} - {v.DataReference} - {v.Lang}</a></h3>\n{string.Join("<br/>\n", v.ErrorLog)}";
            }
            return reportContents;
        }


        /// <summary>
        /// Analyzes the validation errors to determine if the DocumentStore was ready during the XHTML validation process
        /// </summary>
        /// <param name="validationErrorDetailsList"></param>
        /// <returns></returns>
        public static bool WasDocumentStoreReadyDuringXhtmlValidation(ConcurrentBag<ValidationErrorDetails> validationErrorDetailsList)
        {
            if (validationErrorDetailsList.IsEmpty) return true;

            var documentstoreNotReady = false;
            var groupedByProjectId = validationErrorDetailsList.GroupBy(v => v.ProjectId);

            foreach (var group in groupedByProjectId)
            {
                if (group.Count() == 1)
                {
                    bool containsDocumentstoreNotReady = group.Any(details => details.ErrorLog.Contains("documentstore not ready"));

                    if (containsDocumentstoreNotReady)
                    {
                        documentstoreNotReady = true;
                    }
                }
            }

            return !documentstoreNotReady;
        }


    }
}