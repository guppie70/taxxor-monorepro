using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Amazon.S3;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WebMarkupMin.Core;

using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders a website for a project
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task GenerateWebsite(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            var useRandomOutputFolder = false;

            // Default settings for the website generator
            var copyWebsiteAssets = true;
            var copyGenericWebsiteAssets = true;
            var postProcessAssets = true;
            var renderWebsiteData = true;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var exportFormat = request.RetrievePostedValue("format");
            var renderPdfString = request.RetrievePostedValue("renderpdf", "(true|false)", true, reqVars.returnType);
            var renderPdfFiles = renderPdfString == "true";
            var useLoremIpsumString = request.RetrievePostedValue("loremipsum", "(true|false)", true, reqVars.returnType);
            var useLoremIpsum = useLoremIpsumString == "true";
            var xbrlZipReferencesString = request.RetrievePostedValue("addxbrlpackage", @"^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$|^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(,[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})*)$", false, reqVars.returnType);

            // Create a list of guids that refer to XBRL ZIP files stored in the Taxxor Document Store
            var xbrlZipGuids = new List<string>();
            if (!string.IsNullOrEmpty(xbrlZipReferencesString))
            {
                // Regular expression pattern to match GUIDs
                string pattern = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

                // Use Regex.Matches to find all matches of the pattern in the xbrlZipReferencesString
                MatchCollection matches = Regex.Matches(xbrlZipReferencesString, pattern);

                // Loop through the matches and add each GUID to the xbrlZipGuids list
                foreach (Match match in matches)
                {
                    xbrlZipGuids.Add(match.Value);
                }
            }


            var outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);

            var websiteRenderResult = new TaxxorReturnMessage();
            var tempFolderName = "";
            var tempFolderPathOs = "";

            try
            {
                switch (outputChannelType)
                {
                    case "htmlsite":
                        // The generic website

                        // Correct the output channel type
                        projectVars.outputChannelType = outputChannelType;
                        SetProjectVariables(context, projectVars);

                        websiteRenderResult = await RenderFullWebsite(context, projectVars.outputChannelVariantId, renderPdfFiles, useLoremIpsum, xbrlZipGuids);

                        // Payload contains the path where the website assets have been generated
                        if (websiteRenderResult.Success)
                        {
                            tempFolderPathOs = websiteRenderResult.Payload;
                            tempFolderName = Path.GetFileName(tempFolderPathOs);
                        }

                        break;

                    default:
                        // The Philips website...
                        tempFolderName = useRandomOutputFolder ? $"{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"website_{projectVars.projectId}-{projectVars.outputChannelVariantLanguage}";
                        tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";

                        websiteRenderResult = await RenderWebsiteAssets(tempFolderName, renderPdfFiles, copyWebsiteAssets, copyGenericWebsiteAssets, postProcessAssets, renderWebsiteData, useLoremIpsum);
                        break;

                }


                if (!websiteRenderResult.Success)
                {
                    appLogger.LogError($"{websiteRenderResult.Message} debugInfo: {websiteRenderResult.DebugInfo}");
                    HandleError(websiteRenderResult.Message, websiteRenderResult.DebugInfo);
                }
                else
                {
                    // => Potentially ZIP the content
                    var zipFilePathOs = $"{dataRootPathOs}/temp/{tempFolderName}.zip";
                    var s3BucketName = "";
                    if (exportFormat == "zip" || exportFormat == "website")
                    {
                        try
                        {
                            if (File.Exists(zipFilePathOs)) File.Delete(zipFilePathOs);

                            var cleanupResult = Extensions.CleanupWebsiteAssets($"{tempFolderPathOs}/publication");
                            if (!cleanupResult.Success) appLogger.LogWarning($"{cleanupResult.Message} debugInfo: {cleanupResult.DebugInfo}");

                            ZipFile.CreateFromDirectory(tempFolderPathOs, zipFilePathOs);
                        }
                        catch (Exception ex)
                        {
                            HandleError("Could not create ZIP package", $"tempFolderPathOs: {tempFolderPathOs}, zipFilePathOs: {zipFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                        }
                    }
                    else if (exportFormat == "awss3")
                    {
                        // tx-bucket-taxxor-voltana-htmlsite-en -> ( tx-bucket-taxxor-voltana-htmlsite-en-prod & tx-bucket-taxxor-voltana-htmlsite-en-prev )

                        // s3BucketName = $"tx-bucket-{projectVars.outputChannelType}-{TaxxorClientId}-{projectVars.guidLegalEntity}-{projectVars.outputChannelType}-{projectVars.outputChannelVariantLanguage}-{siteType}";
                        s3BucketName = $"tx-bucket-{projectVars.outputChannelType}-{TaxxorClientId}";
                        var s3KeyPrefix = $"{siteType}/{projectVars.guidLegalEntity}/{projectVars.projectId}/{projectVars.outputChannelVariantLanguage}";

                        try
                        {
                            // Create S3 client with proper credentials configuration and region
                            var s3Config = new AmazonS3Config
                            {
                                RegionEndpoint = Amazon.RegionEndpoint.EUCentral1 // Replace with the correct region where your bucket is located
                            };
                            var s3Client = new Amazon.S3.AmazonS3Client(s3Config);

                            // If AWS_PROFILE is set, use it to create the client with proper credentials
                            var awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
                            if (!string.IsNullOrEmpty(awsProfile))
                            {
                                // Replace obsolete StoredProfileAWSCredentials with recommended approach
                                var credentialsFile = new Amazon.Runtime.CredentialManagement.SharedCredentialsFile();
                                if (credentialsFile.TryGetProfile(awsProfile, out var profile))
                                {
                                    var credentialProfileChain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
                                    if (credentialProfileChain.TryGetAWSCredentials(awsProfile, out var awsCredentials))
                                    {
                                        s3Client = new Amazon.S3.AmazonS3Client(awsCredentials, s3Config);
                                    }
                                }
                            }

                            Console.WriteLine($"--- S3 sync from {tempFolderPathOs} to {s3BucketName}/{s3KeyPrefix} ---");

                            // Get list of all files in the temp folder
                            var directoryInfo = new DirectoryInfo(tempFolderPathOs);
                            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);

                            // Get list of existing objects in S3 with the given prefix
                            var listObjectsRequest = new Amazon.S3.Model.ListObjectsV2Request
                            {
                                BucketName = s3BucketName,
                                Prefix = s3KeyPrefix
                            };

                            var existingObjectsResponse = await s3Client.ListObjectsV2Async(listObjectsRequest);
                            var existingObjectKeys = existingObjectsResponse.S3Objects.Select(o => o.Key).ToList();
                            var uploadedObjectKeys = new List<string>();

                            // Upload all files in the temp folder to S3 in parallel
                            var uploadTasks = files.Select(async file =>
                            {
                                var relativePath = file.FullName.Substring(tempFolderPathOs.Length).TrimStart('/').TrimStart('\\');
                                var key = $"{s3KeyPrefix}/{relativePath.Replace('\\', '/')}";

                                // Upload the file
                                var putRequest = new Amazon.S3.Model.PutObjectRequest
                                {
                                    BucketName = s3BucketName,
                                    Key = key,
                                    FilePath = file.FullName
                                };

                                await s3Client.PutObjectAsync(putRequest);
                                return key;
                            }).ToList();

                            // Wait for all uploads to complete
                            await Task.WhenAll(uploadTasks);

                            // Collect all the uploaded keys
                            uploadedObjectKeys = uploadTasks.Select(t => t.Result).ToList();
                            int uploadCount = uploadedObjectKeys.Count;

                            // If --delete flag equivalent is needed, delete objects in S3 that are not in the local folder
                            var objectsToDelete = existingObjectKeys.Where(key =>
                                key.StartsWith(s3KeyPrefix) &&
                                !uploadedObjectKeys.Contains(key)).ToList();

                            if (objectsToDelete.Any())
                            {
                                var deleteRequest = new Amazon.S3.Model.DeleteObjectsRequest
                                {
                                    BucketName = s3BucketName,
                                    Objects = objectsToDelete.Select(key => new Amazon.S3.Model.KeyVersion { Key = key }).ToList()
                                };

                                await s3Client.DeleteObjectsAsync(deleteRequest);
                                Console.WriteLine($"Deleted {objectsToDelete.Count} objects from S3 that were not in the local folder");
                            }

                            Console.WriteLine($"Successfully uploaded {uploadCount} files to S3 (target: s3://{s3BucketName}/{s3KeyPrefix})");
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Error during S3 upload");
                            HandleError("Error during S3 upload", $"Error: {ex.Message}, Stack trace: {ex.StackTrace}");
                        }
                    }

                    // HTML website path
                    var htmlWebsitePath = "";
                    if (exportFormat == "awss3")
                    {
                        htmlWebsitePath = s3BucketName;
                        var nodeWebsiteAssetsPrefix = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='websiteassetsprefix']");
                        if (nodeWebsiteAssetsPrefix != null)
                        {
                            var websiteAssetsPrefix = nodeWebsiteAssetsPrefix.InnerText;
                            if (!string.IsNullOrEmpty(websiteAssetsPrefix))
                            {
                                if (websiteAssetsPrefix.Contains("{sitelang}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{sitelang}", projectVars.outputChannelVariantLanguage);
                                if (websiteAssetsPrefix.Contains("{projectid}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{projectid}", projectVars.projectId);
                                if (websiteAssetsPrefix.Contains("{guidlegalentity}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{guidlegalentity}", projectVars.guidLegalEntity);
                                htmlWebsitePath = $"{websiteAssetsPrefix}/";
                            }
                        }
                    }


                    // Construct a response message for the client
                    dynamic jsonData = new ExpandoObject();
                    jsonData.result = new ExpandoObject();
                    jsonData.result.message = "Successfully generated website content";
                    jsonData.result.filepackage = (exportFormat == "awss3") ? htmlWebsitePath : Path.GetFileName(zipFilePathOs);
                    if (debugRoutine)
                    {
                        jsonData.result.debuginfo = $"";
                    }

                    // Convert to JSON and return it to the client
                    string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

                    await response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new TaxxorReturnMessage(false, "Error generating website", $"error: {ex}");
                await response.Error(errorResponse, ReturnTypeEnum.Json, true);
            }




        }

        /// <summary>
        /// Exports a generated website to a remote location (called from the Publication Controller)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ExportWebsite(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            var useRandomOutputFolder = false;
            var copyWebsiteAssets = true;
            var copyGenericWebsiteAssets = true;
            var postProcessAssets = true;
            var renderWebsiteData = true;

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var callbackUrl = request.RetrievePostedValue("callbackUrl", RegexEnum.Uri, false, reqVars.returnType);
            var remoteUri = request.RetrievePostedValue("remoteUri", RegexEnum.Uri, true, reqVars.returnType);
            var renderPdfString = request.RetrievePostedValue("renderpdf", "(true|false)", true, reqVars.returnType);
            var renderPdfFiles = renderPdfString == "true";
            var useLoremIpsumString = request.RetrievePostedValue("loremipsum", "(true|false)", true, reqVars.returnType);
            var useLoremIpsum = useLoremIpsumString == "true";

            // Generic variables
            var tempFolderName = useRandomOutputFolder ? $"{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"website_{projectVars.projectId}-{projectVars.outputChannelVariantLanguage}";
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";
            var asyncProcessing = string.IsNullOrEmpty(callbackUrl) ? false : true;

            // Complete the project variables object so that we can use it when generating the website
            projectVars.editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
            projectVars.editorContentType = "regular";
            projectVars.reportTypeId = RetrieveReportTypeIdFromProjectId(projectVars.projectId);
            projectVars.outputChannelType = "website";
            projectVars.versionId = "1";
            var xPathForOutputChannelVariantId = $"/configuration/editors/editor[@id={GenerateEscapedXPathString(projectVars.editorId)}]/output_channels/output_channel[@type={GenerateEscapedXPathString(projectVars.outputChannelType)}]/variants/variant[@lang={GenerateEscapedXPathString(projectVars.outputChannelVariantLanguage)}]/@id";
            appLogger.LogInformation($"- xPathForOutputChannelVariantId: {xPathForOutputChannelVariantId}");

            projectVars.outputChannelVariantId = RetrieveAttributeValueIfExists(xPathForOutputChannelVariantId, xmlApplicationConfiguration);

            if (debugRoutine)
            {
                appLogger.LogInformation("-------- ProjectVariables Properties --------");
                Console.WriteLine(GenerateDebugObjectString(projectVars, ReturnTypeEnum.Txt));
                Console.WriteLine("");
            }

            // Store the enriched project variables object in the context so that we can use it furtheron in the code
            SetProjectVariables(context, projectVars);


            // Return a basic message to the calling webservice to indacate that we have received the job and will process it accordingly
            if (asyncProcessing)
            {
                dynamic jsonData = new ExpandoObject();
                jsonData.result = new ExpandoObject();
                jsonData.result.message = "Successfully received the request for rendering the website content";
                if (debugRoutine)
                {
                    jsonData.result.debuginfo = $"callbackUrl: {callbackUrl}, remoteUri: {remoteUri}";
                }

                // Convert to JSON and return it to the client
                string jsonToReturn = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);
                await context.Response.OK(jsonToReturn, ReturnTypeEnum.Json, true);
            }

            // Render the website assets
            var websiteRenderResult = await RenderWebsiteAssets(tempFolderName, renderPdfFiles, copyWebsiteAssets, copyGenericWebsiteAssets, postProcessAssets, renderWebsiteData, useLoremIpsum);



            // Compile the message that we want to return
            var xmlResponseDataForClient = new XmlDocument();
            if (websiteRenderResult.Success)
            {
                xmlResponseDataForClient = GenerateSuccessXml("Successfully rendered the website assets", $"callbackUrl: {callbackUrl}, remoteUri: {remoteUri}");

                // Export the assets to a remote location
                var exportWebsiteAssetsResult = await Extensions.ExportWebsiteContent(context, tempFolderPathOs, remoteUri);

                // Handle the result
                if (exportWebsiteAssetsResult.Success)
                {
                    xmlResponseDataForClient = GenerateSuccessXml("Successfully rendered and exported the website assets", $"callbackUrl: {callbackUrl}, remoteUri: {remoteUri}");
                }
                else
                {
                    xmlResponseDataForClient = GenerateErrorXml(exportWebsiteAssetsResult.Message, exportWebsiteAssetsResult.DebugInfo);
                }
            }
            else
            {
                xmlResponseDataForClient = GenerateErrorXml(websiteRenderResult.Message, websiteRenderResult.DebugInfo);
            }

            // Handle processing done
            if (asyncProcessing)
            {
                // Return a message to the callback URL indicating that we are done with the export
                var customHeaders = new CustomHttpHeaders
                {
                    RequestType = ReturnTypeEnum.Json
                };
                customHeaders.AddTaxxorUserInformation(projectVars.currentUser.Id);

                if (UriLogEnabled)
                {
                    if (!UriLogBackend.Contains(callbackUrl)) UriLogBackend.Add(callbackUrl);
                }

                var callbackResult = await RestRequest<string>(RequestMethodEnum.Post, callbackUrl, ConvertToJson(xmlResponseDataForClient), customHeaders, 10000);
                Console.WriteLine("-------- Callback result ---------");
                Console.WriteLine(callbackResult);
                Console.WriteLine("----------------------------------");
            }
            else
            {
                // Render a message to the client
                if (websiteRenderResult.Success)
                {
                    await context.Response.OK(xmlResponseDataForClient, ReturnTypeEnum.Json, true);
                }
                else
                {
                    HandleError(xmlResponseDataForClient);
                }
            }
        }

        public async static Task RenderSingleHtmlSiteWebPageForEditor(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            var debugRoutine = siteType == "local" || siteType == "dev";

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var useLoremIpsumString = request.RetrievePostedValue("loremipsum", "(true|false)", false, reqVars.returnType, "false");
            var useLoremIpsum = useLoremIpsumString == "true";


            var errorMessage = string.Empty;
            try
            {

                var nodeListWebsiteVariants = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='htmlsite']/variants/variant");
                if (nodeListWebsiteVariants.Count == 0) throw new Exception("No website hierarchies found");

                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);
                XmlDocument xmlWebsiteHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                var dataReference = xmlWebsiteHierarchy.SelectSingleNode($"/items/structured//item[@id='{projectVars.did}']")?.GetAttribute("data-ref") ?? "unknown";
                // Console.WriteLine(PrettyPrintXml(xmlWebsiteHierarchy));
                Console.WriteLine($"- dataReference: {dataReference}");

                var (Success, Html, ErrorMessage) = await RenderSingleWebPage(projectVars, xmlWebsiteHierarchy, projectVars.did);

                var responseToClient = "";
                if (Success)
                {
                    responseToClient = Html;
                }
                else
                {
                    responseToClient = $@"
                <html>
                    <head>
                        <title>HTML Site Page Generation</title>
                    </head>
                    <body>
                        <h1>Error Generating HTML Site Page</h1>
                        <p>{ErrorMessage}</p>
                    </body>
                </html>";
                }

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                errorMessage = $"An error occurred while rendering the single HTML site webpage ({projectVars.did})";
                appLogger.LogError(ex, errorMessage);

                HandleError(errorMessage, ex.ToString());
            }





        }


        /// <summary>
        /// Logic to compile a set of website assets
        /// </summary>
        /// <param name="tempFolderName"></param>
        /// <param name="renderPdfFiles">Render the PDF files that are associated with this project output channel</param>
        /// <param name="copyWebsiteAssets">Fill the zip (or output directory) with the images and downloads which are assiocated with the website</param>
        /// <param name="copyGenericWebsiteAssets">Fill the zip (or output directory) with the generic assets (JavaScript, CSS, etc.) needed to display the website</param>
        /// <param name="postProcessAssets">Mimimizes HTML output, etc.</param>
        /// <param name="renderWebsiteData">Renders JSON files and other related data for the web page</param>
        /// <param name="useLoremIpsum">Render the web page and PDF documents using fake text data</param>
        /// <param name="cleanOutputDir"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RenderWebsiteAssets(string tempFolderName, bool renderPdfFiles = true, bool copyWebsiteAssets = true, bool copyGenericWebsiteAssets = true, bool postProcessAssets = true, bool renderWebsiteData = true, bool useLoremIpsum = false, bool cleanOutputDir = true)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Assure that we have a valid stylesheet cache
            SetupPdfStylesheetCache(reqVars);

            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";

            var placeholderFilename = "_nix.txt";
            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);

            var resultFileStructureDefinitionXpath = RetrieveFileStructureXpath(projectVars.editorId);
            if (!resultFileStructureDefinitionXpath.Success)
            {
                appLogger.LogWarning($"{resultFileStructureDefinitionXpath.Message}, debuginfo: {resultFileStructureDefinitionXpath.DebugInfo}, stack-trace: {GetStackTrace()}");
                return resultFileStructureDefinitionXpath;
            }

            // Retrieve the information about the website assets location
            var genericWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='generic']/@name", xmlApplicationConfiguration);
            var htmlWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='htmlfiles']/@name", xmlApplicationConfiguration);
            var imagesWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='images']/@name", xmlApplicationConfiguration);
            var dataWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='data']/@name", xmlApplicationConfiguration);
            var downloadsWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='downloads']/@name", xmlApplicationConfiguration);
            var pdfWebsiteAssetsFolderPath = RetrieveAttributeValueIfExists($"{resultFileStructureDefinitionXpath.Payload}[@content='pdf']/@name", xmlApplicationConfiguration);
            var projectLanguages = RetrieveProjectLanguages(editorId);
            var reportingPeriod = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_period", xmlApplicationConfiguration);

            // Pre process error handling
            if (reportingPeriod == null)
            {
                HandleError("No reporting period defined for this project", $"projectVars.projectId: {projectVars.projectId}, stack-trace: {GetStackTrace()}");
            }

            if (genericWebsiteAssetsFolderPath == null || htmlWebsiteAssetsFolderPath == null || imagesWebsiteAssetsFolderPath == null || dataWebsiteAssetsFolderPath == null || downloadsWebsiteAssetsFolderPath == null || pdfWebsiteAssetsFolderPath == null)
            {
                HandleError("Could not locate all export paths for website generator", $"websiteAssetsBaseXpath: {resultFileStructureDefinitionXpath.Payload}, stack-trace: {GetStackTrace()}");
            }

            // Replace placeholders
            // genericWebsiteAssetsFolderPath = genericWebsiteAssetsFolderPath.Replace("[language]", projectVars.outputChannelVariantLanguage);
            // htmlWebsiteAssetsFolderPath = htmlWebsiteAssetsFolderPath.Replace("[language]", projectVars.outputChannelVariantLanguage);
            // imagesWebsiteAssetsFolderPath = imagesWebsiteAssetsFolderPath.Replace("[language]", projectVars.outputChannelVariantLanguage);
            // downloadsWebsiteAssetsFolderPath = downloadsWebsiteAssetsFolderPath.Replace("[language]", projectVars.outputChannelVariantLanguage);
            // //pdfWebsiteAssetsFolderPath = pdfWebsiteAssetsFolderPath.Replace("[language]", projectVars.outputChannelVariantLanguage);

            // Calculate full OS path type of variables
            var genericWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{genericWebsiteAssetsFolderPath}";
            var htmlWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{htmlWebsiteAssetsFolderPath}";
            var imagesWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{imagesWebsiteAssetsFolderPath}";
            var dataWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{dataWebsiteAssetsFolderPath}";
            var downloadsWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{downloadsWebsiteAssetsFolderPath}";
            var pdfWebsiteAssetsFolderPathOs = $"{tempFolderPathOs}/{pdfWebsiteAssetsFolderPath}";

            // => Setup folder structure for package
            try
            {
                if (Directory.Exists(tempFolderPathOs) && cleanOutputDir) DelTree(tempFolderPathOs);

                Directory.CreateDirectory(tempFolderPathOs);

                foreach (string projectLanguage in projectLanguages)
                {
                    if (copyGenericWebsiteAssets && !Directory.Exists(genericWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage))) Directory.CreateDirectory(genericWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage));
                    if (!Directory.Exists(htmlWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage))) Directory.CreateDirectory(htmlWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage));
                    if (!Directory.Exists(imagesWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage))) Directory.CreateDirectory(imagesWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage));
                    if (!Directory.Exists(downloadsWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage)))
                    {
                        Directory.CreateDirectory(downloadsWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage));
                        await TextFileCreateAsync("lorem ipsum", $"{downloadsWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage)}/{placeholderFilename}");
                    }
                    if (!Directory.Exists(dataWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage)))
                    {
                        Directory.CreateDirectory(dataWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage));
                        await TextFileCreateAsync("lorem ipsum", $"{dataWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage)}/{placeholderFilename}");
                    }
                    if (renderPdfFiles)
                    {
                        if (!Directory.Exists(pdfWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage))) Directory.CreateDirectory(pdfWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage));
                    }
                }
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "Could not setup folder structure for website generator", $"error: {ex}, stack-trace: {GetStackTrace()}");
            }
            await _outputGenerationProgressMessage($"Successfully setup directory structure");

            // => Generate PDF files
            if (renderPdfFiles)
            {
                var pdfGenerationReult = await GeneratePdfFiles(projectVars, pdfWebsiteAssetsFolderPathOs, false);
                if (pdfGenerationReult.Success)
                {
                    await _outputGenerationProgressMessage(pdfGenerationReult.Message);
                }
                else
                {
                    await _outputGenerationProgressMessage($"ERROR: {pdfGenerationReult.Message}");
                }
            }



            // => Grab all website assets and move into folder structure taxxoreditorwebsitegeneratorexportassets
            if (copyWebsiteAssets)
            {
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", projectVars.projectId },
                    { "vid", projectVars.versionId },
                    { "ctype", projectVars.editorContentType },
                    { "rtype", projectVars.reportTypeId },
                    { "octype", projectVars.outputChannelType },
                    { "ocvariantid", projectVars.outputChannelVariantId },
                    { "oclang", projectVars.outputChannelVariantLanguage },
                    { "tempfoldername", tempFolderName },
                    { "htmlassets", htmlWebsiteAssetsFolderPath },
                    { "imageassets", imagesWebsiteAssetsFolderPath },
                    { "downloadassets", downloadsWebsiteAssetsFolderPath }
                };


                XmlDocument responseXml = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.DocumentStore, RequestMethodEnum.Post, "taxxoreditorwebsitegeneratorexportassets", dataToPost, debugRoutine);

                if (XmlContainsError(responseXml))
                {
                    return new TaxxorReturnMessage(false, "There was an error copying the website assets", $"responseXml: {responseXml.OuterXml}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    await _outputGenerationProgressMessage($"Successfully copied website assets");
                }
            }

            // => Render the data used on the website
            if (renderWebsiteData)
            {
                var dataRenderResult = Extensions.RenderWebsiteData(context, htmlWebsiteAssetsFolderPathOs, dataWebsiteAssetsFolderPathOs.Replace("/[language]", ""));
                if (dataRenderResult.Success)
                {
                    foreach (string projectLanguage in projectLanguages)
                    {
                        var currentDataDirectoryPathOs = dataWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage);

                        var dataLanguageRenderResult = Extensions.RenderWebsiteDataForLanguage(context, htmlWebsiteAssetsFolderPathOs, currentDataDirectoryPathOs, projectLanguage, renderPdfFiles);
                        if (!dataLanguageRenderResult.Success)
                        {
                            return dataLanguageRenderResult;
                        }
                    }
                }
                else
                {
                    return dataRenderResult;
                }
                await _outputGenerationProgressMessage($"Successfully rendered website data");
            }

            // => Add generic assets (JS, CSS, etc.)
            if (copyGenericWebsiteAssets)
            {
                try
                {
                    CopyDirectoryRecursive($"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/website", genericWebsiteAssetsFolderPathOs, true);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "There was an errror copying the generic website assets", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }
                await _outputGenerationProgressMessage($"Successfully copied website assets");
            }



            // => Post process website assets
            if (postProcessAssets)
            {
                // - Convert XML into XHTML files
                foreach (string projectLanguage in projectLanguages)
                {
                    var currentHtmlWebsiteAssetsFolderPathOs = htmlWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage);
                    if (projectLanguage == projectVars.outputChannelVariantLanguage)
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(currentHtmlWebsiteAssetsFolderPathOs);
                        List<FileInfo> xmlDataFiles = dirInfo.GetFiles("*.xml").ToList();

                        // Extract the HTML of the current language
                        foreach (FileInfo xmlFileInfo in xmlDataFiles)
                        {
                            var xmlDataFilePathOs = xmlFileInfo.FullName;
                            Console.WriteLine($"- xmlDataFilePathOs: {xmlDataFilePathOs}");
                            var xhtmlFilePathOs = Path.GetDirectoryName(xmlDataFilePathOs) + "/" + Extensions.RenderHtmlFileNameForWebsite(context, Path.GetFileName(xmlDataFilePathOs));
                            await _outputGenerationProgressMessage($"Start rendering {Path.GetFileName(xhtmlFilePathOs)}");

                            try
                            {
                                // Load the raw data file from the Taxxor Document Store
                                var xmlDataFile = new XmlDocument();
                                xmlDataFile.Load(xmlDataFilePathOs);

                                // Preprocess website xml before transforming it to XHTML
                                var xmlPreProcessed = await Extensions.PreprocessWebsiteXmlDatafile(context, xmlDataFile, xmlDataFilePathOs, tempFolderPathOs, projectLanguage, useLoremIpsum);
                                if (XmlContainsError(xmlPreProcessed))
                                {
                                    return new TaxxorReturnMessage(xmlPreProcessed);
                                }

                                // Load the preprocessed XML in the object
                                xmlDataFile.LoadXml(xmlPreProcessed.OuterXml);

                                // Generate the XHTML version
                                var xhtmlFileContent = await Extensions.RenderXhtmlContent(context, xmlDataFile, xmlDataFilePathOs, projectLanguage, renderPdfFiles);

                                // Return an error when the conversion failed
                                if (XmlContainsError(xhtmlFileContent)) HandleError(xhtmlFileContent);

                                // Store the post processed file
                                if (siteType == "local" || siteType == "dev")
                                {
                                    // Store the full version of the HTML
                                    await xhtmlFileContent.SaveAsync(xhtmlFilePathOs);
                                }
                                else
                                {
                                    // Store the minified version
                                    string xhtmlMinified = "";
                                    bool errorInMinification = false;
                                    if (xhtmlFileContent.OuterXml.Contains(" xmlns=\"http://www.w3.org/1999/xhtml\""))
                                    {
                                        var minifier = new XhtmlMinifier(new XhtmlMinificationSettings(true));

                                        MarkupMinificationResult minificationResult = minifier.Minify(xhtmlFileContent.OuterXml);
                                        xhtmlMinified = minificationResult.MinifiedContent;
                                        IList<MinificationErrorInfo> minificationWarnings = minificationResult.Warnings;
                                        foreach (MinificationErrorInfo minificationError in minificationWarnings)
                                        {
                                            errorInMinification = true;
                                            appLogger.LogWarning(minificationError.ToString());
                                        }


                                    }
                                    else
                                    {
                                        var minifier = new HtmlMinifier(new HtmlMinificationSettings(true));

                                        MarkupMinificationResult minificationResult = minifier.Minify(xhtmlFileContent.OuterXml);
                                        xhtmlMinified = minificationResult.MinifiedContent;
                                        IList<MinificationErrorInfo> minificationWarnings = minificationResult.Warnings;
                                        foreach (MinificationErrorInfo minificationError in minificationWarnings)
                                        {
                                            errorInMinification = true;
                                            appLogger.LogWarning(minificationError.ToString());
                                        }
                                    }

                                    if (errorInMinification)
                                    {
                                        appLogger.LogError($"There were errors minifying the (X)HTML content so we are saving the full version");
                                        // Store the full version of the HTML
                                        await xhtmlFileContent.SaveAsync(xhtmlFilePathOs);
                                    }
                                    else
                                    {
                                        await TextFileCreateAsync(xhtmlMinified, xhtmlFilePathOs);
                                    }
                                }

                                await _outputGenerationProgressMessage($"Successfully rendered {Path.GetFileName(xhtmlFilePathOs)}");

                            }
                            catch (Exception ex)
                            {
                                var errorMessage = "Unable to process HTML files";
                                appLogger.LogError(ex, errorMessage);
                                return new TaxxorReturnMessage(false, errorMessage, $"xmlDataFilePathOs: {xmlDataFilePathOs}, error: {ex}, stack-trace: {GetStackTrace()}");
                            }

                        }
                    }

                }


                // Remove the data files as we have successfully processed them
                foreach (string projectLanguage in projectLanguages)
                {
                    var currentHtmlWebsiteAssetsFolderPathOs = htmlWebsiteAssetsFolderPathOs.Replace("[language]", projectLanguage);
                    if (projectLanguage == projectVars.outputChannelVariantLanguage)
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(currentHtmlWebsiteAssetsFolderPathOs);
                        List<FileInfo> xmlDataFiles = dirInfo.GetFiles("*.xml").ToList();
                        foreach (FileInfo xmlFileInfo in xmlDataFiles)
                        {
                            try
                            {
                                File.Delete(xmlFileInfo.FullName);
                            }
                            catch (Exception ex)
                            {
                                return new TaxxorReturnMessage(false, "Could not delete xml data file", $"path: {xmlFileInfo.FullName}, error: {ex}");
                            }

                        }
                    }
                }




            }

            // Everything successfully generated
            return new TaxxorReturnMessage(true, "Successfully generated website assets", $"tempFolderPathOs: {tempFolderPathOs}");

        }



        /// <summary>
        /// Renders a "standard" HTML website
        /// </summary>
        /// <param name="context"></param>
        /// <param name="outputChannelVariantId"></param>
        /// <param name="renderPdfFiles"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> RenderFullWebsite(HttpContext context, string outputChannelVariantId, bool renderPdfFiles, bool useLoremIpsum, List<string> xbrlZipFileGuids)
        {
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = reqVars.isDebugMode == true || siteType == "local" || siteType == "dev";
            var useRandomOutputFolder = siteType == "prev" || siteType == "prod";

            var renderVisuals = true;



            // Hard coded here because we are generating fixed for annual report
            var outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, outputChannelVariantId);
            var outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectVars.projectId, outputChannelVariantId);


            var timeStamp = createIsoTimestamp();
            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);

            var resultFileStructureDefinitionXpath = RetrieveFileStructureXpath(projectVars.editorId);
            if (!resultFileStructureDefinitionXpath.Success)
            {
                appLogger.LogWarning($"{resultFileStructureDefinitionXpath.Message}, debuginfo: {resultFileStructureDefinitionXpath.DebugInfo}, stack-trace: {GetStackTrace()}");
                return resultFileStructureDefinitionXpath;
            }

            var reportingPeriod = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_period", xmlApplicationConfiguration);
            var siteVersionType = reportingPeriod.StartsWith("q") ? "qr" : "ar";

            // Path that we will be using for generating the assets
            var tempFolderName = useRandomOutputFolder ? $"htmlsite_{CreateTimeBasedString("millisecond")}{RandomString(8, false)}" : $"htmlsite_{projectVars.projectId}-{outputChannelVariantLanguage}";
            var tempFolderPathOs = $"{RetrieveSharedFolderPathOs()}/temp/{tempFolderName}";
            Console.WriteLine($"- tempFolderPathOs: {tempFolderPathOs}");



            //
            // => Setup the XBRL Generation Properties object that we will be using throughout the process
            //
            var xbrlConversionProperties = new XbrlConversionProperties(tempFolderPathOs);
            xbrlConversionProperties.ResetLog();
            xbrlConversionProperties.DebugMode = debugRoutine;
            xbrlConversionProperties.Channel = "WEB";
            xbrlConversionProperties.ClientName = TaxxorClientName;
            xbrlConversionProperties.Prefix = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='prefix']")?.InnerText ?? "";
            xbrlConversionProperties.ReportingYear = Convert.ToInt32(RegExpReplace(@"^..(..)$", reportingPeriod, "20$1"));

            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(RetrieveEditorIdFromProjectId(projectVars.projectId), "htmlsite", outputChannelVariantId, outputChannelVariantLanguage);
            if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
            {
                xbrlConversionProperties.XmlHierarchy.LoadXml(projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml.OuterXml);
            }
            else
            {
                return new TaxxorReturnMessage(false, "Unable to retrieve hierarchy for HTML website", $"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}");
            }

            xbrlConversionProperties.Sections = "all";

            // Fill the project variables passed to this function
            xbrlConversionProperties.XbrlVars.projectId = projectVars.projectId;
            xbrlConversionProperties.XbrlVars.versionId = projectVars.versionId;

            // Fill with variables retrieved from the context    
            xbrlConversionProperties.XbrlVars.editorContentType = projectVars.editorContentType ?? "regular";
            xbrlConversionProperties.XbrlVars.reportTypeId = projectVars.reportTypeId ?? RetrieveReportTypeIdFromProjectId(projectVars.projectId);
            xbrlConversionProperties.XbrlVars.outputChannelType = outputChannelType;
            xbrlConversionProperties.XbrlVars.editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
            xbrlConversionProperties.XbrlVars.outputChannelVariantId = outputChannelVariantId;
            xbrlConversionProperties.XbrlVars.outputChannelVariantLanguage = outputChannelVariantLanguage;
            xbrlConversionProperties.XbrlVars.projectRootPath = projectVars.projectRootPath;
            xbrlConversionProperties.XbrlVars.guidLegalEntity = projectVars.guidLegalEntity;

            // Retrieve the client-side assets (JS and CSS)
            xbrlConversionProperties.XmlClientAssets = Extensions.RenderOutputChannelCssAndJs(xbrlConversionProperties.XbrlVars, xbrlConversionProperties.XbrlVars.outputChannelType);


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
                        xbrlConversionProperties.InputFolderPathOs,
                        xbrlConversionProperties.InputImagesFolderPathOs,
                        xbrlConversionProperties.WorkingFolderPathOs,
                        xbrlConversionProperties.WorkingVisualsFolderPathOs,
                        xbrlConversionProperties.OutputFolderPathOs,
                        xbrlConversionProperties.OutputWebsiteFolderPathOs,
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


                    // //
                    // // => Prepare the visuals in the document
                    // //
                    // var visualsPreparationResult = await PrepareVisualsInTaxxorBaseDocument(xbrlConversionProperties, debugRoutine);
                    // await _handleProcessStepResult(xbrlConversionProperties, visualsPreparationResult, "image-prepare");
                }


                //
                // => Prepare the site structure hierarchy so that it includes paths to the website HTML files
                //
                var xmlWebsiteHierarchy = xbrlConversionProperties.XmlHierarchy;
                var nodeListItems = xmlWebsiteHierarchy.SelectNodes("/items/structured/item/sub_items//item");
                var isFirstItem = true;
                foreach (XmlNode nodeItem in nodeListItems)
                {
                    List<string> pathList = [];
                    var nodeListAncesors = nodeItem.SelectNodes("ancestor::item");
                    bool isFirstIteration = true;
                    foreach (XmlNode nodeAncestor in nodeListAncesors)
                    {
                        if (isFirstIteration)
                        {
                            isFirstIteration = false;
                            continue;
                        }
                        pathList.Add(NormalizeFileNameWithoutExtension(nodeAncestor.SelectSingleNode("web_page/linkname")?.InnerText ?? ""));
                    }
                    pathList.Add(NormalizeFileNameWithoutExtension(nodeItem.SelectSingleNode("web_page/linkname")?.InnerText ?? "") + ".html");

                    // Set the path for the HTML file in the hierarchy item
                    if (isFirstItem)
                    {
                        isFirstItem = false;
                        nodeItem.SelectSingleNode("web_page/path").InnerText = $"/index.html";
                    }
                    else
                    {
                        nodeItem.SelectSingleNode("web_page/path").InnerText = $"/{string.Join("/", pathList)}";
                    }

                    // Find the metadata node
                    var dataRef = nodeItem.GetAttribute("data-ref") ?? "unknown";
                    var nodeItemMetadata = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectVars.projectId}']/data/content[@ref='{dataRef}']");
                    if (nodeItemMetadata != null)
                    {
                        // Add article ID to resolve links
                        nodeItem.SetAttribute("articleid", RetrieveArticleIdFromMetadataCache(nodeItemMetadata, outputChannelVariantLanguage) ?? "unkown");
                    }
                }
                // Console.WriteLine(PrettyPrintXml(xmlWebsiteHierarchy.OuterXml));

                //
                // => Render the HTML pages of the website
                //

                // Determine the paths of the XSLT's used for generating the website pages (part of the report design package)
                var pageXsltPathOs = RenderHtmlSiteWebPageXsltPathOs(xbrlConversionProperties.XbrlVars);
                var menuXsltPathOs = RenderHtmlSiteMenuXsltPathOs(xbrlConversionProperties.XbrlVars);

                // Contains paths to images found in the content
                Dictionary<string, string> imagePaths = [];

                nodeListItems = xmlWebsiteHierarchy.SelectNodes("/items/structured/item/sub_items//item");
                foreach (XmlNode nodeItem in nodeListItems)
                {
                    var path = nodeItem.SelectSingleNode("web_page/path")?.InnerText ?? "";
                    var linkName = nodeItem.SelectSingleNode("web_page/linkname")?.InnerText ?? "";
                    var itemId = nodeItem.GetAttribute("id") ?? "";
                    var dataRef = nodeItem.GetAttribute("data-ref") ?? "";

                    if (path == "")
                    {
                        appLogger.LogWarning($"Could not locate path for item {nodeItem.OuterXml}");
                        continue;
                    }

                    if (itemId == "")
                    {
                        appLogger.LogWarning($"Could not locate item id for item {nodeItem.OuterXml}");
                        continue;
                    }

                    if (dataRef == "")
                    {
                        appLogger.LogWarning($"Could not locate data-ref for item {nodeItem.OuterXml}");
                        continue;
                    }



                    //
                    // => Retrieve the content of the section
                    //
                    var xmlFilingContent = await Extensions.RetrieveFilingComposerXmlData(xbrlConversionProperties.XbrlVars, dataRef);
                    // appLogger.LogInformation($"Retrieved {dataRef} for {xbrlConversionProperties.XbrlVars.outputChannelVariantLanguage}:\n{TruncateString(PrettyPrintXml(xmlFilingContent), 1000)}");
                    if (xmlFilingContent.DocumentElement != null)
                    {
                        if (useLoremIpsum)
                        {

                            var xmlPdfHtmlLoremIpsum = await ConvertToLoremIpsum(xmlFilingContent, outputChannelVariantLanguage);

                            // Message to the client
                            await _outputGenerationProgressMessage("Converted website content to fake (lorem ipsum) text");

                            // Remove the XML declaration
                            if (xmlPdfHtmlLoremIpsum.FirstChild.NodeType == XmlNodeType.XmlDeclaration) xmlPdfHtmlLoremIpsum.RemoveChild(xmlPdfHtmlLoremIpsum.FirstChild);

                            xmlFilingContent.LoadXml(xmlPdfHtmlLoremIpsum.OuterXml);
                        }

                        //
                        // => Check if we can locate any images in the content
                        //
                        var nodeListImages = xmlFilingContent.SelectNodes("//img");
                        foreach (XmlNode nodeImage in nodeListImages)
                        {
                            var originalImagePath = nodeImage.GetAttribute("src") ?? "";
                            if (!string.IsNullOrEmpty(originalImagePath))
                            {
                                appLogger.LogInformation($"Found image {originalImagePath}");
                            }
                            if (originalImagePath.Contains("dataserviceassets"))
                            {
                                var normalizedPath = RegExpReplace(@"/dataserviceassets/(.*?)/images(.*)", originalImagePath, "$2");
                                var sourceImagePathOs = $"{xbrlConversionProperties.InputFolderPathOs}/publication/imgs{normalizedPath}";
                                var targetImagePathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/imgs{normalizedPath}";
                                if (File.Exists(sourceImagePathOs))
                                {
                                    nodeImage.SetAttribute("src", $"/imgs{normalizedPath}");
                                    try
                                    {
                                        if (!imagePaths.ContainsKey(sourceImagePathOs))
                                        {
                                            imagePaths.Add(sourceImagePathOs, targetImagePathOs);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        appLogger.LogError(ex, $"Error while trying to add image with key '{sourceImagePathOs}' and testing for '/imgs{normalizedPath}'");
                                    }
                                }
                                else
                                {
                                    appLogger.LogWarning($"Could not locate image {sourceImagePathOs}");
                                    continue;
                                }
                            }
                        }

                        //
                        // Render the menu for this page
                        //
                        XsltArgumentList xsltArgsMenu = new();
                        xsltArgsMenu.AddParam("current-item-id", "", itemId);
                        var htmlMenu = TransformXml(xmlWebsiteHierarchy, menuXsltPathOs, xsltArgsMenu);


                        //
                        // Render the HTML for this page
                        //
                        XsltArgumentList xsltArgsPage = new();
                        xsltArgsPage.AddParam("hierarchy-title", "", linkName);
                        var xmlPage = TransformXmlToDocument(xmlFilingContent, pageXsltPathOs, xsltArgsPage);

                        //
                        // => Resolve links to other HTML pages
                        //
                        var nodeListLinks = xmlPage.SelectNodes("//a[starts-with(@href, '#')]");
                        if (nodeListLinks.Count > 0)
                        {
                            Console.WriteLine($"Web page {dataRef} contains {nodeListLinks.Count} links to other pages");
                            foreach (XmlNode nodeLink in nodeListLinks)
                            {
                                // Retrieve the href attribute and remove the leading #
                                var href = nodeLink.GetAttribute("href");
                                var articleId = href.Substring(1);
                                var nodeTargetItem = xmlWebsiteHierarchy.SelectSingleNode($"//items/structured//item[@articleid='{articleId}']");
                                if (nodeTargetItem != null)
                                {
                                    var targetPath = nodeTargetItem.SelectSingleNode("web_page/path")?.InnerText ?? "";
                                    if (targetPath != "")
                                    {
                                        nodeLink.SetAttribute("href", targetPath);
                                        appLogger.LogInformation($"Resolved web page link '{href}' to target path '{targetPath}'");
                                    }
                                    else
                                    {
                                        appLogger.LogWarning($"Could not find path for target item for link '{href}' with articleid '{articleId}' in website hierarchy");
                                    }
                                }
                                else
                                {
                                    appLogger.LogWarning($"Could not find target item for link '{href}' with articleid '{articleId}' in website hierarchy");
                                }
                            }
                        }

                        //
                        // Inject CSS and JS files into the page
                        //
                        var nodeHead = xmlPage.DocumentElement.SelectSingleNode("head");
                        var nodeListCssAssets = xbrlConversionProperties.XmlClientAssets.SelectNodes("//css/file");
                        foreach (XmlNode nodeCssAsset in nodeListCssAssets)
                        {
                            var uriCss = GetAttribute(nodeCssAsset, "uri");
                            if (uriCss.Contains('?'))
                            {
                                uriCss = uriCss.SubstringBefore("?");
                            }

                            var nodeLink = xmlPage.CreateElement("link");
                            nodeLink.SetAttribute("rel", "stylesheet");
                            nodeLink.SetAttribute("type", "text/css");
                            nodeLink.SetAttribute("href", uriCss);
                            nodeHead.AppendChild(nodeLink);
                        }

                        var nodeListJsAssets = xbrlConversionProperties.XmlClientAssets.SelectNodes("//js/file");
                        foreach (XmlNode nodeJsAsset in nodeListJsAssets)
                        {
                            try
                            {
                                var uriJs = GetAttribute(nodeJsAsset, "uri");
                                if (uriJs.Contains('?'))
                                {
                                    uriJs = uriJs.SubstringBefore("?");
                                }
                                var nodeScript = xmlPage.CreateElement("script");
                                nodeScript.SetAttribute("type", "text/javascript");
                                nodeScript.SetAttribute("src", uriJs);
                                nodeScript.InnerText = "//";
                                nodeHead.AppendChild(nodeScript);
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogError(ex, $"Could not add JS asset: {nodeJsAsset.OuterXml}");
                            }

                        }

                        //
                        // => Inject the menu into the page
                        //
                        var htmlPage = xmlPage.OuterXml.Replace("[navigation]", htmlMenu);


                        //
                        // => Optionally, prefix all links and assets
                        //
                        var nodeWebsiteAssetsPrefix = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='websiteassetsprefix']");
                        if (nodeWebsiteAssetsPrefix != null)
                        {
                            var websiteAssetsPrefix = nodeWebsiteAssetsPrefix.InnerText;
                            if (!string.IsNullOrEmpty(websiteAssetsPrefix))
                            {
                                if (websiteAssetsPrefix.Contains("{sitelang}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{sitelang}", projectVars.outputChannelVariantLanguage);
                                if (websiteAssetsPrefix.Contains("{projectid}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{projectid}", projectVars.projectId);
                                if (websiteAssetsPrefix.Contains("{guidlegalentity}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{guidlegalentity}", projectVars.guidLegalEntity);
                                if (htmlPage.Contains("<base href=\"/\""))
                                {
                                    htmlPage = htmlPage.Replace("src=\"/", $"src=\"").Replace("href=\"/", $"href=\"");
                                    htmlPage = htmlPage.Replace("<base href=\"", $"<base href=\"{websiteAssetsPrefix}/");
                                }
                                else
                                {
                                    htmlPage = htmlPage.Replace("src=\"/", $"src=\"{websiteAssetsPrefix}/").Replace("href=\"/", $"href=\"{websiteAssetsPrefix}/");
                                }
                            }
                        }

                        //
                        // => Convert the XHTML into an HTML
                        //
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlPage);
                        htmlPage = htmlDoc.DocumentNode.OuterHtml;

                        // Console.WriteLine("-----------------------------------");
                        // Console.WriteLine(htmlPage);
                        // Console.WriteLine("-----------------------------------");

                        //
                        // Store the document in the website folder
                        //
                        var pagePathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}{path}";

                        // Ensure the directory structure exists for the page path
                        var directoryPath = Path.GetDirectoryName(pagePathOs);
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        // Store the HTML content in the specified path
                        await File.WriteAllTextAsync(pagePathOs, htmlPage, Encoding.UTF8);
                    }

                }

                //
                // => CSS and JS assets
                //
                var sourceJsPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/{xbrlConversionProperties.XbrlVars.guidLegalEntity}/js/{projectVars.reportTypeId}/{outputChannelType}.js";
                if (!File.Exists(sourceJsPathOs))
                {
                    sourceJsPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/js/{projectVars.reportTypeId}/{outputChannelType}.js";
                    if (!File.Exists(sourceJsPathOs))
                    {
                        appLogger.LogError($"Could not locate the source JS file for the website pages: {sourceJsPathOs}");
                        throw new Exception($"Could not locate the JS file for the website pages");
                    }
                }
                var targetJsPathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/js/{outputChannelType}.js";
                var directoryPathOs = Path.GetDirectoryName(targetJsPathOs);
                if (!Directory.Exists(directoryPathOs))
                {
                    Directory.CreateDirectory(directoryPathOs);
                }
                File.Copy(sourceJsPathOs, targetJsPathOs);


                var sourceCssPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/{xbrlConversionProperties.XbrlVars.guidLegalEntity}/css/{projectVars.reportTypeId}/{outputChannelType}.css";
                if (!File.Exists(sourceCssPathOs))
                {
                    sourceCssPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/css/{projectVars.reportTypeId}/{outputChannelType}.css";
                    if (!File.Exists(sourceCssPathOs))
                    {
                        appLogger.LogError($"Could not locate the source CSS file for the website pages: {sourceCssPathOs}");
                        throw new Exception($"Could not locate the CSS file for the website pages");
                    }
                }
                var targetCssPathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/css/{outputChannelType}.css";
                directoryPathOs = Path.GetDirectoryName(targetCssPathOs);
                if (!Directory.Exists(directoryPathOs))
                {
                    Directory.CreateDirectory(directoryPathOs);
                }
                File.Copy(sourceCssPathOs, targetCssPathOs);

                //
                // => Optionally copy fonts for the website pages
                //
                var sourceFontsDirectoryFound = false;
                var sourceFontsFolderPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/{xbrlConversionProperties.XbrlVars.guidLegalEntity}/fonts";
                if (!Directory.Exists(sourceFontsFolderPathOs))
                {
                    sourceFontsFolderPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/fonts";
                    if (!Directory.Exists(sourceFontsFolderPathOs))
                    {
                        appLogger.LogError($"Could not locate the source fonts folder for the website pages: {sourceFontsFolderPathOs}");
                        throw new Exception($"Could not locate the  source fonts folder for the website pages");
                    }
                    else
                    {
                        sourceFontsDirectoryFound = true;
                    }
                }
                else
                {
                    sourceFontsDirectoryFound = true;
                }
                // Console.WriteLine($"~~~~~~~~~ sourceFontsDirectoryFound: {sourceFontsDirectoryFound}, sourceFontsFolderPathOs: {sourceFontsFolderPathOs} ~~~~~~~~~~~");
                if (sourceFontsDirectoryFound)
                {
                    var targetFontsFolderPathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/fonts";
                    if (!Directory.Exists(targetFontsFolderPathOs)) Directory.CreateDirectory(targetFontsFolderPathOs);
                    CopyDirectoryRecursive(sourceFontsFolderPathOs, targetFontsFolderPathOs);
                }
                else
                {
                    appLogger.LogInformation($"Could not locate the source fonts folder for the website pages: {sourceFontsFolderPathOs}");
                }


                //
                // => Images from the report design package
                //
                var sourceSystemImagesDirectoryFound = false;
                var sourceSystemImagesFolderPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/{xbrlConversionProperties.XbrlVars.guidLegalEntity}/images";
                if (!Directory.Exists(sourceSystemImagesFolderPathOs))
                {
                    sourceSystemImagesFolderPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{outputChannelType}/images";
                    if (!Directory.Exists(sourceSystemImagesFolderPathOs))
                    {
                        appLogger.LogError($"Could not locate the source system images folder for the website pages: {sourceSystemImagesFolderPathOs}");
                        throw new Exception($"Could not locate the source system images folder for the website pages");
                    }
                    else
                    {
                        sourceSystemImagesDirectoryFound = true;
                    }
                }
                else
                {
                    sourceSystemImagesDirectoryFound = true;
                }
                // Console.WriteLine($"~~~~~~~~~ sourceSystemImagesDirectoryFound: {sourceSystemImagesDirectoryFound}, sourceFontsFolderPathOs: {sourceSystemImagesDirectoryFound} ~~~~~~~~~~~");
                if (sourceSystemImagesDirectoryFound)
                {
                    var targetSystemImagesFolderPathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/images";
                    if (!Directory.Exists(targetSystemImagesFolderPathOs)) Directory.CreateDirectory(targetSystemImagesFolderPathOs);
                    CopyDirectoryRecursive(sourceSystemImagesFolderPathOs, targetSystemImagesFolderPathOs);
                }
                else
                {
                    appLogger.LogInformation($"Could not locate the source system images folder for the website pages: {sourceSystemImagesFolderPathOs}");
                }


                //
                // => Copy the images to the website folder
                //
                foreach (var imagePath in imagePaths)
                {
                    // Console.WriteLine($"Source: {imagePath.Key}, Target: {imagePath.Value}");
                    directoryPathOs = Path.GetDirectoryName(imagePath.Value);
                    if (!Directory.Exists(directoryPathOs))
                    {
                        Directory.CreateDirectory(directoryPathOs);
                    }

                    File.Copy(imagePath.Key, imagePath.Value);
                }

                //
                // => Generate PDF files
                //
                if (renderPdfFiles)
                {
                    var targetPdfFolderPathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/downloads/publications/pdf";
                    if (!Directory.Exists(targetPdfFolderPathOs)) Directory.CreateDirectory(targetPdfFolderPathOs);
                    var pdfGenerationReult = await GeneratePdfFiles(projectVars, targetPdfFolderPathOs, useLoremIpsum);
                    if (pdfGenerationReult.Success)
                    {
                        await _outputGenerationProgressMessage(pdfGenerationReult.Message);
                    }
                    else
                    {
                        await _outputGenerationProgressMessage($"ERROR: {pdfGenerationReult.Message}");
                    }
                }


                //
                // => Add generated XBRL files
                //
                if (xbrlZipFileGuids.Count > 0)
                {
                    await _outputGenerationProgressMessage("Adding XBRL packages");
                    var targetFolderPathOs = $"{xbrlConversionProperties.OutputWebsiteFolderPathOs}/downloads/publications/xbrl";
                    if (!Directory.Exists(targetFolderPathOs)) Directory.CreateDirectory(targetFolderPathOs);

                    foreach (var xbrlGuid in xbrlZipFileGuids)
                    {
                        // Retrieve the URL to get the full package and some metadata about the XBRL package
                        var (Success, ApiUrl, OriginalFileName, XbrlScheme) = await GetGeneratedReportApiUrl(projectVars.projectId, xbrlGuid);
                        var xbrlPackageName = $"xbrl-{XbrlScheme.ToLower()}.zip";

                        if (Success)
                        {
                            using var httpClient = new HttpClient();
                            using var response = await httpClient.GetAsync(ApiUrl, HttpCompletionOption.ResponseHeadersRead);

                            if (!response.IsSuccessStatusCode)
                            {
                                appLogger.LogError($"Failed to download file from {ApiUrl}. Status code: {response.StatusCode}");
                                await _outputGenerationProgressMessage($"ERROR: Failed to download file. Status code: {response.StatusCode}");
                            }
                            else
                            {
                                // Get the binary content of the response
                                using var contentStream = await response.Content.ReadAsStreamAsync();

                                // Extract the actual XBRL package from the downloaded zip binary
                                var foundXbrlPackage = false;
                                using (var zipArchive = new ZipArchive(contentStream, ZipArchiveMode.Read, true, Encoding.UTF8))
                                {
                                    foreach (var entry in zipArchive.Entries)
                                    {
                                        if (Path.GetExtension(entry.FullName).ToLower() == ".zip")
                                        {
                                            var extractedFilePath = Path.Combine(targetFolderPathOs, xbrlPackageName);
                                            entry.ExtractToFile(extractedFilePath, overwrite: true);
                                            foundXbrlPackage = true;
                                        }
                                    }
                                }

                                // Render a message to the progress output
                                if (foundXbrlPackage)
                                {
                                    await _outputGenerationProgressMessage($" -> Added {xbrlPackageName}");
                                }
                                else
                                {
                                    await _outputGenerationProgressMessage($" -> ERROR: unable to find the XBRL package");
                                }
                            }
                        }
                        else
                        {
                            appLogger.LogError("Unable to retrieve the generated report from the repository");
                        }
                    }
                }


                //
                // => Return the website folder path as payload
                //
                return new TaxxorReturnMessage(true, "Successfully rendered HTML website of the Annual Report filing", xbrlConversionProperties.OutputWebsiteFolderPathOs, "");
            }
            catch (Exception ex)
            {
                var errorMessage = new TaxxorReturnMessage(false, "There was an error generating the HTML website", $"error: {ex}");
                appLogger.LogError(ex, errorMessage.Message);
                return errorMessage;
            }


        }


        /// <summary>
        /// Renders a single HTML site web page
        /// </summary>
        /// <param name="Success"></param>
        /// <param name="Html"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlWebsiteHierarchy"></param>
        /// <param name="sectionIdOrDataReference"></param>
        /// <param name="useLoremIpsum"></param>
        /// <returns></returns>
        public static async Task<(bool Success, string Html, string ErrorMessage)> RenderSingleWebPage(
            ProjectVariables projectVars,
            XmlDocument xmlWebsiteHierarchy,
            string sectionIdOrDataReference,
            bool useLoremIpsum = false)
        {
            var errorMessage = string.Empty;

            try
            {
                var nodeItem = xmlWebsiteHierarchy.SelectSingleNode($"/items/structured//item[@id='{sectionIdOrDataReference}' or @data-ref='{sectionIdOrDataReference}']");
                if (nodeItem == null)
                {
                    errorMessage = $"Could not item in website hierarchy for sectionId: {sectionIdOrDataReference}";
                    appLogger.LogWarning(errorMessage);
                    return (Success: false, Html: null, ErrorMessage: errorMessage);
                }

                // Determine the paths of the XSLT's used for generating the website pages (part of the report design package)
                var pageXsltPathOs = RenderHtmlSiteWebPageXsltPathOs(projectVars);
                var menuXsltPathOs = RenderHtmlSiteMenuXsltPathOs(projectVars);

                // Retrieve the client-side assets (JS and CSS)
                var xmlClientAssets = Extensions.RenderOutputChannelCssAndJs(projectVars, projectVars.outputChannelType);



                var path = nodeItem.SelectSingleNode("web_page/path")?.InnerText ?? "";
                var linkName = nodeItem.SelectSingleNode("web_page/linkname")?.InnerText ?? "";
                var itemId = nodeItem.GetAttribute("id") ?? "";
                var dataRef = nodeItem.GetAttribute("data-ref") ?? "";

                if (path == "")
                {
                    errorMessage = $"Could not locate path for item {nodeItem.OuterXml}";
                    appLogger.LogWarning(errorMessage);
                    return (Success: false, Html: null, ErrorMessage: errorMessage);
                }

                if (itemId == "")
                {
                    errorMessage = $"Could not locate item id for item {nodeItem.OuterXml}";
                    appLogger.LogWarning(errorMessage);
                    return (Success: false, Html: null, ErrorMessage: errorMessage);
                }

                if (dataRef == "")
                {
                    errorMessage = $"Could not locate data-ref for item {nodeItem.OuterXml}";
                    appLogger.LogWarning(errorMessage);
                    return (Success: false, Html: null, ErrorMessage: errorMessage);
                }



                //
                // => Retrieve the content of the section
                //
                var xmlFilingContent = await Extensions.RetrieveFilingComposerXmlData(projectVars, dataRef);
                // appLogger.LogInformation($"Retrieved {dataRef} for {xbrlConversionProperties.XbrlVars.outputChannelVariantLanguage}:\n{TruncateString(PrettyPrintXml(xmlFilingContent), 1000)}");
                if (xmlFilingContent.DocumentElement != null)
                {
                    if (useLoremIpsum)
                    {

                        var xmlPdfHtmlLoremIpsum = await ConvertToLoremIpsum(xmlFilingContent, projectVars.outputChannelVariantLanguage);

                        // Message to the client
                        await _outputGenerationProgressMessage("Converted website content to fake (lorem ipsum) text");

                        // Remove the XML declaration
                        if (xmlPdfHtmlLoremIpsum.FirstChild.NodeType == XmlNodeType.XmlDeclaration) xmlPdfHtmlLoremIpsum.RemoveChild(xmlPdfHtmlLoremIpsum.FirstChild);

                        xmlFilingContent.LoadXml(xmlPdfHtmlLoremIpsum.OuterXml);
                    }


                    //
                    // Render the menu for this page
                    //
                    XsltArgumentList xsltArgsMenu = new();
                    xsltArgsMenu.AddParam("current-item-id", "", itemId);
                    var htmlMenu = TransformXml(xmlWebsiteHierarchy, menuXsltPathOs, xsltArgsMenu);


                    //
                    // Render the HTML for this page
                    //
                    XsltArgumentList xsltArgsPage = new();
                    xsltArgsPage.AddParam("hierarchy-title", "", linkName);
                    var xmlPage = TransformXmlToDocument(xmlFilingContent, pageXsltPathOs, xsltArgsPage);


                    //
                    // Inject CSS and JS files into the page
                    //
                    var nodeHead = xmlPage.DocumentElement.SelectSingleNode("head");
                    var nodeListCssAssets = xmlClientAssets.SelectNodes("//css/file");
                    foreach (XmlNode nodeCssAsset in nodeListCssAssets)
                    {
                        var uriCss = GetAttribute(nodeCssAsset, "uri");
                        if (uriCss.Contains('?'))
                        {
                            uriCss = uriCss.SubstringBefore("?");
                        }

                        var nodeLink = xmlPage.CreateElement("link");
                        nodeLink.SetAttribute("rel", "stylesheet");
                        nodeLink.SetAttribute("type", "text/css");
                        nodeLink.SetAttribute("href", uriCss);
                        nodeHead.AppendChild(nodeLink);
                    }

                    var nodeListJsAssets = xmlClientAssets.SelectNodes("//js/file");
                    foreach (XmlNode nodeJsAsset in nodeListJsAssets)
                    {
                        try
                        {
                            var uriJs = GetAttribute(nodeJsAsset, "uri");
                            if (uriJs.Contains('?'))
                            {
                                uriJs = uriJs.SubstringBefore("?");
                            }
                            var nodeScript = xmlPage.CreateElement("script");
                            nodeScript.SetAttribute("type", "text/javascript");
                            nodeScript.SetAttribute("src", uriJs);
                            nodeScript.InnerText = "//";
                            nodeHead.AppendChild(nodeScript);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, $"Could not add JS asset: {nodeJsAsset.OuterXml}");
                        }

                    }

                    //
                    // => Inject the menu into the page
                    //
                    var htmlPage = xmlPage.OuterXml.Replace("[navigation]", htmlMenu);


                    //
                    // => Optionally, prefix all links and assets
                    //
                    var websiteAssetsPrefix = "";
                    var nodeWebsiteAssetsPrefix = xmlApplicationConfiguration.SelectSingleNode("/configuration/general/settings/setting[@id='websiteassetsprefix']");
                    if (nodeWebsiteAssetsPrefix != null)
                    {
                        websiteAssetsPrefix = nodeWebsiteAssetsPrefix.InnerText;
                        if (!string.IsNullOrEmpty(websiteAssetsPrefix))
                        {
                            if (websiteAssetsPrefix.Contains("{sitelang}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{sitelang}", projectVars.outputChannelVariantLanguage);
                            if (websiteAssetsPrefix.Contains("{projectid}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{projectid}", projectVars.projectId);
                            if (websiteAssetsPrefix.Contains("{guidlegalentity}")) websiteAssetsPrefix = websiteAssetsPrefix.Replace("{guidlegalentity}", projectVars.guidLegalEntity);
                            if (htmlPage.Contains("<base href=\"/\""))
                            {
                                htmlPage = htmlPage.Replace("src=\"/", $"src=\"").Replace("href=\"/", $"href=\"");
                                htmlPage = htmlPage.Replace("<base href=\"", $"<base href=\"{websiteAssetsPrefix}/");
                            }
                            else
                            {
                                htmlPage = htmlPage.Replace("src=\"/", $"src=\"{websiteAssetsPrefix}/").Replace("href=\"/", $"href=\"{websiteAssetsPrefix}/");
                            }
                        }
                    }

                    //
                    // For images, we need to use the original image paths
                    //
                    // TODO: this needs to become more sophisticated, but works for now
                    htmlPage = htmlPage.Replace($"img src=\"{websiteAssetsPrefix}/dataserviceassets", $"img src=\"/dataserviceassets");

                    //
                    // => Convert the XHTML into an HTML
                    //
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlPage);
                    htmlPage = htmlDoc.DocumentNode.OuterHtml;

                    return (Success: true, Html: htmlPage, ErrorMessage: null);
                }
                else
                {
                    errorMessage = "Content page does not contain any XHTML content";
                    appLogger.LogError(errorMessage);
                    return (Success: false, Html: null, ErrorMessage: errorMessage);
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "Error rendering single web page");
                return (Success: false, Html: null, ErrorMessage: ex.ToString());
            }
        }

        /// <summary>
        /// Calculates the path to the XSLT that needs to render the base HTML site page.
        /// </summary>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static string RenderHtmlSiteWebPageXsltPathOs(ProjectVariables projectVars)
        {
            var pageXsltPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{projectVars.outputChannelType}/{projectVars.guidLegalEntity}/xslt/page.xsl";
            if (!File.Exists(pageXsltPathOs))
            {
                pageXsltPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{projectVars.outputChannelType}/xslt/page.xsl";
                if (!File.Exists(pageXsltPathOs))
                {
                    appLogger.LogError($"Could not locate the XSLT file for the website pages: {pageXsltPathOs}");
                    throw new Exception($"Could not locate the XSLT file for the website pages");
                }
            }

            return pageXsltPathOs;
        }

        /// <summary>
        /// Renders the path to the XSLT that needs to render the HTML site menu.
        /// </summary>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static string RenderHtmlSiteMenuXsltPathOs(ProjectVariables projectVars)
        {
            var menuXsltPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{projectVars.outputChannelType}/{projectVars.guidLegalEntity}/xslt/menu.xsl";
            if (!File.Exists(menuXsltPathOs))
            {
                menuXsltPathOs = $"{websiteRootPathOs}/outputchannels/{TaxxorClientId}/{projectVars.outputChannelType}/xslt/menu.xsl";
                if (!File.Exists(menuXsltPathOs))
                {
                    appLogger.LogError($"Could not locate the XSLT file for the website menu: {menuXsltPathOs}");
                    throw new Exception($"Could not locate the XSLT file for the website menu");
                }
            }

            return menuXsltPathOs;
        }


        /// <summary>
        /// Generates PDF files for the website
        /// </summary>
        /// <param name="projectVars"></param>
        /// <param name="pdfWebsiteAssetsFolderPathOs"></param>
        /// <param name="useLoremIpsum"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> GeneratePdfFiles(ProjectVariables projectVars, string pdfWebsiteAssetsFolderPathOs, bool useLoremIpsum)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";
            var reportingPeriod = RetrieveNodeValueIfExists($"/configuration/cms_projects/cms_project[@id='{projectVars.projectId}']/reporting_period", xmlApplicationConfiguration);
            var editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);

            var totalPdfFilesToGenerate = 0;
            var numberOfGeneratedPdfFiles = 0;

            try
            {
                PdfProperties pdfProperties = new()
                {
                    Sections = "all",
                    Secure = true
                };
                var nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(editorId) + "]/output_channels/output_channel[@type='pdf']");

                foreach (XmlNode nodeOutputChannel in nodeListOutputChannels)
                {
                    var outputChannelType = GetAttribute(nodeOutputChannel, "type");
                    var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes(Extensions.RenderWebsitePdfGenerationXpath(projectVars));
                    foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                    {
                        var outputChannelVariantLanguage = GetAttribute(nodeVariant, "lang");
                        var outputChannelVariantId = GetAttribute(nodeVariant, "id");
                        //var contentType = "regular";

                        if (!string.IsNullOrEmpty(outputChannelVariantLanguage))
                        {
                            if (projectVars.outputChannelVariantLanguage == outputChannelVariantLanguage)
                            {
                                totalPdfFilesToGenerate++;

                                // Construct a project variables object that we can send to the PDF Generator
                                var projectVariablesForPdfGeneration = new ProjectVariables
                                {
                                    // Fill with variables passed to this function
                                    projectId = projectVars.projectId,
                                    versionId = "latest",
                                    //projectVariablesForPdfGeneration.did = "all";

                                    // Fill with variables retrieved from the context    
                                    editorContentType = "regular",
                                    reportTypeId = projectVars.reportTypeId,
                                    outputChannelType = outputChannelType,
                                    editorId = editorId,
                                    outputChannelVariantId = outputChannelVariantId,
                                    outputChannelVariantLanguage = outputChannelVariantLanguage
                                };
                                projectVariablesForPdfGeneration.currentUser.Id = projectVars.currentUser.Id;

                                // Render a full path to the PDF file
                                var pdfFileName = Extensions.RenderPdfFileNameForWebsite(projectVariablesForPdfGeneration, reportingPeriod);

                                var pdfFilePathOs = $"{pdfWebsiteAssetsFolderPathOs}/{pdfFileName}".Replace("[language]", outputChannelVariantLanguage);

                                await _outputGenerationProgressMessage($"Start rendering {pdfFileName}");

                                Console.Write($"- pdfFilePathOs: {pdfFilePathOs}");

                                // Retrieve output channel hierarchy to get the top node value and use that as a string to send to the PDF generator
                                var booktitle = "";
                                var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage);
                                if (projectVars.cmsMetaData.ContainsKey(outputChannelHierarchyMetadataKey))
                                {
                                    var xmlHierarchyDoc = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                                    var nodeBookTitle = xmlHierarchyDoc.SelectSingleNode("/items/structured/item/web_page/linkname");
                                    if (nodeBookTitle != null)
                                    {
                                        booktitle = nodeBookTitle.InnerText;
                                    }
                                    else
                                    {
                                        appLogger.LogError($"Could not locate booktitle in outputChannelHierarchyMetadataKey: {outputChannelHierarchyMetadataKey}");
                                    }
                                }
                                else
                                {
                                    appLogger.LogError($"Could not locate projectVars.cmsMetaData['{outputChannelHierarchyMetadataKey}'], stack-trace: {GetStackTrace()}");
                                }
                                if (booktitle != "")
                                {
                                    if (pdfProperties.PdfGeneratorStringSet.ContainsKey("booktitle"))
                                    {
                                        pdfProperties.PdfGeneratorStringSet["booktitle"] = booktitle;
                                    }
                                    else
                                    {
                                        pdfProperties.PdfGeneratorStringSet.Add("booktitle", booktitle);
                                    }

                                }

                                if (useLoremIpsum)
                                {
                                    pdfProperties.UseLoremIpsum = true;
                                }

                                // Disable sync errors on all PDF's we publish online
                                pdfProperties.HideErrors = true;

                                // Determine the layout of the PDF that we want to render (landscape vs portrait)
                                pdfProperties.Layout = "regular";
                                Extensions.SetPdfPropertiesForOutputChannel(ref pdfProperties, projectVariablesForPdfGeneration, "website");

                                // Render the PDF and store it in the system folder
                                var xmlPdfRenderResult = await PdfService.RenderPdf<XmlDocument>(pdfProperties, projectVariablesForPdfGeneration, pdfFilePathOs, debugRoutine);

                                if (XmlContainsError(xmlPdfRenderResult))
                                {
                                    appLogger.LogError($"Failed to generate PDF files. pdfFilePathOs: {pdfFilePathOs}, response: {xmlPdfRenderResult.OuterXml}, stack-trace: {GetStackTrace()}");
                                    return new TaxxorReturnMessage(xmlPdfRenderResult);
                                }
                                else
                                {
                                    await _outputGenerationProgressMessage($"Finished rendering {pdfFileName}");
                                    numberOfGeneratedPdfFiles++;
                                }
                            }


                        }
                    }
                }

                return new TaxxorReturnMessage(true, $"Successfully generated all PDF files");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to generate all PDF files (generated {numberOfGeneratedPdfFiles} of total {totalPdfFilesToGenerate} PDF files)";
                appLogger.LogError(ex, errorMessage);
                return new TaxxorReturnMessage(false, errorMessage);
            }
        }




        /// <summary>
        /// Directly publishes a web page from the website editor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task QuickPublishWebPage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Get posted data
            var targetPlatform = request.RetrievePostedValue("targetplatform", "(production|staging|develop)", true, reqVars.returnType);

            // Message to the client
            await _outputGenerationProgressMessage("Start website publication");

            // Publish the web page using the extension method
            var quickPublishResult = await Extensions.QuickPublishWebPage(context, targetPlatform);

            if (!quickPublishResult.Success)
            {
                await response.Error(quickPublishResult, reqVars.returnType);
            }
            else
            {
                await response.OK(quickPublishResult, reqVars.returnType, true);
            }

        }

        /// <summary>
        /// Executes the quick publish web content routine for all the project that contain a website output channel
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task QuickPublishWebPageAllProjects(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = siteType == "local" || siteType == "dev";

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            string[] excludedProjectIds = await Extensions.RetrieveProjectIdsNotToBePublished(context);

            // Get posted data
            var targetPlatform = request.RetrievePostedValue("targetplatform", "(production|staging|develop)", true, reqVars.returnType);
            var targetProjectIds = request.RetrievePostedValue("projectids", @"^[\w\d,\-_]{2,2048}$", true, reqVars.returnType);

            // Message to the client
            await _outputGenerationProgressMessage($"Start website publication projects to {targetPlatform}");

            try
            {
                // Find all projects that contain a website output channel
                var successCounter = 0;
                var failCounter = 0;
                var skippedCounter = 0;

                var arrProjectsToRender = targetProjectIds.Split(',');
                foreach (var projectId in arrProjectsToRender)
                {
                    if (string.IsNullOrEmpty(projectId))
                    {
                        await _outputGenerationProgressMessage($"Unable to generate website for this project because projectId is empty");
                        continue;
                    }

                    var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectId}']");
                    if (nodeProject == null)
                    {
                        await _outputGenerationProgressMessage($"Unable to generate website for this project because it could not be located. projectId: {projectId}");
                        continue;
                    }

                    var projectName = nodeProject.SelectSingleNode("name")?.InnerText ?? "unknown";

                    if (excludedProjectIds.Contains(projectId))
                    {
                        skippedCounter++;
                        await _outputGenerationProgressMessage($"Skipping project {projectName}, because it was explicitly excluded from the sync list");
                    }
                    else
                    {
                        var editorId = RetrieveEditorIdFromProjectId(projectId);
                        var nodeWebOutputChannel = xmlApplicationConfiguration.SelectSingleNode($"/configuration/editors/editor[@id='{editorId}']/output_channels/output_channel[@type='website' and variants/variant]");
                        if (nodeWebOutputChannel == null)
                        {
                            appLogger.LogInformation($"Skipping project {projectId} because it doesn't contain a website output channel");
                        }
                        else
                        {

                            await _outputGenerationProgressMessage($"Start publishing {projectName} with ID {projectId}");

                            var publicationResult = await QuickPublishWebPageAllLanguages(projectId, targetPlatform);
                            // var publicationResult = new TaxxorReturnMessage(true, "Bogus", $"projectId: {projectId}");

                            if (publicationResult.Success)
                            {
                                successCounter++;
                                await _outputGenerationProgressMessage($"Webcontent successfully published");
                            }
                            else
                            {
                                failCounter++;
                                appLogger.LogError($"{publicationResult.Message}, debug-info: {publicationResult.DebugInfo}");
                                await _outputGenerationProgressMessage($"Unable to publish web content on the target platform");
                            }

                        }
                    }


                }

                await response.OK(GenerateSuccessXml($"Successfully published website content - success: {successCounter}, failed: {failCounter}, skipped: {skippedCounter}"), reqVars.returnType, false);

            }
            catch (Exception ex)
            {
                HandleError("Failed to publish web content", ex.ToString());
            }
        }


        /// <summary>
        /// Quickly publish the web pages for all the languages defined for a project
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="targetPlatform"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> QuickPublishWebPageAllLanguages(string projectId, string targetPlatform)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);
            var debugMessage = "";

            try
            {
                //
                // => Loop through the output channels defined for this project and launch the quick publish routine
                //
                var projectName = xmlApplicationConfiguration.SelectSingleNode($"//cms_project[@id='{projectId}']/name")?.InnerText ?? "unknown";
                var publishedLanguages = new List<string>();
                var editorId = RetrieveEditorIdFromProjectId(projectId);

                var nodeListWebOutputChannels = xmlApplicationConfiguration.SelectNodes($"//editor[@id='{editorId}']//output_channel[@type='website']//variant");
                foreach (XmlNode nodeWebOutputChannel in nodeListWebOutputChannels)
                {
                    var outputChannelId = nodeWebOutputChannel.GetAttribute("id");
                    var outputChannelLanguage = nodeWebOutputChannel.GetAttribute("lang");
                    publishedLanguages.Add(outputChannelLanguage);
                    var webHierarchyId = nodeWebOutputChannel.GetAttribute("metadata-id-ref");
                    var nodeWebHierarchyLocation = xmlApplicationConfiguration.SelectSingleNode($"//cms_project[@id='{projectId}']//metadata[@id='{webHierarchyId}']//location");
                    if (nodeWebHierarchyLocation == null)
                    {
                        return new TaxxorReturnMessage(false, $"Could not locate web hierarchy node in project configuration", $"webHierarchyId: {webHierarchyId}, projectId: {projectId}, outputChannelLanguage: {outputChannelLanguage}, targetPlatform: {targetPlatform}");
                    }
                    else
                    {
                        var webHierarchyLocation = nodeWebHierarchyLocation.InnerText;

                        // - Alter the project variables object in the context so that we can use it for quick publishing
                        projectVars.projectId = projectId;
                        projectVars = await FillProjectVariablesFromProjectId(projectVars, false);
                        projectVars.outputChannelVariantId = outputChannelId;
                        projectVars.outputChannelVariantLanguage = outputChannelLanguage;
                        projectVars.outputChannelType = "website";

                        SetProjectVariables(context, projectVars);

                        // - Retrieve the hierarcht from the taxxor data service
                        var webHierarchy = await DocumentStoreService.FilingData.LoadHierarchy(projectVars);
                        Console.WriteLine("------------- Web Hierarchy ------------------");
                        Console.WriteLine(PrettyPrintXml(webHierarchy));
                        Console.WriteLine("----------------------------------------------");

                        var quickPublishAllowed = Extensions.CheckIfWebsiteIsAllowedToBeQuickPublished(projectVars);
                        if (quickPublishAllowed)
                        {
                            await Extensions.QuickPublishWebPage(context, targetPlatform, false);
                        }
                        else
                        {
                            debugMessage = $"projectId: {projectId}, outputChannelLanguage: {outputChannelLanguage}, outputChannelVariantId: {projectVars.outputChannelVariantId}, targetPlatform: {targetPlatform}";
                            appLogger.LogWarning($"This webpage cannot be published because it had been disabled from the quick publish list. {debugMessage}");
                            // return new TaxxorReturnMessage(true, $"This webpage cannot be published because it had been disabled from the quick publish list", debugMessage);
                        }

                    }
                }


                return new TaxxorReturnMessage(true, $"Successfully quick-published languages {string.Join(",", publishedLanguages.ToArray())} for project {projectName} to {targetPlatform}");
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, $"There was an error quick-publishing your website content on {targetPlatform}", $"error: {ex}");
            }

        }

    }

}