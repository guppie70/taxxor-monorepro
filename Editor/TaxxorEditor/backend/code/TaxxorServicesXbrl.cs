using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace Taxxor
{

    public partial class ConnectedServices
    {

        /// <summary>
        /// Utilities for generating XBRL using the Taxxor XBRL Service
        /// </summary>
        public static class XbrlService
        {

            /// <summary>
            /// Renders SEC iXBRL
            /// </summary>
            /// <param name="xmlDoc"></param>
            /// <param name="projectId"></param>
            /// <param name="outputFolderPathOs"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> RenderSecXbrl(XmlDocument xmlDoc, string projectId, string outputFolderPathOs, string backupRootFolderPathOs, string commentContents)
            {
                var debugRoutine = siteType == "local" || siteType == "dev";


                // File and folder paths for the working files
                var zipFilePathOs = $"{dataRootPathOs}/temp/ixbrl-sec-package_{CreateTimeBasedString()}.zip";
                var tempExtractFolderPathOs = $"{dataRootPathOs}/temp/{Path.GetFileNameWithoutExtension(zipFilePathOs)}";

                /*
                Render the Inline XBRL files and grab the result in an XML envelope
                */
                if (debugRoutine)
                {
                    await xmlDoc.SaveAsync($"{logRootPathOs}/ixbrl-source-{projectId}-sec.xml");
                }

                var apiUrl = GetServiceUrlByMethodId(ConnectedServiceEnum.XbrlService, "post_api_xbrl_create_SEC");
                var querystringData = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "docId", commentContents }
                };
                apiUrl = QueryHelpers.AddQueryString(apiUrl, querystringData);

                var xbrlServiceResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.XbrlService, RequestMethodEnum.Post, apiUrl, xmlDoc, true);
                if (XmlContainsError(xbrlServiceResponse))
                {
                    var errorMessage = "Error generating XBRL with the Taxxor XBRL Service";

                    // Attempt tp retrieve details why things went wrong
                    string? xbrlErrorPayload = xbrlServiceResponse.SelectSingleNode("/error/httpresponse")?.InnerText;
                    if (!string.IsNullOrEmpty(xbrlErrorPayload))
                    {
                        var xmlErrorDetails = new XmlDocument();
                        var payloadContent = HttpUtility.HtmlDecode(xbrlErrorPayload);
                        try
                        {
                            xmlErrorDetails.LoadXml(payloadContent);
                            var xbrlErrorDetails = xmlErrorDetails.SelectSingleNode("//message")?.InnerText ?? "";
                            if (xbrlErrorDetails != "") errorMessage += $"\n-------------------------------\nDetails:\n{xbrlErrorDetails}\n-------------------------------";
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Unable to parse the error response of the XBRL Generator");
                        }
                    }
                    return new TaxxorReturnMessage(false, errorMessage, $"debuginfo: {xbrlServiceResponse.SelectSingleNode("/error/debuginfo")?.InnerText ?? $"unknwon..."}, stack-trace: {GetStackTrace()}");
                }

                // Find the node that contains the zipped base64 encoded data
                var nodePayload = xbrlServiceResponse.SelectSingleNode("/RestResult/payload");

                if (nodePayload == null)
                {
                    return new TaxxorReturnMessage(false, "Could not find the XBRL data node", $"response: {TruncateString(xbrlServiceResponse.OuterXml, 2000)}, stack-trace: {GetStackTrace()}");
                }

                // Convert the received base64 encoded content into a binary
                var base64Content = nodePayload.InnerText;
                var success = Base64DecodeAsBinaryFile(base64Content, zipFilePathOs);
                if (!success)
                {
                    return new TaxxorReturnMessage(false, "Could not convert the XBRL binary data", $"zipFilePathOs: {zipFilePathOs}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    appLogger.LogInformation($"Stored SEC XBRL package retrieved from the XBRL generator in {zipFilePathOs}");
                }

                // Store the original output of the XBRL engine for logging purposes
                try
                {
                    var backupZipFilePathOs = $"{logRootPathOs}/xbrlservice-output-sec.zip";
                    if (File.Exists(backupZipFilePathOs)) File.Delete(backupZipFilePathOs);
                    File.Copy(zipFilePathOs, backupZipFilePathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not copy the output of the XBRL service");
                }

                // Extract the ZIP archive to a temporary directory
                try
                {
                    Directory.CreateDirectory(tempExtractFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not create a folder to extract XBRL data to", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                try
                {
                    ZipFile.ExtractToDirectory(zipFilePathOs, tempExtractFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not extract the binary XBRL data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                // Post process the result
                var postProcessingResult = _postProcessXbrlServiceFiles(outputFolderPathOs, tempExtractFolderPathOs);
                if (!postProcessingResult.Success) return postProcessingResult;

                // Zip the result
                var finalXbrlPackagePathOs = $"{outputFolderPathOs}/sec-ixbrl-package.zip";
                try
                {
                    DirectoryInfo di = new DirectoryInfo(outputFolderPathOs);
                    var files = di.GetFiles();
                    using (var stream = File.OpenWrite(finalXbrlPackagePathOs))
                    using (ZipArchive archive = new ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        foreach (var item in files)
                        {
                            archive.CreateEntryFromFile(item.FullName, item.Name, CompressionLevel.Optimal);
                        }
                    }

                    // - Move the extracted files to the working folder
                    var directoryPathOs = $"{outputFolderPathOs}/files";
                    var targetFolderPathOs = $"{backupRootFolderPathOs}/ixbrl";
                    Directory.CreateDirectory(targetFolderPathOs);
                    foreach (string sourcePathOs in Directory.GetFiles(outputFolderPathOs, "*.*"))
                    {
                        if (sourcePathOs != finalXbrlPackagePathOs)
                        {
                            File.Move(sourcePathOs, $"{targetFolderPathOs}/{Path.GetFileName(sourcePathOs)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not recompile the XBRL package", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }


                // Return success message with the path of the XBRL zip package as the payload
                return new TaxxorReturnMessage(true, $"Successfully generated (i)XBRL package", finalXbrlPackagePathOs, $"outputFolderPathOs: {outputFolderPathOs}");
            }


            /// <summary>
            /// Renders ESMA iXBRL using the Taxxor XBRL Service
            /// </summary>
            /// <param name="xmlDoc"></param>
            /// <param name="projectId"></param>
            /// <param name="outputFolderPathOs"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> RenderEsmaXbrl(XmlDocument xmlDoc, string projectId, string outputFolderPathOs, string backupRootFolderPathOs, string commentContents, string reportRequirementScheme)
            {
                var debugRoutine = siteType == "local" || siteType == "dev";

                // File and folder paths for the working files
                var zipFilePathOs = $"{dataRootPathOs}/temp/ixbrl-{reportRequirementScheme.ToLower()}-package_{CreateTimeBasedString()}.zip";
                var tempExtractFolderPathOs = $"{dataRootPathOs}/temp/{Path.GetFileNameWithoutExtension(zipFilePathOs)}";

                /*
                Render the Inline XBRL files and grab the result in an XML envelope
                */
                if (debugRoutine)
                {
                    await xmlDoc.SaveAsync($"{logRootPathOs}/ixbrl-source-{projectId}-{reportRequirementScheme.ToLower()}.xml");
                }

                var apiUrl = reportRequirementScheme.Contains("OCW") ?
                    $"{GetServiceUrl(ConnectedServiceEnum.XbrlService)}/api/xbrl/create/{reportRequirementScheme}" :
                        (reportRequirementScheme == "dVI" || reportRequirementScheme == "CVO") ?
                            $"{GetServiceUrl(ConnectedServiceEnum.XbrlService)}/api/xbrl/create/{reportRequirementScheme}-ixbrl" :
                                GetServiceUrlByMethodId(ConnectedServiceEnum.XbrlService, "post_api_xbrl_create_ESMA");

                var querystringData = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "docId", commentContents }
                };
                apiUrl = QueryHelpers.AddQueryString(apiUrl, querystringData);

                var xbrlServiceResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.XbrlService, RequestMethodEnum.Post, apiUrl, xmlDoc, true);
                if (XmlContainsError(xbrlServiceResponse))
                {
                    var errorMessage = "Error generating XBRL with the Taxxor XBRL Service";

                    // Attempt tp retrieve details why things went wrong
                    string? xbrlErrorPayload = xbrlServiceResponse.SelectSingleNode("/error/httpresponse")?.InnerText;
                    if (!string.IsNullOrEmpty(xbrlErrorPayload))
                    {
                        var xmlErrorDetails = new XmlDocument();
                        var payloadContent = HttpUtility.HtmlDecode(xbrlErrorPayload);
                        try
                        {
                            xmlErrorDetails.LoadXml(payloadContent);
                            var xbrlErrorDetails = xmlErrorDetails.SelectSingleNode("//message")?.InnerText ?? "";
                            if (xbrlErrorDetails != "") errorMessage += $"\n-------------------------------\nDetails:\n{xbrlErrorDetails}\n-------------------------------";
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Unable to parse the error response of the XBRL Generator");
                        }
                    }
                    return new TaxxorReturnMessage(false, errorMessage, $"debuginfo: {xbrlServiceResponse.SelectSingleNode("/error/debuginfo")?.InnerText ?? $"unknwon..."}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    if (debugRoutine) await xbrlServiceResponse.SaveAsync($"{logRootPathOs}/ixbrl-response-{projectId}-{reportRequirementScheme.ToLower()}.xml");
                }

                // Find the node that contains the zipped base64 encoded data
                var nodePayload = xbrlServiceResponse.SelectSingleNode("/RestResult/payload");

                if (nodePayload == null)
                {
                    return new TaxxorReturnMessage(false, "Could not find the XBRL data node", $"response: {TruncateString(xbrlServiceResponse.OuterXml, 2000)}, stack-trace: {GetStackTrace()}");
                }


                // Convert the received base64 encoded content into a binary
                var base64Content = nodePayload.InnerText;

                // var success = Base64DecodeAsBinaryFile(base64Content, zipFilePathOs);
                // if (!success)
                // {
                //     return new TaxxorReturnMessage(false, "Could not convert the XBRL binary data", $"zipFilePathOs: {zipFilePathOs}, stack-trace: {GetStackTrace()}");
                // }
                // else
                // {
                //     appLogger.LogInformation($"Stored ESMA XBRL package retrieved from the XBRL generator in {zipFilePathOs}");
                // }



                bool success = false;
                try
                {
                    // Convert base64 string to byte array
                    byte[] zipBytes = Base64DecodeToBytes(base64Content);

                    // Ensure the directory exists
                    string? directoryPath = Path.GetDirectoryName(zipFilePathOs);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Write the byte array to the file
                    File.WriteAllBytes(zipFilePathOs, zipBytes);

                    // Verify the file was created and has content
                    if (File.Exists(zipFilePathOs) && new FileInfo(zipFilePathOs).Length > 0)
                    {
                        success = true;
                        appLogger.LogInformation($"Successfully created ZIP file at {zipFilePathOs}");
                    }
                    else
                    {
                        appLogger.LogWarning($"ZIP file was created but appears to be empty: {zipFilePathOs}");
                    }
                }
                catch (FormatException ex)
                {
                    appLogger.LogError(ex, "Invalid base64 string");
                }
                catch (IOException ex)
                {
                    appLogger.LogError(ex, $"IO error while writing ZIP file to {zipFilePathOs}");
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unexpected error while creating ZIP file at {zipFilePathOs}");
                }

                if (!success)
                {
                    return new TaxxorReturnMessage(false, "Could not convert the XBRL binary data", $"zipFilePathOs: {zipFilePathOs}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    appLogger.LogInformation($"Stored ESMA XBRL package retrieved from the XBRL generator in {zipFilePathOs}");
                }

                // Store the original output of the XBRL engine for logging purposes
                try
                {
                    var backupZipFilePathOs = $"{logRootPathOs}/xbrlservice-output-{reportRequirementScheme.ToLower()}.zip";
                    if (File.Exists(backupZipFilePathOs)) File.Delete(backupZipFilePathOs);
                    File.Copy(zipFilePathOs, backupZipFilePathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not copy the output of the XBRL service");
                }

                // Extract the ZIP archive to a temporary directory
                try
                {
                    Directory.CreateDirectory(tempExtractFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not create a folder to extract XBRL data to", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                try
                {
                    if (!File.Exists(zipFilePathOs))
                    {
                        throw new FileNotFoundException($"ZIP file not found at {zipFilePathOs}");
                    }

                    using var archive = ZipFile.OpenRead(zipFilePathOs);
                    if (!archive.Entries.Any())
                    {
                        throw new InvalidDataException("ZIP file is empty or invalid");
                    }

                    foreach (var entry in archive.Entries)
                    {
                        var destinationPath = Path.GetFullPath(Path.Combine(tempExtractFolderPathOs, entry.FullName));

                        if (destinationPath.StartsWith(tempExtractFolderPathOs, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log more details about the exception
                    appLogger.LogError(ex, $"Error extracting ZIP file. File size: {new FileInfo(zipFilePathOs).Length} bytes");
                    return new TaxxorReturnMessage(false, "Could not extract the binary XBRL data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }


                // try
                // {
                //     ZipFile.ExtractToDirectory(zipFilePathOs, tempExtractFolderPathOs);
                // }
                // catch (Exception ex)
                // {
                //     return new TaxxorReturnMessage(false, "Could not extract the binary XBRL data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                // }

                // Post process the result
                var postProcessingResult = _postProcessXbrlServiceFiles(outputFolderPathOs, tempExtractFolderPathOs);
                if (!postProcessingResult.Success) return postProcessingResult;

                // Zip up the result
                var xbrlZipFilePostFix = "";
                var finalXbrlPackagePathOs = "";
                try
                {
                    // - filename of the ZIP file is the name of the first directory
                    var rootFolderName = "";
                    foreach (var dir in Directory.GetDirectories(outputFolderPathOs))
                    {
                        rootFolderName = Path.GetFileName(dir);
                        break;
                    }

                    if (rootFolderName == "")
                    {
                        // - filename of the ZIP file is the name of the first directory
                        var zipFileNameWithoutExtension = $"ixbrl-{reportRequirementScheme.ToLower()}-package";

                        // - Move the ZIP file to it's final location
                        var tempXbrlPackagePathOs = $"{Path.GetDirectoryName(outputFolderPathOs)}/{zipFileNameWithoutExtension}.zip";
                        finalXbrlPackagePathOs = $"{outputFolderPathOs}/{zipFileNameWithoutExtension}{xbrlZipFilePostFix}.zip";
                        // ZipFile.CreateFromDirectory(outputFolderPathOs, tempXbrlPackagePathOs);
                        File.Move(zipFilePathOs, finalXbrlPackagePathOs);

                        // - Move the extracted files to the working folder
                        var targetFolderPathOs = $"{backupRootFolderPathOs}/ixbrl/{zipFileNameWithoutExtension}";
                        Directory.CreateDirectory(targetFolderPathOs);
                        CopyDirectoryRecursive(tempExtractFolderPathOs, targetFolderPathOs);
                        DelTree($"{outputFolderPathOs}/{zipFileNameWithoutExtension}");
                    }
                    else
                    {
                        // - Generate the zip file
                        var tempXbrlPackagePathOs = $"{Path.GetDirectoryName(outputFolderPathOs)}/{rootFolderName}.zip";
                        finalXbrlPackagePathOs = $"{outputFolderPathOs}/{rootFolderName}{xbrlZipFilePostFix}.zip";
                        ZipFile.CreateFromDirectory(outputFolderPathOs, tempXbrlPackagePathOs);
                        File.Move(tempXbrlPackagePathOs, finalXbrlPackagePathOs);

                        // - Move the extracted files to the working folder
                        var targetFolderPathOs = $"{backupRootFolderPathOs}/ixbrl/{rootFolderName}";
                        Directory.CreateDirectory(targetFolderPathOs);
                        CopyDirectoryRecursive($"{outputFolderPathOs}/{rootFolderName}", targetFolderPathOs);
                        DelTree($"{outputFolderPathOs}/{rootFolderName}");
                    }

                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not recompile the XBRL package", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                // Return success message with the path of the XBRL zip package as the payload
                return new TaxxorReturnMessage(true, "Successfully generated (i)XBRL package", finalXbrlPackagePathOs, $"outputFolderPathOs: {outputFolderPathOs}");
            }

            /// <summary>
            /// Renders KVK iXBRL using the Taxxor XBRL Service
            /// </summary>
            /// <param name="xmlDoc"></param>
            /// <param name="projectId"></param>
            /// <param name="outputFolderPathOs"></param>
            /// <param name="backupRootFolderPathOs"></param>
            /// <param name="commentContents"></param>
            /// <returns></returns>
            public static async Task<TaxxorReturnMessage> RenderPlainXbrl(XmlDocument xmlDoc, string projectId, string outputFolderPathOs, string backupRootFolderPathOs, string commentContents, string reportRequirementScheme)
            {
                var debugRoutine = siteType == "local" || siteType == "dev";

                // File and folder paths for the working files
                var zipFilePathOs = $"{dataRootPathOs}/temp/xbrl-{reportRequirementScheme.ToLower()}-package_{CreateTimeBasedString()}.zip";
                var tempExtractFolderPathOs = $"{dataRootPathOs}/temp/{Path.GetFileNameWithoutExtension(zipFilePathOs)}";

                /*
                Render the Inline XBRL files and grab the result in an XML envelope
                */
                if (debugRoutine)
                {
                    await xmlDoc.SaveAsync($"{logRootPathOs}/xbrl-source-{projectId}-{reportRequirementScheme.ToLower()}.xml");
                }

                var apiUrl = $"{GetServiceUrl(ConnectedServiceEnum.XbrlService)}/api/xbrl/create/{reportRequirementScheme}";
                var querystringData = new Dictionary<string, string>
                {
                    { "pid", projectId },
                    { "docId", commentContents }
                };
                apiUrl = QueryHelpers.AddQueryString(apiUrl, querystringData);

                var xbrlServiceResponse = await CallTaxxorConnectedService<XmlDocument>(ConnectedServiceEnum.XbrlService, RequestMethodEnum.Post, apiUrl, xmlDoc, true);
                if (XmlContainsError(xbrlServiceResponse))
                {
                    var errorMessage = "Error generating plain XBRL with the Taxxor XBRL Service";

                    // Attempt tp retrieve details why things went wrong
                    string? xbrlErrorPayload = xbrlServiceResponse.SelectSingleNode("/error/httpresponse")?.InnerText;
                    if (!string.IsNullOrEmpty(xbrlErrorPayload))
                    {
                        var xmlErrorDetails = new XmlDocument();
                        var payloadContent = HttpUtility.HtmlDecode(xbrlErrorPayload);
                        try
                        {
                            xmlErrorDetails.LoadXml(payloadContent);
                            var xbrlErrorDetails = xmlErrorDetails.SelectSingleNode("//message")?.InnerText ?? "";
                            if (xbrlErrorDetails != "") errorMessage += $"\n-------------------------------\nDetails:\n{xbrlErrorDetails}\n-------------------------------";
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Unable to parse the error response of the XBRL Generator");
                        }
                    }
                    return new TaxxorReturnMessage(false, errorMessage, $"debuginfo: {xbrlServiceResponse.SelectSingleNode("/error/debuginfo")?.InnerText ?? $"unknwon..."}, stack-trace: {GetStackTrace()}");
                }

                // Find the node that contains the zipped base64 encoded data
                var nodePayload = xbrlServiceResponse.SelectSingleNode("/RestResult/payload");

                if (nodePayload == null)
                {
                    return new TaxxorReturnMessage(false, "Could not find the XBRL data node", $"response: {TruncateString(xbrlServiceResponse.OuterXml, 2000)}, stack-trace: {GetStackTrace()}");
                }

                // Convert the received base64 encoded content into a binary
                var base64Content = nodePayload.InnerText;
                var success = Base64DecodeAsBinaryFile(base64Content, zipFilePathOs);
                if (!success)
                {
                    return new TaxxorReturnMessage(false, "Could not convert the XBRL binary data", $"zipFilePathOs: {zipFilePathOs}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    appLogger.LogInformation($"Stored {reportRequirementScheme} XBRL package retrieved from the XBRL generator in {zipFilePathOs}");
                }

                // Store the original output of the XBRL engine for logging purposes
                try
                {
                    var backupZipFilePathOs = $"{logRootPathOs}/xbrlservice-output-{reportRequirementScheme.ToLower()}.zip";
                    if (File.Exists(backupZipFilePathOs)) File.Delete(backupZipFilePathOs);
                    File.Copy(zipFilePathOs, backupZipFilePathOs);
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Could not copy the output of the XBRL service");
                }



                // Extract the ZIP archive to a temporary directory
                try
                {
                    Directory.CreateDirectory(tempExtractFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not create a folder to extract XBRL data to", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                try
                {
                    ZipFile.ExtractToDirectory(zipFilePathOs, tempExtractFolderPathOs);
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not extract the binary XBRL data", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                // Zip up the result
                var finalXbrlPackagePathOs = "";
                try
                {
                    // - filename of the ZIP file is the name of the first directory
                    var zipFileNameWithoutExtension = $"xbrl-{reportRequirementScheme.ToLower()}-package";

                    // - Move the ZIP file to it's final location
                    var tempXbrlPackagePathOs = $"{Path.GetDirectoryName(outputFolderPathOs)}/{zipFileNameWithoutExtension}.zip";
                    finalXbrlPackagePathOs = $"{outputFolderPathOs}/{zipFileNameWithoutExtension}.zip";
                    // ZipFile.CreateFromDirectory(outputFolderPathOs, tempXbrlPackagePathOs);
                    File.Move(zipFilePathOs, finalXbrlPackagePathOs);

                    // - Move the extracted files to the working folder
                    var targetFolderPathOs = $"{backupRootFolderPathOs}/xbrl/{zipFileNameWithoutExtension}";
                    Directory.CreateDirectory(targetFolderPathOs);
                    CopyDirectoryRecursive(tempExtractFolderPathOs, targetFolderPathOs);
                    DelTree($"{outputFolderPathOs}/{zipFileNameWithoutExtension}");
                }
                catch (Exception ex)
                {
                    return new TaxxorReturnMessage(false, "Could not recompile the XBRL package", $"error: {ex}, stack-trace: {GetStackTrace()}");
                }

                // Return success message with the path of the XBRL zip package as the payload
                return new TaxxorReturnMessage(true, "Successfully generated (i)XBRL package", finalXbrlPackagePathOs, $"outputFolderPathOs: {outputFolderPathOs}");
            }

            /// <summary>
            /// Routine that moves the log files generated by the XBRL Service to another folder
            /// </summary>
            /// <param name="outputFolderPathOs"></param>
            /// <param name="tempExtractFolderPathOs"></param>
            /// <returns></returns>
            private static TaxxorReturnMessage _postProcessXbrlServiceFiles(string outputFolderPathOs, string tempExtractFolderPathOs)
            {
                // Copy the extracted files to the output directory
                var xbrlFilesToCopy = new Dictionary<string, string>();

                // - Test if all files are in the root directory
                var xbrlFilingInSubDir = Directory.GetFiles(tempExtractFolderPathOs, "*.*", SearchOption.TopDirectoryOnly).Length == 1;
                var subDirectoryName = "";
                if (xbrlFilingInSubDir)
                {
                    var directories = Directory.GetDirectories(tempExtractFolderPathOs);
                    if (directories.Length > 0)
                    {
                        subDirectoryName = Path.GetFileName(directories[0]);
                    }
                    else
                    {
                        xbrlFilingInSubDir = false;
                    }
                }


                // - Calculate source and target paths and store in dictionary
                string[] filePaths = Directory.GetFiles(tempExtractFolderPathOs, "*.*", SearchOption.AllDirectories);
                if (filePaths.Length > 1)
                {
                    foreach (string sourcePathOs in filePaths)
                    {
                        if (Path.GetFileName(sourcePathOs) == "log.xml")
                        {
                            xbrlFilesToCopy.Add(sourcePathOs, $"{Path.GetDirectoryName(Path.GetDirectoryName(outputFolderPathOs))}/logs/_xbrlservice.xml");
                        }
                        else if (Path.GetExtension(sourcePathOs) == ".log")
                        {
                            var targetLogFilePathOs = $"{Path.GetDirectoryName(Path.GetDirectoryName(outputFolderPathOs))}/logs/_xbrlservice-{Path.GetFileName(sourcePathOs)}";
                            var postFixCounter = 1;
                            while (xbrlFilesToCopy.ContainsValue(targetLogFilePathOs))
                            {
                                targetLogFilePathOs = targetLogFilePathOs.Replace(".log", $"-{postFixCounter}.log");
                                postFixCounter++;
                            }
                        }
                        else
                        {
                            var targetFilePathOs = sourcePathOs.Replace(tempExtractFolderPathOs, outputFolderPathOs);
                            if (xbrlFilingInSubDir) targetFilePathOs = targetFilePathOs.Replace($"/{subDirectoryName}/", "/");

                            //appLogger.LogInformation($"- targetFileName: {targetFileName}");
                            xbrlFilesToCopy.Add(sourcePathOs, targetFilePathOs);
                        }
                    }

                    // - Copy the files
                    foreach (var pair in xbrlFilesToCopy)
                    {
                        var sourcePathOs = pair.Key;
                        var targetPathOs = pair.Value;
                        try
                        {
                            var targetDirectoryPathOs = Path.GetDirectoryName(targetPathOs);
                            if (!Directory.Exists(targetDirectoryPathOs)) Directory.CreateDirectory(targetDirectoryPathOs);

                            // appLogger.LogWarning(sourcePathOs + " -> " + targetPathOs);

                            File.Copy(sourcePathOs, targetPathOs, true);
                        }
                        catch (Exception e)
                        {
                            appLogger.LogError(e, "Unable to copy all XBRL files");
                            return new TaxxorReturnMessage(false, "Could not move XBRL data files", $"sourcePathOs: {sourcePathOs}, targetPathOs: {targetPathOs}, error: {e.ToString()}, stack-trace: {GetStackTrace()}");
                        }
                    }
                }


                // Potentially cleanup the temporary working files and folders

                // Return success message
                return new TaxxorReturnMessage(true, "Successfully post processed XBRL Service result");
            }


        }

    }
}