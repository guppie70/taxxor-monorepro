using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{


    /// <summary>
    /// Utilities used for interacting with the XBRL service
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Renders an XBRL package based on the posted variables and returns the result to the client
        /// </summary>
        /// <param name="returnType"></param>
        public static async Task RenderXbrl(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Variables to speed up development
            var renderVisuals = true;
            var renderXbrl = true;
            var generateHtmlStylerPackage = true;
            var generateXbrlValidationPackage = siteType != "local";
            generateXbrlValidationPackage = true;

            var debugRoutine = reqVars.isDebugMode == true || siteType == "local" || siteType == "dev" || siteType == "prev";
            var useRandomOutputFolder = siteType == "prev" || siteType == "prod";

            // Output path for the XBRL package on the shared folder

            var xbrlZipPackageFilePathOs = "";
            var debuggerInfo = "";

            // Standard: {pid: '<<project_id>>', vid: '<<version_id>>', ocvariantid, '<<output channel id>>', oclang: '<<output channel language>>', did: 'all'}
            // Optional: {serveasdownload: 'true|false', serveasbinary: 'true|false', mode, 'normal|diff', base: 'baseversion-id', latest: 'current|latestversion-id'}

            //
            // => Retrieve posted values
            //
            var sectionId = request.RetrievePostedValue("did", RegexEnum.Loose, true, reqVars.returnType);
            var projectId = request.RetrievePostedValue("pid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var versionId = request.RetrievePostedValue("vid", @"^[a-zA-Z_\-\d]{1,70}$", true, reqVars.returnType);
            var outputChannelVariantId = request.RetrievePostedValue("ocvariantid", RegexEnum.Strict, true, reqVars.returnType);
            var outputChannelVariantLanguage = request.RetrievePostedValue("oclang", RegexEnum.Strict, true, reqVars.returnType);
            var reportingRequirementScheme = request.RetrievePostedValue("reportingrequirementscheme", RegexEnum.Default.Value, false, reqVars.returnType, "PHG2017");
            var serveAsDownloadString = request.RetrievePostedValue("serveasdownload", "(true|false)", false, reqVars.returnType, "false");
            var serveAsDownload = serveAsDownloadString == "true";
            var serveAsBinaryString = request.RetrievePostedValue("serveasbinary", "(true|false)", false, reqVars.returnType, "false");
            var serveAsBinary = serveAsBinaryString == "true";
            var renderSignatureMarksString = request.RetrievePostedValue("signaturemarks", "(true|false)", false, reqVars.returnType, "true");
            var renderSignatureMarks = renderSignatureMarksString == "true";
            var disableXbrlValidationPackageString = request.RetrievePostedValue("disablexbrlvalidation", "(true|false)", false, reqVars.returnType, "false");
            if (disableXbrlValidationPackageString == "true") generateXbrlValidationPackage = false;
            var includeXbrlGeneratorLogsString = request.RetrievePostedValue("includexbrllogs", "(true|false)", false, reqVars.returnType, "false");
            var includeXbrlGeneratorLogs = (includeXbrlGeneratorLogsString == "true") ? true : false;

            var mode = request.RetrievePostedValue("mode", "normal");
            var clientTimeOffsetMinutesString = request.RetrievePostedValue("timezoneoffset", @"\d{1,5}", true, reqVars.returnType, "0");
            var channel = request.RetrievePostedValue("channel", "(xbrl|ixbrl)", false, reqVars.returnType, "ixbrl");

            //
            // => Calculate the path where we will create the report in
            //
            var tempFolderName = useRandomOutputFolder ? $"xbrl_{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"xbrl_{projectVars.projectId}-{reportingRequirementScheme.ToLower()}-{projectVars.outputChannelVariantLanguage}";
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";

            //
            // => Setup the XBRL Generation Properties object that we will be using throughout the process
            //
            var xbrlConversionProperties = new XbrlConversionProperties(tempFolderPathOs);
            xbrlConversionProperties.ResetLog();
            // TODO: Should probably be part of extensions of configuration
            xbrlConversionProperties.AssetsPrefix = "phg-";
            xbrlConversionProperties.DebugMode = debugRoutine;
            xbrlConversionProperties.Channel = "IXBRL"; // "SECHTML", "XBRL", "IXBRL"
            xbrlConversionProperties.ReportRequirementScheme = reportingRequirementScheme; // "ESMA", "PHG2019"
            xbrlConversionProperties.ClientName = TaxxorClientName;
            xbrlConversionProperties.Prefix = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='prefix']")?.InnerText ?? "";
            var reportingPeriod = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectId}']/reporting_period", xmlApplicationConfiguration);
            if (reportingPeriod == "not-applicable" || reportingPeriod == "none" || string.IsNullOrEmpty(reportingPeriod))
            {
                xbrlConversionProperties.ReportingYear = DateTime.Now.Year;
            }
            else
            {
                xbrlConversionProperties.ReportingYear = Convert.ToInt32(RegExpReplace(@"^..(..)$", reportingPeriod, "20$1"));
            }
            xbrlConversionProperties.SignatureMarks = renderSignatureMarks;
            xbrlConversionProperties.EditorId = projectVars.editorId;
            xbrlConversionProperties.GenerateHtmlStylerOutput = generateHtmlStylerPackage;
            xbrlConversionProperties.CommentContents = $" Document created with Taxxor Disclosure Manager {Guid.NewGuid()} ";

            // Determine the channel based on the reporting scheme
            switch (xbrlConversionProperties.ReportRequirementScheme)
            {
                case "SEC6K":
                    xbrlConversionProperties.Channel = "SECHTML";
                    break;
                default:
                    xbrlConversionProperties.Channel = channel.ToUpper();
                    break;
            }

            //
            // => When we render plain XBRL, then we do not need any pictures and bypass the XBRL validation step
            //
            if (xbrlConversionProperties.Channel == "XBRL")
            {
                generateXbrlValidationPackage = false;
                renderVisuals = false;
            }

            // Load the hierarchy used in this output channel
            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(RetrieveEditorIdFromProjectId(projectId), "pdf", outputChannelVariantId, outputChannelVariantLanguage);
            if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
            {
                xbrlConversionProperties.XmlHierarchy.ReplaceContent(projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml);
            }
            else
            {
                appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}");
            }

            // Retrieve the regulator ID from the data_configuration
            var xPath = $"/configuration/cms_projects/cms_project[@id='{projectId}']/reporting_requirements/reporting_requirement[@ref-mappingservice='{reportingRequirementScheme}']";
            var nodeReportingRequirement = xmlApplicationConfiguration.SelectSingleNode(xPath);
            if (nodeReportingRequirement != null)
            {
                var regulatorId = GetAttribute(nodeReportingRequirement, "regulator");
                if (string.IsNullOrEmpty(regulatorId))
                {
                    appLogger.LogError($"Could not find regulator for reporting scheme {reportingRequirementScheme}");
                }
                else
                {
                    xbrlConversionProperties.RegulatorId = regulatorId;
                }

                // Optionally, the configuration may contain the a special value for the submission type used in the EIS file for the SEC
                xbrlConversionProperties.SubmissionType = nodeReportingRequirement?.GetAttribute("submissiontype");
            }
            else
            {
                appLogger.LogWarning($"Could not find reporting requirement node. xPath: {xPath}");
            }

            // Time difference between server and client
            xbrlConversionProperties.ClientServerOffsetMinutes = CalculateClientServerOffsetInMinutes(clientTimeOffsetMinutesString);
            if (debugRoutine)
            {
                Console.WriteLine("-- Client & Server time difference --");
                Console.WriteLine($"diff: {xbrlConversionProperties.ClientServerOffsetMinutes.ToString()} minutes");
                Console.WriteLine("-------------------------------------");
            }



            // Fill the project variables passed to this function
            xbrlConversionProperties.XbrlVars.projectId = projectId;
            xbrlConversionProperties.XbrlVars.versionId = versionId;

            // Fill with variables retrieved from the context    
            xbrlConversionProperties.XbrlVars.editorContentType = projectVars.editorContentType ?? "regular";
            xbrlConversionProperties.XbrlVars.reportTypeId = projectVars.reportTypeId ?? RetrieveReportTypeIdFromProjectId(projectId);
            xbrlConversionProperties.XbrlVars.outputChannelType = projectVars.outputChannelType;
            xbrlConversionProperties.XbrlVars.editorId = RetrieveEditorIdFromProjectId(projectId);
            xbrlConversionProperties.XbrlVars.outputChannelVariantId = outputChannelVariantId;
            xbrlConversionProperties.XbrlVars.outputChannelVariantLanguage = outputChannelVariantLanguage ?? projectVars.outputChannelDefaultLanguage;
            xbrlConversionProperties.XbrlVars.projectRootPath = projectVars.projectRootPath;
            xbrlConversionProperties.XbrlVars.guidTaxonomy = reportingRequirementScheme;


            // Render the client-side assets assets that this routine needs to use when generating content
            xbrlConversionProperties.XmlClientAssets = Extensions.RenderOutputChannelCssAndJs(xbrlConversionProperties.XbrlVars, "reportgenerator");
            // Console.WriteLine("****************** OVERALL **********************");
            // Console.WriteLine(PrettyPrintXml(xbrlConversionProperties.XmlClientAssets));
            // Console.WriteLine("****************************************");

            // Construct a properties for the XBRL file that we want to generate
            xbrlConversionProperties.Sections = sectionId;
            xbrlConversionProperties.Mode = mode;

            // Image resizing
            double resizeFactor = 1.0;
            switch (xbrlConversionProperties.RegulatorId)
            {
                case "esef":
                case "afm":
                case "sasb":
                    // When we embed images in the XBRL instance document we downsample the images to safe space and to make sure that they are inserted as JFIF images
                    var resizeFactorString = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='compress-xbrl-image-ratio']")?.InnerText ?? "1.0";

                    if (double.TryParse(resizeFactorString, out resizeFactor))
                    {
                        xbrlConversionProperties.ImageResizeFactor = resizeFactor;
                    }

                    break;
            }

            //
            // => Calculate XBRL filename to use when we will be serving the XBRL as a download or when we want to store it on the disk somewhere
            //
            var xbrlDownloadFileName = "xbrl.zip";
            try
            {
                // Create a filename for the download
                xbrlDownloadFileName = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']/name").InnerText.ToLower().Replace(" ", "-");
                xbrlDownloadFileName = RegExpReplace(@"(\s|\||:|\/|&|'|!|@|#|%|\^|\*|\(|\)|\-|\+|=|\~|\$|;|<|>|\?|\||\.|,)+", xbrlDownloadFileName, "-");
                xbrlDownloadFileName = RegExpReplace(@"-+", xbrlDownloadFileName, "-");
                if (xbrlDownloadFileName.Length > 20) xbrlDownloadFileName = xbrlDownloadFileName.Substring(0, 20);
                xbrlDownloadFileName += $"_{xbrlConversionProperties.Sections}_{xbrlConversionProperties.Mode}_{outputChannelVariantLanguage}.zip";
            }
            catch (Exception ex)
            {
                appLogger.LogWarning($"Could not compose a nice filename for the XBRL download. error: {ex}, stack-trace: {GetStackTrace()}");
            }
            xbrlZipPackageFilePathOs = $"{dataRootPathOs}/temp/{xbrlDownloadFileName}";


            xbrlConversionProperties.IsoTimestampPeriodEndDate = CreateIsoTimestampPublicationDate(xbrlConversionProperties);

            //
            // => Return data to the web client to close the XHR call
            //
            if (reqVars.returnType == ReturnTypeEnum.Json)
            {
                await response.OK(GenerateSuccessXml("Started XBRL generation", $"projectId: {projectVars.projectId}"), ReturnTypeEnum.Json, true, true);
                await response.CompleteAsync();
            }


            // Send a message to the client that we will start the generation of an XBRL package
            await _outputGenerationProgressMessage("Start generation of output channel");

            try
            {
                //
                // => Stylesheet cache
                //
                SetupPdfStylesheetCache(reqVars);


                //
                // => Setup folder structure on the shared folder for the package
                //
                try
                {
                    string[] dirs = {
                        xbrlConversionProperties.RootFolderPathOs,
                        xbrlConversionProperties.SystemFolderPathOs,
                        xbrlConversionProperties.InputFolderPathOs,
                        xbrlConversionProperties.InputImagesFolderPathOs,
                        xbrlConversionProperties.WorkingFolderPathOs,
                        xbrlConversionProperties.WorkingVisualsFolderPathOs,
                        xbrlConversionProperties.OutputFolderPathOs,
                        xbrlConversionProperties.OutputEdgarFolderPathOs,
                        xbrlConversionProperties.OutputIxbrlFolderPathOs,
                        xbrlConversionProperties.OutputValidationFolderPathOs,
                        xbrlConversionProperties.OutputWebsiteFolderPathOs,
                        xbrlConversionProperties.OutputXbrlFolderPathOs,
                        xbrlConversionProperties.LogFolderPathOs
                    };

                    foreach (var folderPathOs in dirs)
                    {
                        if (Directory.Exists(folderPathOs)) DelTree(folderPathOs);
                        Directory.CreateDirectory(folderPathOs);
                    }
                }
                catch (Exception ex)
                {
                    // Handle the error
                    HandleError("Could not create directory structure", $"tempFolderPathOs: {tempFolderPathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                }

                //
                // => Load the document we want to use for the conversion
                //
                var fullDocumentLoadResult = await LoadFullTaxxorBaseDocument(xbrlConversionProperties, debugRoutine);
                await _handleProcessStepResult(xbrlConversionProperties, fullDocumentLoadResult);


                //
                // => Clone the XmlDocument so that we have the original document and a document that we will be preparing for XBRL
                //
                xbrlConversionProperties.XmlXbrl.ReplaceContent(xbrlConversionProperties.XmlTaxxor, true);
                xbrlConversionProperties.LogXmlXbrl("initial-version");

                if (renderVisuals)
                {
                    //
                    // => Render image renditions
                    //
                    var imageRenditionsResult = await RenderProjectImageRenditions(xbrlConversionProperties.XbrlVars.projectId);
                    await _handleProcessStepResult(xbrlConversionProperties, imageRenditionsResult);


                    //
                    // => Generate images from the drawings
                    //
                    var drawingsConversionLogResult = await GenerateDrawingRenditions(reqVars, projectVars);
                    await _handleProcessStepResult(xbrlConversionProperties, new TaxxorReturnMessage(drawingsConversionLogResult.Success, drawingsConversionLogResult.Message, drawingsConversionLogResult.DebugInfo));


                    //
                    // => Generate images from the graphs
                    //
                    var graphConversionLogResult = await GenerateGraphRenditions(reqVars, projectVars);
                    await _handleProcessStepResult(xbrlConversionProperties, new TaxxorReturnMessage(graphConversionLogResult.Success, graphConversionLogResult.Message, graphConversionLogResult.DebugInfo));


                    //
                    // => Export all the images used in the project to the shared folder
                    //
                    var imageExportResult = await RenderWebsiteAssets(xbrlConversionProperties.InputFolderPathOs.SubstringAfter("temp/"), false, true, false, false, false, false, false);
                    await _handleProcessStepResult(xbrlConversionProperties, imageExportResult);


                    //
                    // => Prepare the visuals in the document
                    //
                    var visualsPreparationResult = await PrepareVisualsInTaxxorBaseDocument(xbrlConversionProperties, debugRoutine);
                    await _handleProcessStepResult(xbrlConversionProperties, visualsPreparationResult, "image-prepare");
                }

                // throw new Exception("Thrown on purpose");


                //
                // => Generate the basic XHTML file we will be using for the conversion to XBRL
                //
                var baseHtmlConversionResult = await BaseHtmlToInlineXbrlXhtml(xbrlConversionProperties, debugRoutine);
                await _handleProcessStepResult(xbrlConversionProperties, baseHtmlConversionResult, "base-xhtml");


                //
                // => Generate overviews to analyze the generated XBRL files
                //
                var xmlUsedTagsXbrl = await Xslt3TransformXmlToDocument(xbrlConversionProperties.XmlXbrl, "used-tags-utility");
                await xmlUsedTagsXbrl.SaveAsync($"{xbrlConversionProperties.LogFolderPathOs}/__used-tags-xbrl.xml");


                if ((xbrlConversionProperties.Channel == "IXBRL" || xbrlConversionProperties.Channel == "XBRL") && renderXbrl)
                {
                    //
                    // => Generate the Inline XBRL package
                    //
                    var ixbrlPackageCreationResult = await CompileXbrlPackage(xbrlConversionProperties, debugRoutine);
                    await _handleProcessStepResult(xbrlConversionProperties, ixbrlPackageCreationResult);


                    //
                    // => Validate the generated XBRL package using Arelle
                    //
                    if (generateXbrlValidationPackage)
                    {
                        var arelleRenderingResult = await CreateValidationPackage(xbrlConversionProperties, debugRoutine);
                        await _handleProcessStepResult(xbrlConversionProperties, arelleRenderingResult);
                    }
                }



                if (xbrlConversionProperties.Channel != "IXBRL" && xbrlConversionProperties.RegulatorId == "sec")
                {
                    //
                    // => Convert the Inline XBRL documents to EDGAR HTML
                    //                    
                    var edgarHtmlConversionResult = await InlineXbrlToEdgar(xbrlConversionProperties, debugRoutine);
                    await _handleProcessStepResult(xbrlConversionProperties, edgarHtmlConversionResult, "edgar-xhtml");

                    //
                    // => Generate overviews to analyze the generated EDGAR files
                    //
                    var xmlUsedTagsEdgar = await Xslt3TransformXmlToDocument(xbrlConversionProperties.XmlXbrl, "used-tags-utility");
                    await xmlUsedTagsEdgar.SaveAsync($"{xbrlConversionProperties.LogFolderPathOs}/__used-tags-edgar.xml");

                    //
                    // => Generate the EDGAR package
                    //
                    var edgarPackageCreationResult = await CompileEdgarPackage(xbrlConversionProperties, debugRoutine);
                    await _handleProcessStepResult(xbrlConversionProperties, edgarPackageCreationResult);
                }



                //
                // => Dump the log file
                //
                xbrlConversionProperties.StoreLog();


                //
                // => Rework the folder structure to the structure we want it to be for the user
                //
                try
                {
                    var mainFolderPathOs = "";
                    switch (xbrlConversionProperties.Channel)
                    {
                        case "IXBRL":
                            mainFolderPathOs = xbrlConversionProperties.OutputIxbrlFolderPathOs;
                            break;

                        case "XBRL":
                            mainFolderPathOs = xbrlConversionProperties.OutputXbrlFolderPathOs;
                            break;

                        case "SEC":
                            mainFolderPathOs = xbrlConversionProperties.OutputEdgarFolderPathOs;
                            break;

                        case "WEB":
                            mainFolderPathOs = xbrlConversionProperties.OutputWebsiteFolderPathOs;
                            break;

                        default:
                            appLogger.LogWarning($"No support for {xbrlConversionProperties.Channel}");
                            break;
                    }

                    switch (xbrlConversionProperties.Channel)
                    {
                        case "IXBRL":
                        case "XBRL":
                            CopyDirectoryRecursive(mainFolderPathOs, xbrlConversionProperties.RootFolderPathOs);
                            DelTree(mainFolderPathOs);
                            break;
                    }

                    //
                    // => Cleanup output folder structure
                    //
                    foreach (var directory in Directory.GetDirectories(xbrlConversionProperties.OutputFolderPathOs))
                    {
                        if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                        {
                            Directory.Delete(directory, false);
                        }
                    }
                    foreach (var directory in Directory.GetDirectories(xbrlConversionProperties.RootFolderPathOs))
                    {
                        if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                        {
                            Directory.Delete(directory, false);
                        }
                    }
                    DelTree(xbrlConversionProperties.InputFolderPathOs);
                    DelTree($"{xbrlConversionProperties.WorkingFolderPathOs}/visuals");


                }
                catch (Exception ex)
                {
                    var errorMessageRework = "Error while cleaning up the download package";
                    appLogger.LogError(ex, errorMessageRework);
                }


                //
                // => Zip up the result of the complete package that we want to serve to the client
                //
                try
                {
                    if (File.Exists(xbrlZipPackageFilePathOs)) File.Delete(xbrlZipPackageFilePathOs);

                    if (!xbrlConversionProperties.IncludeXbrlGeneratorLogs)
                    {
                        DelTree($"{xbrlConversionProperties.RootFolderPathOs}/.system");
                    }

                    ZipFile.CreateFromDirectory(xbrlConversionProperties.RootFolderPathOs, xbrlZipPackageFilePathOs);

                    if (debugRoutine)
                    {
                        appLogger.LogInformation($"Created downloadable zip file: {xbrlZipPackageFilePathOs} from directory: {xbrlConversionProperties.RootFolderPathOs}");
                    }
                }
                catch (Exception ex)
                {
                    HandleError("Could not create ZIP package", $"xbrlConversionProperties.RootFolderPathOs: {xbrlConversionProperties.RootFolderPathOs}, zipFilePathOs: {xbrlZipPackageFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                }


                // // Dump the contents of this object to an XML file and exit
                // xbrlConversionProperties.ToXml($"{logRootPathOs}/~xbrlconversionproperties.xml");
                // throw new Exception("Thrown on purpose");

                try
                {


                    // - For the HTML styler
                    if (generateHtmlStylerPackage)
                    {
                        var htmlStylerSourceFilePathOs = $"{dataRootPathOs}/temp/htmlstyler-{xbrlConversionProperties.ReportRequirementScheme}.html";
                        var iXbrlInstanceDocumentPathOs = RetrieveInstanceDocumentPath($"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl");
                        var htmlStylerFolderPathOs = RenderHtmlStylerOutputPathOs(projectVars.projectId, $"ixbrl-{xbrlConversionProperties.ReportRequirementScheme}");
                        if (iXbrlInstanceDocumentPathOs != null && File.Exists(iXbrlInstanceDocumentPathOs))
                        {
                            CopyDirectoryRecursive($"{xbrlConversionProperties.WorkingFolderPathOs}/ixbrl", htmlStylerFolderPathOs);

                            if (File.Exists(htmlStylerSourceFilePathOs))
                            {
                                File.Copy(htmlStylerSourceFilePathOs, $"{htmlStylerFolderPathOs}/index.html", true);
                            }
                            else
                            {
                                appLogger.LogError($"Unable to copy file for the HTML styler because the source '{htmlStylerSourceFilePathOs}' could not be located");
                            }
                        }
                        else
                        {

                            if (Directory.Exists(xbrlConversionProperties.OutputEdgarFolderPathOs))
                            {
                                htmlStylerFolderPathOs = htmlStylerFolderPathOs.Replace("ixbrl-", "edgar-");
                                CopyDirectoryRecursive(xbrlConversionProperties.OutputEdgarFolderPathOs, htmlStylerFolderPathOs);
                                if (File.Exists(htmlStylerSourceFilePathOs))
                                {
                                    File.Copy(htmlStylerSourceFilePathOs, $"{htmlStylerFolderPathOs}/index.html", true);
                                }
                                else
                                {
                                    appLogger.LogError($"Unable to copy file for the HTML styler because the source '{htmlStylerSourceFilePathOs}' could not be located");
                                }
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to copy file for the HTML styler because the sources '{iXbrlInstanceDocumentPathOs}' and '{xbrlConversionProperties.OutputEdgarFolderPathOs}' could not be located");
                            }
                        }

                        DeleteEmptyDirectories(Path.GetDirectoryName(htmlStylerFolderPathOs));
                    }


                    //
                    // => Create a ZIP file for non iXBRL requests
                    //
                    if (xbrlConversionProperties.Channel != "IXBRL")
                    {
                        // Zip up the first directory that we encounter
                        // - check if there are files directly in /output -> then we zip up the complete dir
                        var dirs = new List<string>(Directory.EnumerateDirectories(xbrlConversionProperties.OutputEdgarFolderPathOs));
                        var files = new List<string>(Directory.EnumerateFiles(xbrlConversionProperties.OutputEdgarFolderPathOs));
                        if (dirs.Count == 0 && files.Count == 0)
                        {
                            appLogger.LogError($"Failed to generate XBRL zip folder of core filing package, because there are no files or folders in {xbrlConversionProperties.OutputEdgarFolderPathOs} to zip up");
                        }
                        else if (files.Count > 0)
                        {
                            // Zip up the contents of the output directory
                            if (File.Exists(xbrlZipPackageFilePathOs)) File.Delete(xbrlZipPackageFilePathOs);
                            ZipFile.CreateFromDirectory(xbrlConversionProperties.OutputEdgarFolderPathOs, xbrlZipPackageFilePathOs);
                        }
                        else if (dirs.Count == 1)
                        {
                            // Base the zip on the content of the single subdirectory in the output folder
                            if (File.Exists(xbrlZipPackageFilePathOs)) File.Delete(xbrlZipPackageFilePathOs);
                            ZipFile.CreateFromDirectory(dirs[0], xbrlZipPackageFilePathOs);
                        }
                        else
                        {
                            // The output directory contains multiple subdirectories, so we zip-up all of them
                            ZipFile.CreateFromDirectory(xbrlConversionProperties.OutputEdgarFolderPathOs, xbrlZipPackageFilePathOs);
                        }
                    }

                }
                catch (Exception ex)
                {
                    appLogger.LogWarning($"Unable to cleanup XBRL ZIP file that we want to return to the client. error: {ex}");
                }


                // Generate the XBRL and return the result in the correct format
                if (serveAsBinary)
                {
                    xbrlConversionProperties.Dispose();

                    GC.Collect();

                    // Stream the XBRL file to the client
                    await StreamFile(context.Response, xbrlDownloadFileName, xbrlZipPackageFilePathOs, serveAsDownload, reqVars.returnType);
                }
                else
                {
                    // Path where the assets of this report can be found (we are assuming that the SEC filing service is running in a container)
                    var sharedFolderPathOs = (xbrlConversionProperties.Channel == "SECHTML") ? xbrlConversionProperties.OutputEdgarFolderPathOs : xbrlConversionProperties.OutputIxbrlFolderPathOs;
                    sharedFolderPathOs = "/mnt/shared" + sharedFolderPathOs.SubstringAfter("/shared");

                    //
                    // => Determine the filing period, submission type and fixed naming for the downloads
                    //
                    var filingPeriod = ""; // mm-dd-yyyy
                    var submissionType = "";
                    string? fixedXbrlZipFileName = null;


                    switch (xbrlConversionProperties.RegulatorId)
                    {
                        case "sec":

                            switch (xbrlConversionProperties.ReportRequirementScheme)
                            {
                                case "SEC6K":
                                    submissionType = "6-K";

                                    // Filing period differs between QR and small 6-K
                                    switch (xbrlConversionProperties.XbrlVars.reportTypeId)
                                    {
                                        case "philips-sec-6k-small":
                                            // Launch date is the submission date
                                            var nodeProjectConfig = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']");

                                            if (nodeProjectConfig != null)
                                            {
                                                var publicationDate = nodeProjectConfig.GetAttribute("date-publication");
                                                if (!string.IsNullOrEmpty(publicationDate))
                                                {
                                                    var parsedDate = DateTime.Parse(publicationDate);
                                                    filingPeriod = parsedDate.ToString("MM-dd-yyyy");
                                                }
                                                else
                                                {
                                                    // Default to today's date
                                                    filingPeriod = $"{DateTime.Now.ToString("MM-dd-yyyy")}";
                                                }
                                            }
                                            break;
                                        case "philips-quarterly-report":
                                            // Launch date depends on the quarter
                                            var quarter = RegExpReplace(@"^(..)..$", reportingPeriod, "$1");
                                            switch (quarter)
                                            {
                                                case "q1":
                                                    filingPeriod = $"03-31-{xbrlConversionProperties.ReportingYear}";
                                                    break;
                                                case "q2":
                                                    filingPeriod = $"06-30-{xbrlConversionProperties.ReportingYear}";
                                                    break;
                                                case "q3":
                                                    filingPeriod = $"09-30-{xbrlConversionProperties.ReportingYear}";
                                                    break;
                                                case "q4":
                                                    filingPeriod = $"12-31-{xbrlConversionProperties.ReportingYear}";
                                                    break;
                                                default:
                                                    appLogger.LogCritical($"Could not determine filing date for quarter {quarter}");
                                                    break;
                                            }

                                            break;

                                        default:
                                            appLogger.LogCritical($"Could not determine filing date for report-type {xbrlConversionProperties.XbrlVars.reportTypeId}");
                                            break;

                                    }

                                    break;
                                default:
                                    submissionType = "20-F";
                                    filingPeriod = $"12-31-{xbrlConversionProperties.ReportingYear}";
                                    break;
                            }

                            break;

                            // case "afm":
                            // case "sasb":

                            //     // The name of the ZIP download needs to match the name of the folder containing the iXBRL files
                            //     var xbrlFolderPathOs = $"{zipCleanupFolderPathOs}/output/ixbrl";
                            //     if (Directory.Exists(xbrlFolderPathOs))
                            //     {
                            //         List<string> dirs = new List<string>(Directory.EnumerateDirectories(xbrlFolderPathOs));

                            //         foreach (var folderPathOs in dirs)
                            //         {
                            //             fixedXbrlZipFileName = $"{Path.GetFileName(folderPathOs)}.zip";
                            //             break;
                            //         }
                            //     }
                            //     else
                            //     {
                            //         throw new Exception($"Could not find base path to XBRL assets.");
                            //     }


                            //     break;

                    }

                    //
                    // => Store the generated XBRL file in the generated reports folder
                    //
                    TaxxorReturnMessage? generatedReportsAddResult = null;
                    if (xbrlConversionProperties.Channel == "XBRL" || xbrlConversionProperties.Channel == "IXBRL")
                    {
                        // - Copy the complete zip including the README.html file back to the shared folder so that the repository logic in the Document Store can pick it up.
                        var copyPackageSuccess = false;
                        var repositoryFileName = $"xbrl_{projectId}-{xbrlConversionProperties.RegulatorId.ToLower()}-{xbrlConversionProperties.ReportRequirementScheme.ToLower()}-{outputChannelVariantLanguage}.zip";
                        var xbrlPackageTempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/xbrlpackage-{GenerateRandomString(10)}";
                        var xbrlPackageTempFilePathOs = $"{xbrlPackageTempFolderPathOs}/{repositoryFileName}";
                        try
                        {
                            if (siteType == "local") xbrlConversionProperties.ToXml($"{logRootPathOs}/xbrl-conversion-properties.xml");

                            Directory.CreateDirectory(xbrlPackageTempFolderPathOs);
                            File.Copy(xbrlZipPackageFilePathOs, xbrlPackageTempFilePathOs, true);
                            copyPackageSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Failed to add XBRL report to the repository");
                            await _outputGenerationProgressMessage($"WARNING: Unable to add the generated XBRL report to the repository");
                        }

                        // - Create the entry in the generated reports repository
                        if (copyPackageSuccess)
                        {
                            var xbrlPackageRelativePath = xbrlPackageTempFilePathOs.Replace(RetrieveSharedFolderPathOs(), "");
                            generatedReportsAddResult = await DocumentStoreService.GeneratedReportsRepository.Add(xbrlPackageRelativePath, xbrlConversionProperties.ReportRequirementScheme, xbrlConversionProperties.XbrlValidationLinks, debugRoutine);
                            if (generatedReportsAddResult.Success)
                            {
                                await _outputGenerationProgressMessage($"{generatedReportsAddResult.Message}");
                            }
                            else
                            {
                                appLogger.LogWarning($"Failed to add XBRL report to the repository. {generatedReportsAddResult.ToString()}");
                                await _outputGenerationProgressMessage($"WARNING: {generatedReportsAddResult.Message}");
                            }
                        }
                    }


                    //
                    // => Write a response to the client
                    //
                    if (reqVars.returnType == ReturnTypeEnum.Json)
                    {

                        dynamic jsonData = new ExpandoObject();
                        jsonData.result = new ExpandoObject();

                        jsonData.result.message = $"Successfully rendered the {submissionType} file";
                        // Name of the generated file on the disk
                        jsonData.result.filename = $"{xbrlDownloadFileName}";
                        // Filename of the download
                        jsonData.result.fixedpackagename = fixedXbrlZipFileName;

                        // Shared folder path where the filing content is stored
                        jsonData.result.folderpathos = sharedFolderPathOs;

                        // Regulator id
                        jsonData.result.regulatorid = xbrlConversionProperties.RegulatorId;

                        // Submission type
                        jsonData.result.submissiontype = submissionType;

                        // Filing period
                        jsonData.result.filingperiod = filingPeriod;

                        // Repository item id
                        jsonData.result.repositoryitemid = (generatedReportsAddResult?.Success ?? false) ? generatedReportsAddResult.Payload : "";


                        if (!string.IsNullOrEmpty(xbrlConversionProperties.ValidationPackageUri))
                        {
                            jsonData.result.validationuri = xbrlConversionProperties.ValidationPackageUri;
                        }

                        if ((siteType == "local" || siteType == "dev") && debuggerInfo != "")
                        {
                            //jsonData.result.debuginfo = debuggerInfo;
                        }

                        var json = (string)ConvertToJson(jsonData);

                        // Create a TaxxorReturnMessage
                        var generationResult = new TaxxorReturnMessage(true, "Successfully generated XBRL package", json, "");

                        // Send the result of the synchronization process back to the server (use the message field to transport the target project id)
                        await MessageToCurrentClient("XbrlGenerationDone", generationResult);
                    }
                    else if (reqVars.returnType == ReturnTypeEnum.Xml)
                    {
                        XmlDocument? xmlResponse = RetrieveTemplate("success_xml");
                        xmlResponse.SelectSingleNode("/result/message").InnerText = "Successfully rendered the XBRL file";

                        var nodeFileName = xmlResponse.CreateElement("filename");
                        nodeFileName.InnerText = $"{xbrlDownloadFileName}";
                        xmlResponse.DocumentElement.AppendChild(nodeFileName);

                        var nodeFixedPackageName = xmlResponse.CreateElementWithText("fixedpackagename", string.IsNullOrEmpty(fixedXbrlZipFileName) ? "null" : fixedXbrlZipFileName);
                        xmlResponse.DocumentElement.AppendChild(nodeFixedPackageName);

                        var nodeFolderPath = xmlResponse.CreateElement("folderpathos");
                        nodeFolderPath.InnerText = sharedFolderPathOs;
                        xmlResponse.DocumentElement.AppendChild(nodeFolderPath);

                        var nodeRegulatorId = xmlResponse.CreateElement("regulatorid");
                        nodeRegulatorId.InnerText = xbrlConversionProperties.RegulatorId;
                        xmlResponse.DocumentElement.AppendChild(nodeRegulatorId);

                        var nodeSubmissionType = xmlResponse.CreateElement("submissiontype");
                        nodeSubmissionType.InnerText = submissionType;
                        xmlResponse.DocumentElement.AppendChild(nodeSubmissionType);

                        var nodeFilingPeriod = xmlResponse.CreateElement("filingperiod");
                        nodeFilingPeriod.InnerText = filingPeriod;
                        xmlResponse.DocumentElement.AppendChild(nodeFilingPeriod);

                        var nodeRepositoryItemId = xmlResponse.CreateElement("repositoryitemid");
                        nodeRepositoryItemId.InnerText = (generatedReportsAddResult?.Success ?? false) ? generatedReportsAddResult.Payload : "";
                        xmlResponse.DocumentElement.AppendChild(nodeRepositoryItemId);

                        if (!string.IsNullOrEmpty(xbrlConversionProperties.ValidationPackageUri))
                        {
                            var nodeValidationUri = xmlResponse.CreateElement("validationuri");
                            nodeValidationUri.InnerText = xbrlConversionProperties.ValidationPackageUri;
                            xmlResponse.DocumentElement.AppendChild(nodeValidationUri);
                        }


                        if ((siteType == "local" || siteType == "dev") && debuggerInfo != "")
                        {
                            xmlResponse.SelectSingleNode("/result/debuginfo").InnerText = debuggerInfo;
                        }

                        await context.Response.OK(xmlResponse, ReturnTypeEnum.Xml, false);

                    }
                    else
                    {
                        var message = $"Successfully rendered the XBRL file. Filename: {xbrlDownloadFileName}";

                        if ((siteType == "local" || siteType == "dev") && debuggerInfo != "") message += ". Debuginfo: " + debuggerInfo;

                        await context.Response.OK(message, ReturnTypeEnum.Txt, true);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = new TaxxorReturnMessage(false, "There was an error generating XBRL package", $"error: {ex}");

                if (reqVars.returnType == ReturnTypeEnum.Json)
                {
                    appLogger.LogError(ex, errorMessage.Message);

                    await MessageToCurrentClient("XbrlGenerationDone", errorMessage);
                }
                else
                {
                    HandleError(errorMessage);
                }
            }
            finally
            {
                xbrlConversionProperties.Dispose();

                GC.Collect();
            }


        }

        /// <summary>
        /// Streams a generated XBRL file from the user's temp directory to the client as a download
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="type">Supported are "xbrlinstance" and "pdfcompare"</param>
        public static async Task DownloadFile(string fileName, string type, string binaryFileName = null)
        {
            var context = System.Web.Context.Current;

            var pathOs = "";

            switch (type)
            {
                case "xbrlinstance":
                    await DocumentStoreService.UserTempData.Stream(fileName, true, "instancedocument.xml", true);
                    break;

                case "pdfcompare":
                    pathOs = $"{dataRootPathOs}/temp/{fileName}";
                    if (binaryFileName == null)
                    {
                        await StreamFile(context.Response, pathOs, true, ReturnTypeEnum.Txt);
                    }
                    else
                    {
                        await StreamFile(context.Response, binaryFileName, pathOs, true, ReturnTypeEnum.Txt);
                    }
                    break;

                case "pdfsectioncompare":
                    pathOs = $"{RetrieveSharedFolderPathOs()}/{fileName}";
                    if (binaryFileName == null)
                    {
                        await StreamFile(context.Response, pathOs, true, ReturnTypeEnum.Txt);
                    }
                    else
                    {
                        await StreamFile(context.Response, binaryFileName, pathOs, true, ReturnTypeEnum.Txt);
                    }
                    break;

                default:
                    break;
            }

        }

        /// <summary>
        /// Represents an exhibit of a filing
        /// </summary>
        public class XbrlExhibit
        {
            public string FileName { get; set; } = null;
            public string Title { get; set; } = null;
            public XmlDocument Xml { get; set; } = new XmlDocument();

            public XbrlExhibit()
            {

            }

            public XbrlExhibit(string pathOs)
            {
                this.FileName = Path.GetFileName(pathOs);
            }
        }

        /// <summary>
        /// Object to be used when generating XBRL
        /// </summary>
        public class XbrlConversionProperties : IDisposable
        {
            public string? ClientName { get; set; } = null;
            public string? Prefix { get; set; } = null;
            public string? EditorId { get; set; } = null;
            public int ReportingYear { get; set; } = -1;

            public double ClientServerOffsetMinutes { get; set; } = 0;

            public string? IsoTimestampPeriodEndDate { get; set; } = null;

            public bool DebugMode { get; set; } = false;

            public bool GenerateHtmlStylerOutput { get; set; } = false;

            public string? AssetsPrefix { get; set; } = null;

            public string? RootFolderPathOs { get; set; } = null;
            public string? InputFolderPathOs { get; set; } = null;
            public string? InputImagesFolderPathOs { get; set; } = null;
            public string? WorkingFolderPathOs { get; set; } = null;
            public string? WorkingVisualsFolderPathOs { get; set; } = null;
            public string? SystemFolderPathOs { get; set; } = null;
            public string? OutputFolderPathOs { get; set; } = null;
            public string? OutputEdgarFolderPathOs { get; set; } = null;
            public string? OutputIxbrlFolderPathOs { get; set; } = null;
            public string? OutputXbrlFolderPathOs { get; set; } = null;
            public string? OutputWebsiteFolderPathOs { get; set; } = null;
            public string? OutputValidationFolderPathOs { get; set; } = null;
            public string? LogFolderPathOs { get; set; } = null;
            public string? ValidationPackageUri { get; set; } = null;

            public string? AccessToken { get; set; } = null;

            public string? Sections { get; set; } = null;
            public string? Mode { get; set; } = null;

            public string? XbrlPackagePathOs { get; set; } = null;

            /// <summary>
            /// Defines the main title of the generated report
            /// Analogue to "booktitle" in the PDF flow
            /// </summary>
            /// <value></value>
            public string? ReportCaption { get; set; } = null;

            /// <summary>
            /// Possible values are:
            /// - SEC
            /// - XBRL
            /// - IXBRL
            /// - WEB
            /// </summary>
            /// <value></value>
            public string? Channel { get; set; } = null;

            public string? RegulatorId { get; set; } = null;

            public string? SubmissionType { get; set; } = null;

            public string? ReportRequirementScheme { get; set; } = null;

            public string? CommentContents { get; set; } = null;

            public Dictionary<string, string> XbrlValidationLinks { get; set; } = new Dictionary<string, string>();

            public ProjectVariables XbrlVars { get; set; } = new ProjectVariables();

            public XmlDocument XmlTaxxor { get; set; } = new XmlDocument();
            public XmlDocument XmlXbrl { get; set; } = new XmlDocument();
            public XmlDocument XmlHierarchy { get; set; } = new XmlDocument();
            public XmlDocument XmlClientAssets { get; set; } = new XmlDocument();

            private XmlDocument _xmlLog = new XmlDocument();

            public Dictionary<string, XbrlExhibit> Exhibits { get; set; } = new Dictionary<string, XbrlExhibit>();

            /// <summary>
            /// Show signature marks or not
            /// </summary>
            public bool SignatureMarks = true;

            /// <summary>
            /// Determines the amount in which the images need to be resampled to "compress" image size
            /// </summary>
            /// <value></value>
            public double ImageResizeFactor { get; set; } = 1;

            /// <summary>
            /// In or exclude XBRL generation log files in the resulting package
            /// </summary>
            public bool IncludeXbrlGeneratorLogs = false;

            /// <summary>
            /// For disposing the class
            /// </summary>
            private bool _disposed = false;

            private int _logDumpCounter = 0;

            public XbrlConversionProperties()
            {
                // Empty constructor
            }

            public XbrlConversionProperties(string baseFolderPathOs)
            {
                this.RootFolderPathOs = baseFolderPathOs;

                // Setup the main directory that we will be using throughout the conversion process
                try
                {
                    if (Directory.Exists(baseFolderPathOs))
                    {
                        Directory.Delete(baseFolderPathOs, true);
                    }
                    Directory.CreateDirectory(baseFolderPathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Could not create root directory for the conversion process {baseFolderPathOs}");
                }



                // Derive main paths from the folder provided as input
                this.SystemFolderPathOs = $"{baseFolderPathOs}/.system";
                this.InputFolderPathOs = $"{this.SystemFolderPathOs}/input";
                this.InputImagesFolderPathOs = $"{this.InputFolderPathOs}/images";
                this.WorkingFolderPathOs = $"{this.SystemFolderPathOs}/_work";
                this.WorkingVisualsFolderPathOs = $"{this.WorkingFolderPathOs}/visuals";
                this.OutputFolderPathOs = $"{baseFolderPathOs}/output";
                this.OutputEdgarFolderPathOs = $"{this.OutputFolderPathOs}/edgar";
                this.OutputIxbrlFolderPathOs = $"{this.OutputFolderPathOs}/ixbrl";
                this.OutputXbrlFolderPathOs = $"{this.OutputFolderPathOs}/xbrl";
                this.OutputWebsiteFolderPathOs = $"{this.OutputFolderPathOs}/website";
                this.OutputValidationFolderPathOs = $"{this.OutputFolderPathOs}/validation";
                this.LogFolderPathOs = $"{this.SystemFolderPathOs}/logs";

                var tokenExpire = (siteType == "local" || siteType == "dev") ? 3600 : 3600;
                this.AccessToken = Taxxor.Project.ProjectLogic.AccessToken.GenerateToken(tokenExpire);
            }

            /// <summary>
            /// Dumps the currently available version of the XBRL XML document in the log folder
            /// </summary>
            /// <param name="stepName"></param>
            public void LogXmlXbrl(string stepName)
            {
                LogXmlXbrl(this.XmlXbrl, stepName);
            }

            /// <summary>
            /// Dumps an XML document in the log folder
            /// </summary>
            /// <param name="xmlToDump"></param>
            /// <param name="stepName"></param>
            public void LogXmlXbrl(XmlDocument xmlToDump, string stepName)
            {
                if (this.DebugMode)
                {
                    try
                    {
                        xmlToDump.Save($"{this.LogFolderPathOs}/_{this._logDumpCounter.ToString()}-{stepName}.xml");
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, $"Could not store XML in log directory");
                    }
                    this._logDumpCounter++;
                }
            }

            /// <summary>
            /// Resets the XML log
            /// </summary>
            public void ResetLog()
            {
                _xmlLog.LoadXml("<log/>");
            }

            /// <summary>
            /// Logs a message in the XML log file
            /// </summary>
            /// <param name="category"></param>
            /// <param name="message"></param>
            public void Log(string category, string message)
            {
                var nodeCategory = _xmlLog.SelectSingleNode($"/log/{category}");
                if (nodeCategory == null)
                {
                    nodeCategory = _xmlLog.CreateElement(category);
                    _xmlLog.DocumentElement.AppendChild(nodeCategory);
                }

                // Create the log entry
                var nodeLogEntry = _xmlLog.CreateElement("entry");
                nodeLogEntry.InnerText = message;
                nodeCategory.AppendChild(nodeLogEntry);
            }

            public XmlDocument RetrieveLog()
            {
                return this._xmlLog;
            }

            /// <summary>
            /// Stores the log file on the disk
            /// </summary>
            public void StoreLog()
            {
                try
                {
                    _xmlLog.Save($"{this.LogFolderPathOs}/_log.xml");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Could not store log file in log directory");
                }
            }

            /// <summary>
            /// Saves all the exhibits in the folder passed to this function
            /// </summary>
            /// <param name="folderRootPathOs"></param>
            /// <parem namr="asHtml"></param>
            public async Task SaveExhibits(string folderRootPathOs, bool asHtml = false)
            {
                if (!Directory.Exists(folderRootPathOs)) Directory.CreateDirectory(folderRootPathOs);

                foreach (var pair in this.Exhibits)
                {
                    var exhibit = pair.Value;
                    var exhibitPathOs = $"{folderRootPathOs}/{exhibit.FileName}";
                    var xmlExhibit = new XmlDocument();
                    xmlExhibit.ReplaceContent(exhibit.Xml, true);

                    // Save and use this "trick" to convert non-ascii character to numeric entities
                    using (Stream stream = File.Create(exhibitPathOs))
                    {
                        using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings()
                        {
                            OmitXmlDeclaration = true,
                            Encoding = Encoding.ASCII,
                            Indent = true,
                            IndentChars = " "
                        }))
                        {
                            xmlExhibit.Save(writer);
                        }
                    }

                    // Add the XHTML namespace if we are not saving the exhibit as plain HTML
                    if (!asHtml)
                    {
                        xmlExhibit.Load(exhibitPathOs);
                        xmlExhibit.ReplaceContent(AddXhtmlNameSpace(xmlExhibit), true);
                        await xmlExhibit.SaveAsync(exhibitPathOs);
                    }
                    else
                    {
                        await _convertXmlToHtml(exhibitPathOs);
                    }

                }
            }

            /// <summary>
            /// Dumps the content of this object to an XML file
            /// </summary>
            /// <param name="outputPathOs"></param>
            public void ToXml(string outputPathOs)
            {
                var xmlXbrlProperties = new XmlDocument();
                var nodeRoot = xmlXbrlProperties.CreateElement("properties");

                InsertKeyValue("ClientName", this.ClientName);
                InsertKeyValue("Prefix", this.Prefix);
                InsertKeyValue("IsoTimestampPeriodEndDate", this.IsoTimestampPeriodEndDate);
                InsertKeyValue("EditorId", this.EditorId);
                InsertKeyValueInt("ReportingYear", this.ReportingYear);
                InsertKeyValue("AssetsPrefix", this.AssetsPrefix);
                InsertKeyValue("RootFolderPathOs", this.RootFolderPathOs);
                InsertKeyValue("InputFolderPathOs", this.InputFolderPathOs);
                InsertKeyValue("InputImagesFolderPathOs", this.InputImagesFolderPathOs);
                InsertKeyValue("WorkingFolderPathOs", this.WorkingFolderPathOs);
                InsertKeyValue("OutputFolderPathOs", this.OutputFolderPathOs);
                InsertKeyValue("OutputEdgarFolderPathOs", this.OutputEdgarFolderPathOs);
                InsertKeyValue("OutputIxbrlFolderPathOs", this.OutputIxbrlFolderPathOs);
                InsertKeyValue("OutputXbrlFolderPathOs", this.OutputXbrlFolderPathOs);
                InsertKeyValue("OutputWebsiteFolderPathOs", this.OutputWebsiteFolderPathOs);
                InsertKeyValue("OutputValidationFolderPathOs", this.OutputValidationFolderPathOs);
                InsertKeyValue("LogFolderPathOs", this.LogFolderPathOs);
                InsertKeyValue("ValidationPackageUri", this.ValidationPackageUri);
                InsertKeyValue("ReportRequirementScheme", this.ReportRequirementScheme);
                InsertKeyValue("AccessToken", this.AccessToken);
                InsertKeyValue("Sections", this.Sections);
                InsertKeyValue("Mode", this.Mode);
                InsertKeyValue("XbrlPackagePathOs", this.XbrlPackagePathOs);
                InsertKeyValue("Channel", this.Channel);
                InsertKeyValue("RegulatorId", this.RegulatorId);

                if (this.XbrlValidationLinks.Count > 0)
                {
                    var nodeValidationLinksRoot = xmlXbrlProperties.CreateElement("XbrlValidationLinks");
                    foreach (var item in this.XbrlValidationLinks)
                    {
                        var nodeLink = xmlXbrlProperties.CreateElementWithText("XbrlValidationLink", item.Value);
                        nodeLink.SetAttribute("id", item.Key);
                        nodeValidationLinksRoot.AppendChild(nodeLink);
                    }

                    nodeRoot.AppendChild(nodeValidationLinksRoot);
                }

                InsertKeyValueXml("XmlXbrl", this.XmlXbrl);
                InsertKeyValueXml("XmlHierarchy", this.XmlHierarchy);
                InsertKeyValueXml("XmlClientAssets", this.XmlClientAssets);
                InsertKeyValue("_xmlLog", this._xmlLog.OuterXml);

                xmlXbrlProperties.AppendChild(nodeRoot);

                xmlXbrlProperties.Save(outputPathOs);

                void InsertKeyValue(string key, string value)
                {
                    var nodeProperty = xmlXbrlProperties.CreateElementWithText("property", value);
                    nodeProperty.SetAttribute("key", key);
                    nodeProperty.SetAttribute("type", "string");
                    nodeRoot.AppendChild(nodeProperty);
                }

                void InsertKeyValueInt(string key, int value)
                {
                    var nodeProperty = xmlXbrlProperties.CreateElementWithText("property", value.ToString());
                    nodeProperty.SetAttribute("key", key);
                    nodeProperty.SetAttribute("type", "int");
                    nodeRoot.AppendChild(nodeProperty);
                }

                void InsertKeyValueXml(string key, XmlDocument value)
                {
                    var nodeProperty = xmlXbrlProperties.CreateElement("property");
                    nodeProperty.SetAttribute("key", key);
                    nodeProperty.SetAttribute("type", "xml");
                    nodeProperty.InnerXml = value.OuterXml;
                    nodeRoot.AppendChild(nodeProperty);
                }
            }

            /// <summary>
            /// Loads the contents of this object into an XML file
            /// </summary>
            /// <param name="pathOs"></param>
            public void FromXml(string pathOs)
            {
                var xmlXbrlProperties = new XmlDocument();
                xmlXbrlProperties.Load(pathOs);

                foreach (XmlNode nodeProperty in xmlXbrlProperties.SelectNodes("/properties/property"))
                {
                    var key = nodeProperty.GetAttribute("key");
                    var type = nodeProperty.GetAttribute("type");
                    var value = (type == "xml") ? nodeProperty.InnerXml : nodeProperty.InnerText;

                    switch (key)
                    {
                        case "ClientName":
                            this.ClientName = value;
                            break;
                        case "Prefix":
                            this.Prefix = value;
                            break;
                        case "IsoTimestampPeriodEndDate":
                            this.IsoTimestampPeriodEndDate = value;
                            break;
                        case "EditorId":
                            this.EditorId = value;
                            break;
                        case "ReportingYear":
                            this.ReportingYear = Int32.Parse(value);
                            break;
                        case "AssetsPrefix":
                            this.AssetsPrefix = value;
                            break;
                        case "RootFolderPathOs":
                            this.RootFolderPathOs = value;
                            break;
                        case "InputFolderPathOs":
                            this.InputFolderPathOs = value;
                            break;
                        case "InputImagesFolderPathOs":
                            this.InputImagesFolderPathOs = value;
                            break;
                        case "WorkingFolderPathOs":
                            this.WorkingFolderPathOs = value;
                            break;
                        case "OutputFolderPathOs":
                            this.OutputFolderPathOs = value;
                            break;
                        case "OutputEdgarFolderPathOs":
                            this.OutputEdgarFolderPathOs = value;
                            break;
                        case "OutputIxbrlFolderPathOs":
                            this.OutputIxbrlFolderPathOs = value;
                            break;
                        case "OutputXbrlFolderPathOs":
                            this.OutputXbrlFolderPathOs = value;
                            break;
                        case "OutputWebsiteFolderPathOs":
                            this.OutputWebsiteFolderPathOs = value;
                            break;
                        case "OutputValidationFolderPathOs":
                            this.OutputValidationFolderPathOs = value;
                            break;
                        case "LogFolderPathOs":
                            this.LogFolderPathOs = value;
                            break;
                        case "ValidationPackageUri":
                            this.ValidationPackageUri = value;
                            break;
                        case "AccessToken":
                            this.AccessToken = value;
                            break;
                        case "Sections":
                            this.Sections = value;
                            break;
                        case "Mode":
                            this.Mode = value;
                            break;
                        case "XbrlPackagePathOs":
                            this.XbrlPackagePathOs = value;
                            break;
                        case "Channel":
                            this.Channel = value;
                            break;
                        case "RegulatorId":
                            this.RegulatorId = value;
                            break;
                        case "XmlXbrl":
                            this.XmlXbrl.LoadXml(value);
                            break;
                        case "XmlHierarchy":
                            this.XmlHierarchy.LoadXml(value);
                            break;
                        case "XmlClientAssets":
                            this.XmlClientAssets.LoadXml(value);
                            break;
                        case "_xmlLog":
                            this._xmlLog.LoadXml(value);
                            break;

                        default:
                            appLogger.LogWarning($"No support to parse {nodeProperty.OuterXml}");
                            break;
                    }

                }

            }


            /// <summary>
            /// Disposes the memory resources in use by this class instance
            /// </summary>
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Free up resources used by the instance of the class
            /// </summary>
            /// <param name="disposing"></param>
            protected virtual void Dispose(bool disposing)
            {
                if (_disposed)
                    return;

                if (disposing)
                {
                    //
                    // Free any other managed objects
                    //
                    this.ClientName = null;
                    this.Prefix = null;
                    this.EditorId = null;
                    this.ReportingYear = -1;
                    this.DebugMode = false;
                    this.AssetsPrefix = null;
                    this.RootFolderPathOs = null;
                    this.InputFolderPathOs = null;
                    this.InputImagesFolderPathOs = null;
                    this.WorkingFolderPathOs = null;
                    this.WorkingVisualsFolderPathOs = null;
                    this.OutputFolderPathOs = null;
                    this.OutputEdgarFolderPathOs = null;
                    this.OutputIxbrlFolderPathOs = null;
                    this.OutputXbrlFolderPathOs = null;
                    this.OutputWebsiteFolderPathOs = null;
                    this.OutputValidationFolderPathOs = null;
                    this.LogFolderPathOs = null;
                    this.ValidationPackageUri = null;
                    this.AccessToken = null;
                    this.Sections = null;
                    this.Mode = null;
                    this.Channel = null;
                    this.RegulatorId = null;
                    this.XbrlVars = null;
                    this.XmlTaxxor = null;
                    this.XmlXbrl = null;
                    this.XmlHierarchy = null;
                    this.XmlClientAssets = null;
                    this._xmlLog = null;
                    this.Exhibits = null;
                }

                _disposed = true;
            }

        }
    }
}