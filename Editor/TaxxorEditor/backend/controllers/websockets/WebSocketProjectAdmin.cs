using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;
using static Taxxor.ConnectedServices.DocumentStoreService;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Utilities used in the project admin page
        /// </summary>
        public partial class WebSocketsHub : Hub
        {

            /// <summary>
            /// Replaces the properties of a SDE ERP mapping
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="dataReference"></param>
            /// <param name="erpDataset"></param>
            /// <param name="searchPhrase"></param>
            /// <param name="replacePhrase"></param>
            /// <param name="regExReplaceMode"></param>
            /// <param name="testRun"></param>
            /// <returns></returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> MappingClusterSearchReplace(string projectId, string dataReference, string erpDataset, string searchPhrase, string replacePhrase, bool regExReplaceMode, bool testRun)
            {
                var errorMessage = "There was an error search and replace in the mappings";
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);
                var debugRoutine = (siteType == "local" || siteType == "dev");
                var replaceResult = new StringBuilder();

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) securityCheckResult.DebugInfo = "";
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("dataReference", dataReference, RegexEnum.FileName, true);
                    inputValidationCollection.Add("erpDataset", erpDataset, RegexEnum.Default, false);
                    inputValidationCollection.Add("searchPhrase", searchPhrase, @".{2,512}", true);
                    inputValidationCollection.Add("replacePhrase", replacePhrase, @".{0,512}", false);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) validationResult.DebugInfo = "";
                        return validationResult;
                    }

                    //
                    // => Generate a project variables object
                    //
                    var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                    if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                    var overallReplaceResult = await Taxxor.Project.ProjectLogic.MappingServiceClusterSearchReplace(projectVars, dataReference, erpDataset, searchPhrase, replacePhrase, regExReplaceMode, testRun);

                    return overallReplaceResult;


                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }

            /// <summary>
            /// Initiates a new project data import session
            /// </summary>
            /// <param name="projectId">The target project ID</param>
            /// <param name="fileName">The ZIP filename being uploaded</param>
            /// <param name="importMode">The import mode: "replace" or "append"</param>
            /// <returns>TaxxorReturnMessage with sessionId in debugInfo</returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> InitiateProjectDataImport(string projectId, string fileName, string importMode)
            {
                var errorMessage = "There was an error initiating the data import";
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);
                var debugRoutine = (siteType == "local" || siteType == "dev");

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) securityCheckResult.DebugInfo = "";
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation for string values
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("projectId", projectId, @"^[a-zA-Z0-9\-_]{2,128}$", true);
                    inputValidationCollection.Add("fileName", fileName, RegexEnum.FileName, true);
                    inputValidationCollection.Add("importMode", importMode, @"^(replace|append)$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) validationResult.DebugInfo = "";
                        return validationResult;
                    }

                    // Verify fileName ends with .zip
                    if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        var zipError = new TaxxorReturnMessage(false, "File must be a ZIP archive", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) zipError.DebugInfo = "";
                        return zipError;
                    }

                    //
                    // => Generate a project variables object
                    //
                    var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId);
                    if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

                    //
                    // => Clean up and prepare temp directory
                    //
                    var tempUploadPath = CalculateFullPathOs("/temp/import", "dataroot");

                    // Clean up any previously stored content
                    if (Directory.Exists(tempUploadPath))
                    {
                        var existingFiles = Directory.GetFiles(tempUploadPath);
                        foreach (var existingFile in existingFiles)
                        {
                            try
                            {
                                File.Delete(existingFile);
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogWarning($"Could not delete existing temp file {existingFile}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(tempUploadPath);
                    }

                    //
                    // => Create new session
                    //
                    var sessionId = Guid.NewGuid().ToString();
                    var tempFilePath = Path.Combine(tempUploadPath, $"{sessionId}.zip");

                    // Store session information in Context.Items
                    Context.Items["ImportSessionId"] = sessionId;
                    Context.Items["ImportFileName"] = fileName;
                    Context.Items["ImportProjectId"] = projectId;
                    Context.Items["ImportTempFilePath"] = tempFilePath;
                    Context.Items["ImportBytesReceived"] = 0L;
                    Context.Items["ImportMode"] = importMode;

                    // Create FileStream for writing chunks
                    var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
                    Context.Items["ImportFileStream"] = fileStream;

                    appLogger.LogInformation($"Data import session initiated: {sessionId} for project {projectId}, file {fileName}, mode {importMode}");

                    var responseData = new
                    {
                        sessionId = sessionId,
                        fileName = fileName,
                        projectId = projectId,
                        importMode = importMode
                    };
                    return new TaxxorReturnMessage(
                        true,
                        "Import session initiated",
                        (projectVars.currentUser.Permissions.ViewDeveloperTools) ? JsonSerializer.Serialize(responseData) : ""
                    );
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }

            /// <summary>
            /// Receives and writes a chunk of the import file
            /// </summary>
            /// <param name="sessionId">The session ID from InitiateProjectDataImport</param>
            /// <param name="base64Chunk">Base64-encoded chunk data</param>
            /// <param name="chunkIndex">Zero-based index of this chunk</param>
            /// <returns>TaxxorReturnMessage with progress info</returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> ImportProjectDataChunk(string sessionId, string base64Chunk, int chunkIndex)
            {
                var errorMessage = "There was an error uploading the data chunk";
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);
                var debugRoutine = (siteType == "local" || siteType == "dev");

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError(securityCheckResult.ToString());
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) securityCheckResult.DebugInfo = "";
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("sessionId", sessionId, @"^[a-zA-Z0-9\-]{36}$", true);
                    inputValidationCollection.Add("base64Chunk", base64Chunk, @"^[A-Za-z0-9+/=]{1,}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError(validationResult.ToString());
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) validationResult.DebugInfo = "";
                        return validationResult;
                    }

                    // Validate chunkIndex
                    if (chunkIndex < 0)
                    {
                        var indexError = new TaxxorReturnMessage(false, "Chunk index must be non-negative", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) indexError.DebugInfo = "";
                        return indexError;
                    }

                    //
                    // => Verify session exists
                    //
                    if (!Context.Items.ContainsKey("ImportSessionId") ||
                        Context.Items["ImportSessionId"]?.ToString() != sessionId)
                    {
                        var sessionError = new TaxxorReturnMessage(false, "Invalid or expired upload session", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) sessionError.DebugInfo = "";
                        return sessionError;
                    }

                    var fileStream = Context.Items["ImportFileStream"] as FileStream;
                    if (fileStream == null)
                    {
                        var streamError = new TaxxorReturnMessage(false, "Upload session not properly initialized", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) streamError.DebugInfo = "";
                        return streamError;
                    }

                    //
                    // => Decode and write chunk
                    //
                    byte[] chunkData = Convert.FromBase64String(base64Chunk);
                    await fileStream.WriteAsync(chunkData, 0, chunkData.Length);

                    // Update bytes received counter
                    var currentBytes = (long)(Context.Items["ImportBytesReceived"] ?? 0L);
                    currentBytes += chunkData.Length;
                    Context.Items["ImportBytesReceived"] = currentBytes;

                    if (debugRoutine)
                    {
                        appLogger.LogInformation($"Chunk {chunkIndex} received for session {sessionId}: {chunkData.Length} bytes (total: {currentBytes})");
                    }

                    var responseData = new
                    {
                        chunkIndex = chunkIndex,
                        bytesReceived = currentBytes
                    };

                    return new TaxxorReturnMessage(
                        true,
                        $"Chunk {chunkIndex} received",
                        (projectVars.currentUser.Permissions.ViewDeveloperTools) ? JsonSerializer.Serialize(responseData) : ""
                    );
                }
                catch (FormatException ex)
                {
                    appLogger.LogError(ex, "Invalid Base64 encoding in chunk data");
                    return new TaxxorReturnMessage(false, "Invalid chunk data encoding", ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, errorMessage);
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }

            /// <summary>
            /// Finalizes the import, verifies ZIP integrity, and closes the session
            /// </summary>
            /// <param name="sessionId">The session ID from InitiateProjectDataImport</param>
            /// <returns>TaxxorReturnMessage with file details</returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> FinalizeProjectDataImport(string sessionId)
            {
                var errorMessage = "There was an error finalizing the data import";
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);
                var debugRoutine = (siteType == "local" || siteType == "dev");

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError($"FinalizeProjectDataImport: Security check failed - {securityCheckResult.ToString()}");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) securityCheckResult.DebugInfo = "";
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("sessionId", sessionId, @"^[a-zA-Z0-9\-]{36}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError($"FinalizeProjectDataImport: Input validation failed - {validationResult.ToString()}");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) validationResult.DebugInfo = "";
                        return validationResult;
                    }

                    //
                    // => Verify session exists
                    //
                    if (!Context.Items.ContainsKey("ImportSessionId") ||
                        Context.Items["ImportSessionId"]?.ToString() != sessionId)
                    {
                        appLogger.LogError($"FinalizeProjectDataImport: Invalid or expired session - {sessionId}");
                        var sessionError = new TaxxorReturnMessage(false, "Invalid or expired upload session", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) sessionError.DebugInfo = "";
                        return sessionError;
                    }

                    var fileStream = Context.Items["ImportFileStream"] as FileStream;
                    var tempFilePath = Context.Items["ImportTempFilePath"]?.ToString();
                    var fileName = Context.Items["ImportFileName"]?.ToString();
                    var projectId = Context.Items["ImportProjectId"]?.ToString();
                    var importMode = Context.Items["ImportMode"]?.ToString();
                    var totalBytes = (long)(Context.Items["ImportBytesReceived"] ?? 0L);

                    //
                    // => Close and flush file stream
                    //
                    if (fileStream != null)
                    {
                        await fileStream.FlushAsync();
                        fileStream.Close();
                        fileStream.Dispose();
                        Context.Items["ImportFileStream"] = null;
                    }
                    else
                    {
                        appLogger.LogWarning($"FinalizeProjectDataImport: FileStream was null for session {sessionId}");
                    }

                    //
                    // => Verify ZIP file integrity
                    //
                    if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                    {
                        // Check ZIP magic bytes: PK\x03\x04 (50 4B 03 04) or PK\x05\x06 (50 4B 05 06)
                        using (var fs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                        {
                            if (fs.Length < 4)
                            {
                                appLogger.LogError($"FinalizeProjectDataImport: File too small ({fs.Length} bytes)");
                                var sizeError = new TaxxorReturnMessage(false, "File too small to be a valid ZIP", "");
                                if (!projectVars.currentUser.Permissions.ViewDeveloperTools) sizeError.DebugInfo = "";
                                return sizeError;
                            }

                            byte[] magicBytes = new byte[4];
                            await fs.ReadExactlyAsync(magicBytes.AsMemory(0, 4));

                            bool isValidZip = (magicBytes[0] == 0x50 && magicBytes[1] == 0x4B &&
                                             (magicBytes[2] == 0x03 && magicBytes[3] == 0x04 ||
                                              magicBytes[2] == 0x05 && magicBytes[3] == 0x06));

                            if (!isValidZip)
                            {
                                appLogger.LogError($"FinalizeProjectDataImport: Invalid ZIP magic bytes - {magicBytes[0]:X2} {magicBytes[1]:X2} {magicBytes[2]:X2} {magicBytes[3]:X2}");
                                var zipError = new TaxxorReturnMessage(false, "Invalid ZIP file format", "");
                                if (!projectVars.currentUser.Permissions.ViewDeveloperTools) zipError.DebugInfo = "File does not contain valid ZIP magic bytes";
                                return zipError;
                            }
                        }
                    }
                    else
                    {
                        appLogger.LogError($"FinalizeProjectDataImport: File not found at '{tempFilePath}'");
                        var fileError = new TaxxorReturnMessage(false, "Uploaded file not found", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) fileError.DebugInfo = "";
                        return fileError;
                    }

                    //
                    // => Rename ZIP file to data.zip
                    //
                    var tempDirectory = Path.GetDirectoryName(tempFilePath);
                    var normalizedFilePath = Path.Combine(tempDirectory, "data.zip");

                    // Delete existing data.zip if it exists
                    if (File.Exists(normalizedFilePath))
                    {
                        File.Delete(normalizedFilePath);
                    }

                    // Rename the uploaded file to data.zip
                    File.Move(tempFilePath, normalizedFilePath);

                    //
                    // => Extract ZIP contents to temporary folder
                    //
                    try
                    {
                        ZipFile.ExtractToDirectory(normalizedFilePath, tempDirectory, overwriteFiles: true);
                    }
                    catch (Exception extractEx)
                    {
                        appLogger.LogError(extractEx, $"FinalizeProjectDataImport: Failed to extract ZIP contents");
                        var extractError = new TaxxorReturnMessage(false, "Failed to extract ZIP file contents", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) extractError.DebugInfo = extractEx.Message;
                        return extractError;
                    }


                    //
                    // => Process the data that was uploaded
                    //
                    var resultsToShow = new List<string>
                    {
                        $"- projectId: {projectId}",
                        $"- importMode: {importMode}"
                    };

                    //
                    // => Global variables
                    //
                    var stopOnMultiplePdfVariants = false;

                    //
                    // => Test if uploaded files are present
                    //
                    var uploadedDataFilePathOs = CalculateFullPathOs("/temp/import/data.zip", "dataroot");
                    resultsToShow.Add($"- uploadedDataFilePathOs: {uploadedDataFilePathOs}");
                    if (!File.Exists(uploadedDataFilePathOs)) throw new Exception("Unable to locate uploaded data file");

                    var uploadedDataManifestFilePathOs = CalculateFullPathOs("/temp/import/manifest.yml", "dataroot");
                    resultsToShow.Add($"- uploadedDataManifestFilePathOs: {uploadedDataManifestFilePathOs}");
                    if (!File.Exists(uploadedDataManifestFilePathOs)) throw new Exception("Unable to locate uploaded data manifest file");

                    //
                    // => Validate manifest file - check if all referenced files exist
                    //
                    var importBasePath = Path.GetDirectoryName(uploadedDataManifestFilePathOs);
                    var manifestContent = await File.ReadAllTextAsync(uploadedDataManifestFilePathOs);
                    var lines = manifestContent.Split('\n');

                    var filesToCheck = new List<string>();
                    var missingFiles = new List<string>();
                    var currentSection = "";

                    string hierarchyFilePathOs = null;
                    var sectionXmlFiles = new List<string>();
                    var imageFiles = new List<string>();

                    // Parse YAML manifest to extract file paths
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();

                        // Detect sections
                        if (trimmedLine.StartsWith("hierarchy:"))
                        {
                            currentSection = "hierarchy";
                        }
                        else if (trimmedLine.StartsWith("sections:"))
                        {
                            currentSection = "sections";
                        }
                        else if (trimmedLine.StartsWith("images:"))
                        {
                            currentSection = "images";
                        }
                        else if (trimmedLine.StartsWith("file:") && currentSection == "hierarchy")
                        {
                            // Extract file path after "file:"
                            var filePath = trimmedLine.Substring(5).Trim();
                            filesToCheck.Add(filePath);

                            // Store the full path to the hierarchy file for later use
                            hierarchyFilePathOs = Path.Combine(importBasePath, filePath);
                        }
                        else if (trimmedLine.StartsWith("- ") && (currentSection == "sections" || currentSection == "images"))
                        {
                            // Extract file path from list item
                            var filePath = trimmedLine.Substring(2).Trim();
                            filesToCheck.Add(filePath);

                            // Store the full path based on section
                            var fullPath = Path.Combine(importBasePath, filePath);
                            if (currentSection == "sections")
                            {
                                sectionXmlFiles.Add(fullPath);
                            }
                            else if (currentSection == "images")
                            {
                                imageFiles.Add(fullPath);
                            }
                        }
                    }

                    resultsToShow.Add($"- Total files referenced in manifest: {filesToCheck.Count}");

                    // Check each file exists
                    foreach (var relativeFilePath in filesToCheck)
                    {
                        var fullFilePath = Path.Combine(importBasePath, relativeFilePath);

                        if (!File.Exists(fullFilePath))
                        {
                            missingFiles.Add(relativeFilePath);
                        }
                    }

                    if (missingFiles.Count > 0)
                    {
                        resultsToShow.Add($"- ERROR: {missingFiles.Count} file(s) missing:");
                        foreach (var missingFile in missingFiles)
                        {
                            resultsToShow.Add($"  * {missingFile}");
                        }
                        throw new Exception($"Manifest validation failed: {missingFiles.Count} file(s) referenced in manifest are missing from the upload");
                    }
                    else
                    {
                        resultsToShow.Add($"- Manifest validation: SUCCESS - All {filesToCheck.Count} files exist");
                        resultsToShow.Add($"- hierarchyFilePathOs: {hierarchyFilePathOs}");
                    }

                    resultsToShow.Add($"- sectionXmlFiles.Count: {sectionXmlFiles.Count}");
                    resultsToShow.Add($"- imageFiles.Count: {imageFiles.Count}");

                    // resultsToShow.Add($"- hierarchyFilePathOs: {hierarchyFilePathOs}");

                    //
                    // => Check how many PDF output channels are defined for this project
                    //
                    var nodeListPdfOutputChannels = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='pdf']/variants/variant");
                    resultsToShow.Add($"- Total PDF output channels: {nodeListPdfOutputChannels.Count}");
                    if (nodeListPdfOutputChannels.Count == 0) throw new Exception("No PDF output channels defined for this project");
                    if (nodeListPdfOutputChannels.Count > 1 && stopOnMultiplePdfVariants) throw new Exception("Multiple PDF output channels defined for this project - not supported in automatic import");
                    // resultsToShow.Add($"- nodeListPdfOutputChannels.Item(0).OuterXml: {HtmlEncodeForDisplay(nodeListPdfOutputChannels.Item(0).OuterXml)}");
                    var outputChannelVariantId = nodeListPdfOutputChannels.Item(0).GetAttribute("id");
                    var outputChannelLanguage = nodeListPdfOutputChannels.Item(0).GetAttribute("lang");
                    resultsToShow.Add($"- outputChannelVariantId: {outputChannelVariantId}");
                    resultsToShow.Add($"- outputChannelLanguage: {outputChannelLanguage}");

                    //
                    // => Load the current hierarchy file and prepare for updates
                    //
                    XmlDocument xmlOriginalHierarchy = await FilingData.LoadHierarchy(projectVars.projectId, projectVars.versionId, projectVars.editorId, "pdf", outputChannelVariantId, outputChannelLanguage);
                    // resultsToShow.Add($"- xmlOriginalHierarchy: {HtmlEncodeForDisplay(xmlOriginalHierarchy.OuterXml)}");
                    if (XmlContainsError(xmlOriginalHierarchy))
                    {
                        appLogger.LogError($"Unable to load original output channel hierarchy from the document store for projectId: {projectVars.projectId}, versionId: {projectVars.versionId}, editorId: {projectVars.editorId}, outputChannelType: pdf, outputChannelVariantId: {outputChannelVariantId}, outputChannelLanguage: {outputChannelLanguage}");
                        throw new Exception("Unable to load original output channel hierarchy from the document store");
                    }

                    //
                    // => Create a pre-import version
                    //
                    var preSyncVerionResult = await GenerateVersion(projectId, $"Pre import data version", false, projectVars);
                    if (!preSyncVerionResult.Success)
                    {
                        appLogger.LogError($"Unable to create pre-import version. Error: {preSyncVerionResult.Message}, DebugInfo: {preSyncVerionResult.DebugInfo}");
                        throw new Exception($"Unable to create pre-import version. Error: {preSyncVerionResult.Message}");
                    }

                    //
                    // => Post process the hierarchy file - depending on the import mode
                    //

                    // Load the imported hierarchy file
                    var xmlImportedHierarchy = new XmlDocument();
                    xmlImportedHierarchy.Load(hierarchyFilePathOs);
                    resultsToShow.Add($"- Loaded imported hierarchy from: {hierarchyFilePathOs}");

                    //
                    // => Post process the hierarchy file - depending on the import mode
                    //
                    if (importMode == "replace")
                    {
                        resultsToShow.Add($"- Import mode: REPLACE");

                        // In the original hierarchy, remove all items between "toc" and "back-cover"
                        var nodeOriginalSubItems = xmlOriginalHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                        if (nodeOriginalSubItems == null)
                        {
                            throw new Exception("Could not find /items/structured/item/sub_items in original hierarchy");
                        }

                        // Find the toc and back-cover nodes in original hierarchy
                        var nodeToc = nodeOriginalSubItems.SelectSingleNode("item[@id='toc']");
                        var nodeBackCover = nodeOriginalSubItems.SelectSingleNode("item[@id='back-cover']");

                        if (nodeToc == null)
                        {
                            throw new Exception("Could not find toc marker in original hierarchy");
                        }

                        if (nodeBackCover == null)
                        {
                            throw new Exception("Could not find back-cover marker in original hierarchy");
                        }

                        // Remove all nodes between toc and back-cover
                        var nodesToRemove = new List<XmlNode>();
                        var currentNode = nodeToc.NextSibling;

                        while (currentNode != null && currentNode != nodeBackCover)
                        {
                            // Only collect element nodes (skip text nodes, comments, etc.)
                            if (currentNode.NodeType == XmlNodeType.Element)
                            {
                                nodesToRemove.Add(currentNode);
                            }
                            currentNode = currentNode.NextSibling;
                        }

                        resultsToShow.Add($"- Removing {nodesToRemove.Count} items from original hierarchy between toc and back-cover");
                        foreach (var nodeToRemove in nodesToRemove)
                        {
                            var removedId = nodeToRemove.Attributes?["id"]?.Value ?? "unknown";
                            resultsToShow.Add($"  - Removed item: {removedId}");
                            nodeOriginalSubItems.RemoveChild(nodeToRemove);
                        }

                        // Get all items from imported hierarchy (no toc/back-cover markers in imported hierarchy)
                        var nodeImportedSubItems = xmlImportedHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                        if (nodeImportedSubItems == null)
                        {
                            throw new Exception("Could not find /items/structured/item/sub_items in imported hierarchy");
                        }

                        // Collect ALL item nodes from imported hierarchy
                        var importedItems = nodeImportedSubItems.SelectNodes("item");
                        if (importedItems == null || importedItems.Count == 0)
                        {
                            resultsToShow.Add($"- WARNING: No items found in imported hierarchy");
                        }
                        else
                        {
                            resultsToShow.Add($"- Inserting {importedItems.Count} items from imported hierarchy into original hierarchy");

                            // Insert imported nodes into original hierarchy between toc and back-cover
                            var insertAfterNode = nodeToc;
                            foreach (XmlNode importedItem in importedItems)
                            {
                                var importedId = importedItem.Attributes?["id"]?.Value ?? "unknown";
                                resultsToShow.Add($"  - Inserting item: {importedId}");

                                // Import the node into the original document
                                var importedNode = xmlOriginalHierarchy.ImportNode(importedItem, true);

                                // Insert after the current position
                                nodeOriginalSubItems.InsertAfter(importedNode, insertAfterNode);

                                // Update the insertion point for the next iteration
                                insertAfterNode = importedNode;
                            }

                            resultsToShow.Add($"- Successfully merged hierarchies in REPLACE mode");
                        }
                    }
                    else if (importMode == "append")
                    {
                        resultsToShow.Add($"- Import mode: APPEND");

                        //
                        // Step 0: Check if "import-root" already exists and remove it
                        //
                        var sectionId = "import-root";
                        var sectionTitle = "Imported Structure";

                        var nodeOriginalSubItems = xmlOriginalHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                        if (nodeOriginalSubItems == null)
                        {
                            throw new Exception("Could not find /items/structured/item/sub_items in original hierarchy");
                        }

                        var existingImportRootItem = nodeOriginalSubItems.SelectSingleNode($"item[@id='{sectionId}']");
                        if (existingImportRootItem != null)
                        {
                            resultsToShow.Add($"- Found existing '{sectionId}' item in hierarchy - removing it");
                            nodeOriginalSubItems.RemoveChild(existingImportRootItem);
                            resultsToShow.Add($"- Removed existing '{sectionId}' item");
                        }
                        else
                        {
                            resultsToShow.Add($"- No existing '{sectionId}' item found - proceeding with clean import");
                        }

                        //
                        // Step 1: Create a new Section XML file named "import-root"
                        //
                        var createTimeStamp = createIsoTimestamp();

                        // Retrieve the template for section content
                        var filingDataTemplate = RetrieveTemplate("inline-editor-content");
                        if (filingDataTemplate == null)
                        {
                            throw new Exception("Could not load inline-editor-content template");
                        }

                        // Fill the template with data
                        var nodeDateCreated = filingDataTemplate.SelectSingleNode("/data/system/date_created");
                        var nodeDateModified = filingDataTemplate.SelectSingleNode("/data/system/date_modified");
                        var nodeContentOriginal = filingDataTemplate.SelectSingleNode("/data/content");

                        if (nodeDateCreated == null || nodeDateModified == null || nodeContentOriginal == null)
                        {
                            throw new Exception("Could not find required elements in data template");
                        }

                        // Create one content node per language
                        var languages = RetrieveProjectLanguages(projectVars.editorId);
                        foreach (var lang in languages)
                        {
                            var nodeContent = nodeContentOriginal.CloneNode(true);
                            var nodeContentArticle = nodeContent.FirstChild;
                            var nodeContentHeader = nodeContentArticle.SelectSingleNode("div/section/h1");

                            if (nodeContentArticle == null || nodeContentHeader == null)
                            {
                                throw new Exception("Could not find article or header elements in content template");
                            }

                            nodeDateCreated.InnerText = createTimeStamp;
                            nodeDateModified.InnerText = createTimeStamp;
                            SetAttribute(nodeContent, "lang", lang);
                            SetAttribute(nodeContentArticle, "id", sectionId);
                            SetAttribute(nodeContentArticle, "data-guid", sectionId);
                            SetAttribute(nodeContentArticle, "data-last-modified", createTimeStamp);
                            nodeContentHeader.InnerText = sectionTitle;

                            filingDataTemplate.DocumentElement.AppendChild(nodeContent);
                        }

                        // Remove the original template node
                        RemoveXmlNode(nodeContentOriginal);

                        resultsToShow.Add($"- Created section template for: {sectionId}");

                        // Save the new section to the document store
                        var xmlCreateResult = await DocumentStoreService.FilingData.CreateSourceData(
                            sectionId,
                            filingDataTemplate,
                            projectVars.projectId,
                            projectVars.versionId,
                            "text",
                            projectVars.outputChannelDefaultLanguage,
                            debugRoutine
                        );

                        if (XmlContainsError(xmlCreateResult))
                        {
                            appLogger.LogError($"Failed to create import-root section: {xmlCreateResult.OuterXml}");
                            throw new Exception("Failed to create import-root section in document store");
                        }

                        resultsToShow.Add($"- Successfully created section in document store: {sectionId}");

                        //
                        // Step 2: Create the new <item> node in the hierarchy
                        //
                        // Note: nodeOriginalSubItems was already declared in Step 0

                        var nodeToc = nodeOriginalSubItems.SelectSingleNode("item[@id='toc']");
                        if (nodeToc == null)
                        {
                            throw new Exception("Could not find toc marker in original hierarchy");
                        }

                        var newItem = xmlOriginalHierarchy.CreateElement("item");
                        SetAttribute(newItem, "id", sectionId);
                        SetAttribute(newItem, "level", "0");
                        SetAttribute(newItem, "data-ref", $"{sectionId}.xml");

                        var webPage = xmlOriginalHierarchy.CreateElement("web_page");
                        var path = xmlOriginalHierarchy.CreateElement("path");
                        path.InnerText = "/";
                        var linkname = xmlOriginalHierarchy.CreateElement("linkname");
                        linkname.InnerText = sectionTitle;

                        webPage.AppendChild(path);
                        webPage.AppendChild(linkname);
                        newItem.AppendChild(webPage);

                        var subItems = xmlOriginalHierarchy.CreateElement("sub_items");
                        newItem.AppendChild(subItems);

                        // Insert the new item after the toc node
                        nodeOriginalSubItems.InsertAfter(newItem, nodeToc);

                        resultsToShow.Add($"- Added new hierarchy item: {sectionId}");

                        //
                        // Step 3: Add all items from imported hierarchy into the new item's sub_items
                        //
                        var nodeImportedSubItems = xmlImportedHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                        if (nodeImportedSubItems == null)
                        {
                            throw new Exception("Could not find /items/structured/item/sub_items in imported hierarchy");
                        }

                        // Collect all item nodes from imported hierarchy
                        var importedItems = nodeImportedSubItems.SelectNodes("item");
                        if (importedItems == null || importedItems.Count == 0)
                        {
                            resultsToShow.Add($"- WARNING: No items found in imported hierarchy");
                        }
                        else
                        {
                            resultsToShow.Add($"- Importing {importedItems.Count} items from imported hierarchy");

                            foreach (XmlNode importedItem in importedItems)
                            {
                                var importedId = importedItem.Attributes?["id"]?.Value ?? "unknown";
                                resultsToShow.Add($"  - Importing item: {importedId}");

                                // Import the node into the original document
                                var importedNode = xmlOriginalHierarchy.ImportNode(importedItem, true);

                                // Add to the sub_items of the new import-root item
                                subItems.AppendChild(importedNode);
                            }

                            resultsToShow.Add($"- Successfully merged hierarchies in APPEND mode");
                        }
                    }
                    else
                    {
                        throw new Exception($"Unknown import mode: {importMode}");
                    }



                    //
                    // => Save the new hierarchy file
                    //
                    var projectVarsForSave = projectVars;
                    projectVarsForSave.outputChannelType = "pdf";
                    projectVarsForSave.outputChannelVariantId = outputChannelVariantId;
                    projectVarsForSave.outputChannelVariantLanguage = outputChannelLanguage;
                    XmlDocument xmlSaveResult = await FilingData.SaveHierarchy(projectVarsForSave, xmlOriginalHierarchy, true, true);
                    if (XmlContainsError(xmlSaveResult))
                    {
                        appLogger.LogError($"Unable to save updated output channel hierarchy to the document store for projectId: {projectVars.projectId}, versionId: {projectVars.versionId}, editorId: {projectVars.editorId}, outputChannelType: pdf, outputChannelVariantId: {outputChannelVariantId}, outputChannelLanguage: {outputChannelLanguage}. Error: {xmlSaveResult.OuterXml}");
                        throw new Exception("Unable to save updated output channel hierarchy to the document store");
                    }


                    //
                    // => Store the section XML files
                    //
                    resultsToShow.Add($"- Storing {sectionXmlFiles.Count} section XML files");
                    var failedSectionSaves = 0;

                    foreach (var sectionXmlFilePathOs in sectionXmlFiles)
                    {
                        try
                        {
                            // Extract the data reference (filename without extension)
                            var dataReferenceFileName = Path.GetFileName(sectionXmlFilePathOs);
                            // var dataReference = Path.GetFileNameWithoutExtension(fileName);

                            // Load the XML file from disk
                            var xmlFilingContentSourceData = new XmlDocument();
                            xmlFilingContentSourceData.Load(sectionXmlFilePathOs);

                            // Save to document store
                            if (debugRoutine) appLogger.LogInformation($"Storing section data with reference: '{dataReferenceFileName}'");
                            var saveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFilingContentSourceData, $"/textual/{dataReferenceFileName}", "cmsdataroot", debugRoutine, false);

                            if (XmlContainsError(saveResult))
                            {
                                appLogger.LogError($"{dataReferenceFileName} failed to save with error: {saveResult.OuterXml}");
                                failedSectionSaves++;
                                continue;
                            }

                            resultsToShow.Add($"  - Saved: {dataReferenceFileName}");
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError($"Error saving section XML file {sectionXmlFilePathOs}: {ex.Message}");
                            failedSectionSaves++;
                        }
                    }

                    if (failedSectionSaves > 0)
                    {
                        throw new Exception($"Failed to save {failedSectionSaves} out of {sectionXmlFiles.Count} section XML files");
                    }

                    resultsToShow.Add($"- Successfully stored {sectionXmlFiles.Count} section XML files");



                    //
                    // => Store the image files
                    //

                    // Update projectVars with output channel details needed for file storage
                    projectVars.outputChannelType = "pdf";
                    projectVars.outputChannelVariantId = outputChannelVariantId;
                    projectVars.outputChannelVariantLanguage = outputChannelLanguage;
                    projectVars.did = "";

                    resultsToShow.Add($"- Storing {imageFiles.Count} image files");
                    var failedImageUploads = 0;

                    foreach (var imageFilePathOs in imageFiles)
                    {
                        try
                        {
                            // Upload the image using DocumentStore with gRPC
                            var uploadSuccess = await UploadImageToDocumentStore(imageFilePathOs, projectVars);

                            if (!uploadSuccess)
                            {
                                var imageFileName = Path.GetFileName(imageFilePathOs);
                                appLogger.LogError($"Failed to upload image: {imageFileName}");
                                failedImageUploads++;
                                continue;
                            }

                            resultsToShow.Add($"  - Uploaded: {Path.GetFileName(imageFilePathOs)}");
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError($"Error uploading image file {imageFilePathOs}: {ex.Message}");
                            failedImageUploads++;
                        }
                    }

                    if (failedImageUploads > 0)
                    {
                        throw new Exception($"Failed to upload {failedImageUploads} out of {imageFiles.Count} image files");
                    }

                    resultsToShow.Add($"- Successfully stored {imageFiles.Count} image files");



                    //
                    // => Create the post-import version
                    //
                    var postSyncVerionResult = await GenerateVersion(projectId, $"Post import data version", false, projectVars);
                    if (!preSyncVerionResult.Success)
                    {
                        appLogger.LogError($"Unable to create pre-import version. Error: {postSyncVerionResult.Message}, DebugInfo: {postSyncVerionResult.DebugInfo}");
                        throw new Exception($"Unable to create pre-import version. Error: {postSyncVerionResult.Message}");
                    }

                    //
                    // => Clear the caches
                    //

                    // Clear framework memory cache
                    RemoveMemoryCacheAll();

                    // Clear RBAC cache
                    projectVars.rbacCache.ClearAll();

                    // Optionally clear Document Store cache
                    await DocumentStoreService.FilingData.ClearCache(false);




                    //
                    // => Clean up Context.Items
                    //
                    Context.Items.Remove("ImportSessionId");
                    Context.Items.Remove("ImportFileName");
                    Context.Items.Remove("ImportProjectId");
                    Context.Items.Remove("ImportTempFilePath");
                    Context.Items.Remove("ImportBytesReceived");
                    Context.Items.Remove("ImportFileStream");
                    Context.Items.Remove("ImportMode");

                    appLogger.LogInformation($"FinalizeProjectDataImport: Successfully finalized import - session {sessionId}, file {fileName}, {totalBytes} bytes, mode {importMode}");

                    var responseData = new
                    {
                        fileName = "data.zip",
                        filePath = normalizedFilePath,
                        projectId = projectId,
                        importMode = importMode,
                        totalBytes = totalBytes,
                        results = resultsToShow
                    };

                    return new TaxxorReturnMessage(
                        true,
                        "Import file received, extracted and processed successfully",
                        (projectVars.currentUser.Permissions.ViewDeveloperTools) ? JsonSerializer.Serialize(responseData) : ""
                    );
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"FinalizeProjectDataImport: Exception - {errorMessage}");
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }

            /// <summary>
            /// Uploads an image file to the DocumentStore using gRPC
            /// </summary>
            /// <param name="imageFilePathOs">Full path to the image file on disk</param>
            /// <param name="projectVars">Project variables containing project context</param>
            /// <returns>True if upload succeeded, false otherwise</returns>
            private async Task<bool> UploadImageToDocumentStore(string imageFilePathOs, ProjectVariables projectVars)
            {
                try
                {
                    // Read the image file data
                    byte[] imageData = await File.ReadAllBytesAsync(imageFilePathOs);

                    // Extract just the filename
                    string fileName = Path.GetFileName(imageFilePathOs);

                    // Create the gRPC request with all required fields populated
                    var request = new DocumentStore.Protos.StoreFileRequest
                    {
                        GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
                        AssetType = "projectimages",
                        FolderPath = "/from-conversion",
                        FileName = fileName,
                        FileData = Google.Protobuf.ByteString.CopyFrom(imageData),
                        NormalizeFilename = true
                    };

                    // Call the gRPC service
                    var response = await _binaryFileClient.StoreFileAsync(request);

                    // Log the result
                    if (response.Success)
                    {
                        appLogger.LogInformation($"Successfully uploaded image: {fileName}");
                    }
                    else
                    {
                        appLogger.LogError($"Failed to upload image: {fileName}. Error: {response.Message}");
                    }

                    return response.Success;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Exception while uploading image: {imageFilePathOs}");
                    return false;
                }
            }

            /// <summary>
            /// Aborts an in-progress import and cleans up resources
            /// </summary>
            /// <param name="sessionId">The session ID from InitiateProjectDataImport</param>
            /// <returns>TaxxorReturnMessage confirming cancellation</returns>
            [AuthorizeAttribute(VirtualPath = "/hub/methods/admintools")]
            public async Task<TaxxorReturnMessage> AbortProjectDataImport(string sessionId)
            {
                var errorMessage = "There was an error aborting the data import";
                var context = System.Web.Context.Current;
                RequestVariables reqVars = RetrieveRequestVariables(context);
                ProjectVariables projectVars = RetrieveProjectVariables(context);
                var debugRoutine = (siteType == "local" || siteType == "dev");

                try
                {
                    //
                    // => Handle security
                    //
                    var securityCheckResult = await HandleMethodSecurity(GetType().GetMethod(MethodBase.GetCurrentMethod().GetDeclaringName()).GetCustomAttributes(true).OfType<AuthorizeAttribute>().FirstOrDefault(), reqVars, projectVars);
                    if (!securityCheckResult.Success)
                    {
                        appLogger.LogError($"AbortProjectDataImport: Security check failed - {securityCheckResult.ToString()}");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) securityCheckResult.DebugInfo = "";
                        return securityCheckResult;
                    }

                    //
                    // => Input data validation
                    //
                    var inputValidationCollection = new InputValidationCollection();
                    inputValidationCollection.Add("sessionId", sessionId, @"^[a-zA-Z0-9\-]{36}$", true);

                    var validationResult = inputValidationCollection.Validate();
                    if (!validationResult.Success)
                    {
                        appLogger.LogError($"AbortProjectDataImport: Input validation failed - {validationResult.ToString()}");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) validationResult.DebugInfo = "";
                        return validationResult;
                    }

                    //
                    // => Verify session exists
                    //
                    if (!Context.Items.ContainsKey("ImportSessionId") ||
                        Context.Items["ImportSessionId"]?.ToString() != sessionId)
                    {
                        appLogger.LogError($"AbortProjectDataImport: Invalid or expired session - {sessionId}");
                        var sessionError = new TaxxorReturnMessage(false, "Invalid or expired upload session", "");
                        if (!projectVars.currentUser.Permissions.ViewDeveloperTools) sessionError.DebugInfo = "";
                        return sessionError;
                    }

                    var fileStream = Context.Items["ImportFileStream"] as FileStream;
                    var tempFilePath = Context.Items["ImportTempFilePath"]?.ToString();

                    //
                    // => Close and dispose file stream
                    //
                    if (fileStream != null)
                    {
                        try
                        {
                            fileStream.Close();
                            fileStream.Dispose();
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogWarning($"AbortProjectDataImport: Error closing file stream - {ex.Message}");
                        }
                    }

                    //
                    // => Delete temp file
                    //
                    if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                    {
                        try
                        {
                            File.Delete(tempFilePath);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogWarning($"AbortProjectDataImport: Error deleting temp file - {ex.Message}");
                        }
                    }

                    //
                    // => Clean up Context.Items
                    //
                    Context.Items.Remove("ImportSessionId");
                    Context.Items.Remove("ImportFileName");
                    Context.Items.Remove("ImportProjectId");
                    Context.Items.Remove("ImportTempFilePath");
                    Context.Items.Remove("ImportBytesReceived");
                    Context.Items.Remove("ImportFileStream");
                    Context.Items.Remove("ImportMode");

                    appLogger.LogInformation($"AbortProjectDataImport: Import aborted - session {sessionId}");

                    return new TaxxorReturnMessage(
                        true,
                        "Upload cancelled",
                        ""
                    );
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"AbortProjectDataImport: Exception - {errorMessage}");
                    return new TaxxorReturnMessage(false, errorMessage, ((projectVars.currentUser.Permissions.ViewDeveloperTools) ? $"error: {ex}" : ""));
                }
            }

        }

    }

}