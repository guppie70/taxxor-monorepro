using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routines to help working with text and log files
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Asynchronously creates a text file on the disk and writes content in it
    /// </summary>
    /// <param name="content">Text content to store</param>
    /// <param name="pathLocation">Full path or ID to path location from the configuration</param>
    /// <returns></returns>
    public static async Task TextFileCreateAsync(string content, string pathLocation)
    {
        await TextFileAppendAsync(content, pathLocation, false);
    }

    /// <summary>
    /// Asynchronously appends text to an existing text file
    /// </summary>
    /// <param name="content">Text content to store</param>
    /// <param name="pathLocation">Full path or ID to path location from the configuration</param>
    /// <param name="append"></param>
    /// <returns></returns>
    public static async Task TextFileAppendAsync(string content, string pathLocation, bool append = true)
    {
        if (!pathLocation.Contains("/")) pathLocation = CalculateFullPathOs(pathLocation);

        using (FileStream sourceStream = new FileStream(pathLocation, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            using (StreamWriter sw = new StreamWriter(sourceStream))
            {
                await sw.WriteLineAsync(content);
                sw.Close();
            }
            sourceStream.Close();
        }
    }


    /// <summary>
    /// Create a text file on the server disk and records the error in case it fails
    /// </summary>
    /// <param name="content">Textual content for the file</param>
    /// <param name="pathLocation">Full OS path or ID to path location from the configuration where the file needs to be created</param>
    public static void TextFileCreate(string content, string pathLocation)
    {
        TextFileCreate(content, pathLocation, true);
    }

    /// <summary>
    /// Create a text file on the server disk
    /// </summary>
    /// <param name="content">Textual content for the file</param>
    /// <param name="pathLocation">Full OS path or ID to path location from the configuration where the file needs to be created</param>
    /// <param name="logIssues">Indicates if in case of an error logging needs to take place</param>
    /// <param name="doErrorResponseWrite">Write something to the output string in case something goes wrong</param>
    /// <param name="stopProcessingIfError">Stop further processing if something goes wrong</param>
    /// <param name="checkExclusiveFileAccess">Flag if we need to test first if we have exclusive file access (may impact performance)</param>
    public static void TextFileCreate(string content, string pathLocation, bool logIssues, bool doErrorResponseWrite = false, bool stopProcessingIfError = false, bool checkExclusiveFileAccess = true)
    {
        if (!pathLocation.Contains("/")) pathLocation = CalculateFullPathOs(pathLocation);

        try
        {
            if (checkExclusiveFileAccess)
            {
                if (HasExclusiveFileAccess(pathLocation))
                {
                    using (var file = new StreamWriter(pathLocation))
                    {
                        file.Write(content);
                        file.Close();
                    }
                }
                else
                {
                    if (logIssues)
                    {
                        if (doErrorResponseWrite || stopProcessingIfError)
                        {
                            HandleError(RetrieveRequestVariables(System.Web.Context.Current), $"Could not create {pathLocation}", $"Could not obtain exclusive permissions to write file {pathLocation}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            WriteErrorMessageToConsole($"Could not create {pathLocation}", $"Could not obtain exclusive permissions to write file {pathLocation}, stack-trace: {GetStackTrace()}");
                        }
                    }
                }
            }
            else
            {
                using (var file = new StreamWriter(pathLocation))
                {
                    file.Write(content);
                    file.Close();
                }
            }
        }
        catch (Exception e)
        {
            if (logIssues)
            {
                if (doErrorResponseWrite || stopProcessingIfError)
                {
                    HandleError(RetrieveRequestVariables(System.Web.Context.Current), $"Could not create {pathLocation}", $"error: {e.ToString()}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    WriteErrorMessageToConsole($"Could not create {pathLocation}", $"error: {e.ToString()}, stack-trace: {GetStackTrace()}");
                }
            }

            // Now create a memory cache entry to prevent the data from being lost and attempt to write it on the disk at a later stage
            if (pathLocation != null) CreateLogDataMemCacheEntry(content, pathLocation, false);
        }
    }

    /// <summary>
    /// Appends a line of text to a text file stored on the disk
    /// </summary>
    /// <param name="content">Content to add</param>
    /// <param name="pathLocation">Full OS path or ID to path location from the configuration where the file needs to be created</param>
    /// <param name="errorReturnType">Format in which to report an error if things go wrong</param>
    public static void TextFileAppend(string content, string pathLocation, ReturnTypeEnum errorReturnType = ReturnTypeEnum.Txt)
    {
        TextFileAppend(content, pathLocation, true, errorReturnType);
    }

    /// <summary>
    /// Appends a line of text to a text file stored on the disk - and optionally logs any problems that it encounters
    /// </summary>
    /// <param name="content">Content to add</param>
    /// <param name="pathLocation">Full OS path or ID to path location from the configuration where the file needs to be created</param>
    /// <param name="logIssues">Indicates if in case of an error logging needs to take place</param>
    /// <param name="errorReturnType">Format in which to report an error if things go wrong</param>
    ///	<param name="doErrorResponseWrite">Write something to the output string in case something goes wrong</param>
    /// <param name="stopProcessingIfError">Stop further processing if something goes wrong</param>
    /// <param name="checkExclusiveFileAccess">Flag if we need to test first if we have exclusive file access (may impact performance)</param>
    public static void TextFileAppend(String content, String? pathLocation, bool logIssues, ReturnTypeEnum errorReturnType = ReturnTypeEnum.Txt, bool doErrorResponseWrite = false, bool stopProcessingIfError = false, bool checkExclusiveFileAccess = true)
    {
        var context = System.Web.Context.Current;
        var reqVars = RetrieveRequestVariables(context);

        if (!pathLocation.Contains('/')) pathLocation = CalculateFullPathOs(pathLocation);

        try
        {
            if (checkExclusiveFileAccess)
            {
                if (HasExclusiveFileAccess(pathLocation))
                {
                    using (var file = new StreamWriter(pathLocation, true))
                    {
                        file.WriteLine(content);
                        file.Close();
                    }
                }
                else
                {
                    if (logIssues)
                    {
                        if (doErrorResponseWrite || stopProcessingIfError)
                        {
                            HandleError(reqVars, $"Could not append to {pathLocation}", $"Could not obtain exclusive permissions to write file {pathLocation}, stack-trace: {GetStackTrace()}");
                        }
                        else
                        {
                            WriteErrorMessageToConsole($"Could not append to {pathLocation}", $"Could not obtain exclusive permissions to write file {pathLocation}, stack-trace: {GetStackTrace()}");
                        }
                    }
                }
            }
            else
            {
                using (var file = new StreamWriter(pathLocation, true))
                {
                    file.WriteLine(content);
                    file.Close();
                }
            }
        }
        catch (Exception e)
        {
            if (logIssues)
            {
                if (doErrorResponseWrite || stopProcessingIfError)
                {
                    HandleError(reqVars, $"Could not append to {pathLocation}", $"error: {e.ToString()}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    WriteErrorMessageToConsole($"Could not append to {pathLocation}", $"error: {e.ToString()}, stack-trace: {GetStackTrace()}");
                }
            }

            // Now create a memory cache entry to prevent the data from being lost and attempt to write it on the disk at a later stage
            if (pathLocation != null) CreateLogDataMemCacheEntry(content, pathLocation, true);
        }
    }

    /// <summary>
    /// Utility that will be called when an error occured trying to write data in a file to the disk - it will fill the mem cache of the server and will then be picked up by the "DumpLogData()" routine
    /// </summary>
    /// <param name="content"></param>
    /// <param name="filePathOs"></param>
    private static void CreateLogDataMemCacheEntry(string content, string filePathOs, bool appendMode)
    {
        var cacheKey = "logdata";
        //test if we have defined caching
        if (xmlApplicationConfiguration.SelectNodes("/configuration/cache_system/cache_item[@id='" + cacheKey + "']").Count > 0)
        {

            string keyContent = "logdata_" + md5(filePathOs);
            string keyPath = keyContent + "_path";
            string keyCreated = keyContent + "_datecreated";

            // TODO: Fix the below properly
            // memoryCacheUtilities.Set(keyContent, content);
            // //make the date one day old so that on the next call for the web page it will be handled as data that needs to be written to the disk
            // memoryCacheUtilities.Set(keyCreated, DateTime.Today.AddDays(-1));

            // if (!memoryCacheUtilities.Exists(keyPath)) memoryCacheUtilities.Set(keyPath, filePathOs);

            // if (!appendMode)
            // {
            // 	string keyAppendMode = keyContent + "_mode";
            // 	memoryCacheUtilities.Set(keyAppendMode, "create");
            // }
        }

    }

    /// <summary>
    /// Adds a timestamp to the content passed and then adds it to a log file (possibly cached to improve scalibility)
    /// </summary>
    /// <param name="content">Log data to store</param>
    /// <param name="logFilePathOs">Full path to the log file</param>
    public static void AppendLogDataInFile(string content, string logFilePathOs)
    {
        String logLineToAppend = GenerateLogTimestamp() + " - " + content;

        var cacheKey = "logdata";
        //test if we have defined caching
        if (xmlApplicationConfiguration.SelectNodes("/configuration/cache_system/cache_item[@id='" + cacheKey + "']").Count > 0)
        {
            // TODO: Fix this
            // string keyContent = "logdata_" + md5(logFilePathOs);
            // string keyPath = keyContent + "_path";
            // string keyCreated = keyContent + "_datecreated";
            // string logData = "";
            // bool cacheValid = IsCacheValid(cacheKey, keyContent, keyCreated);

            // if (!memoryCacheUtilities.Exists(keyPath)) memoryCacheUtilities.Set(keyPath, logFilePathOs);

            // //grab the value from the cache
            // logData = RetrieveCacheContent<String>(keyContent);

            // //add the log line
            // if (!string.IsNullOrEmpty(logData))
            // {
            // 	logData += "\r\n" + logLineToAppend;
            // }
            // else
            // {
            // 	logData += logLineToAppend;
            // }

            // if (cacheValid)
            // {
            // 	//store it back in the cache
            // 	memoryCacheUtilities.Set(keyContent, logData);
            // }
            // else
            // {
            // 	//add it to the log file
            // 	try
            // 	{
            // 		using (var file = new StreamWriter(logFilePathOs, true))
            // 		{
            // 			file.WriteLine(logData);
            // 			file.Close();
            // 			//empty the mem variable
            // 			//store it in the cache
            // 			SetCacheContent(keyContent, keyCreated, "");
            // 		}
            // 	}
            // 	catch (Exception e)
            // 	{
            // 		//store it in the cache (as a nice side effect this will create a new timestamp in the data cache which will prevent this routine from running again in the next request)
            // 		SetCacheContent(keyContent, keyCreated, logData + "\r\n" + GenerateLogTimestamp() + " - Unable to write to the log file '" + logFilePathOs + "' file is probably in use (exception was: " + e.ToString() + ")");
            // 	}

            // }
        }
        else
        {
            TextFileAppend(logLineToAppend, logFilePathOs);
        }

    }

    /// <summary>
    /// This routine loops though all the logfile data sored in the memory cache and attempts to dump it on the disk
    /// </summary>
    public static void DumpLogData(HttpContext httpContext)
    {
        //string keyContent = string.Empty;
        //string keyPath = string.Empty;
        //string logData = string.Empty;
        //var cacheKey = "logdata";
        //var debugRoutine = true;
        //var dumpLogDataDebugMessage = "";

        //// TODO: fix this
        //foreach (DictionaryEntry item in httpContext.Response. .Current.Cache)
        //{
        //	keyContent = item.Key.ToString();
        //	if (isDebugMode && debugRoutine)
        //	{
        //		//dumpLogDataDebugMessage += "Examining keyContent: " + keyContent;
        //	}
        //	if (keyContent.Contains("logdata_"))
        //	{
        //		keyPath = keyContent + "_path";
        //		if (isDebugMode && debugRoutine)
        //		{
        //			//dumpLogDataDebugMessage += ", keyPath: " + keyPath;
        //		}
        //		if (memoryCacheUtilities.Exists(keyPath))
        //		{
        //			logData = RetrieveCacheContent<String>(keyContent);
        //			if (!string.IsNullOrEmpty(logData))
        //			{
        //				//retrieve the path to the log file from the memory cache
        //				string logFilePathOs = RetrieveCacheContent<String>(keyPath);

        //				if (isDebugMode && debugRoutine)
        //				{
        //					dumpLogDataDebugMessage += " - logFilePathOs: " + logFilePathOs;
        //				}

        //				//test if the mem cache is still valid
        //				string keyCreated = keyContent + "_datecreated";
        //				bool cacheValid = IsCacheValid(cacheKey, keyContent, keyCreated);
        //				if (!cacheValid || !string.IsNullOrEmpty(querystringVariables.Get("dumplog")))
        //				{
        //					bool appendMode = true;
        //					if (memoryCacheUtilities.Exists(keyContent + "_mode"))
        //					{
        //						string streamWriterMode = RetrieveCacheContent<String>(keyContent + "_mode");
        //						if (streamWriterMode == "create") appendMode = false;
        //					}

        //					//attempt to add it to the log file
        //					try
        //					{
        //						using (var file = new StreamWriter(logFilePathOs, appendMode))
        //						{
        //							file.WriteLine(logData);
        //							file.Close();
        //							if (isDebugMode && debugRoutine)
        //							{
        //								dumpLogDataDebugMessage += " written to disk";
        //							}

        //							//empty the mem variable
        //							//store it in the cache
        //							SetCacheContent(keyContent, keyCreated, "");
        //						}
        //					}
        //					catch (Exception e)
        //					{
        //						// calculate the path and the memory key for the system error log file
        //						string systemLogFilePathOs = GenerateOsPathToSystemErrorLog();
        //						keyContent = "logdata_" + md5(systemLogFilePathOs);

        //						//log the exception in the system error log (and append it to the existing content of the memory cache)
        //						string logMessage = GenerateLogTimestamp() + " - Unable to write to the log file '" + logFilePathOs + "' file is probably in use (exception was: " + e.ToString() + ")";
        //						if (logFilePathOs == systemLogFilePathOs) logMessage = logData + "\r\n" + logMessage;

        //						//store it in the cache (as a nice side effect this will create a new timestamp in the data cache which will prevent this routine from running again in the next request)
        //						SetCacheContent(keyContent, keyCreated, logMessage);

        //						if (isDebugMode && debugRoutine)
        //						{
        //							dumpLogDataDebugMessage += " was locked and could not be written to the disk";
        //						}
        //					}
        //				}

        //			}
        //		}
        //	}

        //}

        //if (isDebugMode && debugRoutine) {
        //    AddDebugVariable(()=> dumpLogDataDebugMessage);
        //}

    }

    /// <summary>
    /// Utility that generates a timestamp to be used in logging routines
    /// </summary>
    /// <returns></returns>
    public static string GenerateLogTimestamp(bool highPrecision = true)
    {
        string datePattern = highPrecision ? @"yyyy-MM-dd HH:mm:ss.fff" : @"yyyy-MM-dd HH:mm:ss";
        DateTime dateNow = DateTime.Now;
        return dateNow.ToString(datePattern);
    }

    /// <summary>
    /// Retrieves the complete content of a text file from the disk or from an http resource - assumes "utf-8" encoding
    /// </summary>
    /// <param name="uri">Complete filepath or URL pointing to the text file we want te retrieve</param>
    /// <returns></returns>
    public async static Task<string> RetrieveTextFile(string uri)
    {
        return await RetrieveTextFile(uri, "utf-8", false, null);
    }

    /// <summary>
    /// Retrieves the complete content of a text file from the disk or from an http resource - assumes "utf-8" encoding
    /// </summary>
    /// <param name="uri">Complete filepath or URL pointing to the text file we want te retrieve</param>
    /// <param name="renderError">Indicates if we should return the error or log it</param>
    /// <returns></returns>
    public async static Task<string> RetrieveTextFile(string uri, bool renderError)
    {
        return await RetrieveTextFile(uri, "utf-8", renderError, null);
    }

    /// <summary>
    /// Retrieves the complete content of a text file from the disk or from an http resource
    /// </summary>
    /// <param name="uri">Complete filepath or URL pointing to the te=xt file we want te retrieve</param>
    /// <param name="encoding">Indicates the encoding to use when reading the text file from an http resource</param>
    /// <param name="returnType">Determines the format of the error data to return if things go wrong</param>
    /// <returns></returns>
    public async static Task<string> RetrieveTextFile(string uri, string encoding, ReturnTypeEnum? returnType)
    {
        return await RetrieveTextFile(uri, encoding, false, returnType);
    }

    /// <summary>
    /// Retrieves the complete content of a text file from the disk or from an http resource
    /// </summary>
    /// <param name="uri">Complete filepath or URL pointing to the te=xt file we want te retrieve</param>
    /// <param name="encoding">Indicates the encoding to use when reading the text file from an http resource</param>
    /// <param name="renderError">Indicates if we should return the error or log it</param>
    /// <param name="returnType">Determines the format of the error data to return if things go wrong</param>
    /// <returns></returns>
    public async static Task<string> RetrieveTextFile(string uri, string encoding, bool renderError, ReturnTypeEnum? returnType = null)
    {
        string? contents = null;

        try
        {
            if (uri.ToLower().StartsWith("http", StringComparison.CurrentCulture))
            {
                // A remote resource
                using (var httpClient = new HttpClient())
                {
                    Task<string> contentsTask = httpClient.GetStringAsync(uri);

                    contents = await contentsTask;
                }
            }
            else
            {
                // Regular file on the disk
                using (var reader = File.OpenText(uri))
                {
                    contents = await reader.ReadToEndAsync();
                }
            }
        }
        catch (Exception e)
        {
            if (renderError)
            {
                throw;
            }
            else
            {
                if (returnType == null)
                {
                    WriteErrorMessageToConsole($"Unable to retrieve {uri}", e.ToString());
                }
                else
                {
                    var context = System.Web.Context.Current;
                    var reqVars = RetrieveRequestVariables(context);
                    HandleError(reqVars, $"Unable to retrieve {uri}", e.ToString());
                }
            }
        }

        return contents;
    }

    /// <summary>
    /// Retrieves the size of a file in bytes
    /// </summary>
    /// <param name="filePath">Complete OS path to the file</param>
    /// <param name="pathType">"approot" or "webroot"</param>
    /// <returns>An integer containing the size of the file</returns>
    public static int RetrieveFileSizeAsInt(string filePath, string pathType = null)
    {
        string filePathOs = filePath;
        if (pathType != null)
        {
            switch (pathType)
            {
                case "approot":
                    filePathOs = applicationRootPathOs + filePath;
                    break;
                default:
                    filePathOs = websiteRootPathOs + filePath;
                    break;
            }
        }

        // If FileExists move it and finally write the response message
        if (File.Exists(filePathOs))
        {
            var fileInfo = new FileInfo(filePathOs);
            return Convert.ToInt32(fileInfo.Length);
        }
        else
        {
            return 0;
        }
    }

    /// <summary>
    /// Retrieves the size of a file in a nicely formatted string
    /// </summary>
    /// <param name="filePath">Complete OS path to the file</param>
    /// <returns></returns>
    public static string RetrieveFileSizeAsString(string filePath, string pathType = "webroot")
    {
        String filePathOs = filePath;
        if (!filePath.Contains(":"))
        {
            switch (pathType)
            {
                case "approot":
                    filePathOs = applicationRootPathOs + filePath;
                    break;
                default:
                    filePathOs = websiteRootPathOs + filePath;
                    break;
            }
        }

        // If FileExists move it and finally write the response message
        if (File.Exists(filePathOs))
        {
            var fileInfo = new FileInfo(filePathOs);
            return CalculateFileSize(fileInfo.Length);
        }
        else
        {
            return String.Empty;
        }
    }

    /// <summary>
    /// Retrieves the size of a file in a nicely formatted string and writes it to the output stream
    /// </summary>
    /// <param name="filePath">Complete OS path to the file</param>
    public static string RetrieveFileSize(string filePath, string pathType = "webroot")
    {
        String filePathOs = filePath;
        if (!filePath.Contains(":"))
        {
            switch (pathType)
            {
                case "approot":
                    filePathOs = applicationRootPathOs + filePath;
                    break;
                default:
                    filePathOs = websiteRootPathOs + filePath;
                    break;
            }
        }

        // If FileExists move it and finally write the response message
        if (File.Exists(filePathOs))
        {
            var fileInfo = new FileInfo(filePathOs);
            return CalculateFileSize(fileInfo.Length);
        }
        else
        {
            WriteErrorMessageToConsole("Unable to locate file", $"filePathOs: {filePathOs}");
            return "";
        }
    }

    /// <summary>
    /// Calculates a decent and readable filesize formatted as x Gb, x Mb, x Kb, x Bytes
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string CalculateFileSize(long bytes)
    {
        if (bytes >= Math.Pow(1024, 3))
        {
            return String.Format("{0} GB", Math.Round(bytes / Math.Pow(1024, 3), 1));
        }
        else if (bytes >= Math.Pow(1024, 2))
        {
            return String.Format("{0} MB", Math.Round(bytes / Math.Pow(1024, 2), 1));
        }
        else if (bytes >= 1024)
        {
            return String.Format("{0} kB", Math.Round(bytes / Math.Pow(1024, 1), 0));
        }
        else
        {
            String.Format("{0} Bytes", bytes);
        }

        return "";
    }


    /// <summary>
    /// Streams a file to the output stream
    /// </summary>
    /// <param name="filePathOs">Path to the file (binary or text based)</param>
    /// <param name="forceDownload">Should the download be forced as an attachment</param>
    /// <param name="errorReturnType">What type of error to return</param>
    public static async Task StreamFile(HttpResponse Response, string filePathOs, bool forceDownload, ReturnTypeEnum errorReturnType)
    {
        string fileName = Path.GetFileName(filePathOs);
        await StreamFile(Response, fileName, filePathOs, forceDownload, errorReturnType);
    }

    /// <summary>
    /// Streams a file to the output stream
    /// </summary>
    /// <param name="displayFileName">The default file name that the web client will use when the file is forced as a download</param>
    /// <param name="filePathOs">Path to the file (binary or text based)</param>
    /// <param name="forceDownload">Should the download be forced as an attachment</param>
    /// <param name="errorReturnType">What type of error to return</param>
    public static async Task StreamFile(HttpResponse Response, string displayFileName, string filePathOs, bool forceDownload, ReturnTypeEnum errorReturnType)
    {
        if (File.Exists(filePathOs))
        {
            string fileName = Path.GetFileName(filePathOs);
            bool binary = isBinary(Path.GetExtension(fileName));
            string contentType = GetContentType(Path.GetExtension(filePathOs).Replace(".", ""));
            FileInfo fInfo = new FileInfo(filePathOs);

            SetHeaders(Response, displayFileName, contentType, Convert.ToInt32(fInfo.Length), forceDownload);

            if (binary)
            {
                // Create an optimized file stream for reading binary data
                using var fileStream = new FileStream(
                    filePathOs,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 64 * 1024, // 64KB buffer size
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan); // Optimize for sequential access

                // Rent a buffer from the shared pool instead of allocating a new one
                const int bufferSize = 64 * 1024; // 64KB chunks
                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await Response.Body.WriteAsync(buffer, 0, bytesRead);
                    }
                }
                finally
                {
                    // Return the buffer to the pool
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else
            {
                // For text files, stream them directly instead of loading entire file into memory
                using (var fileStream = new FileStream(
                    filePathOs,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    options: FileOptions.SequentialScan | FileOptions.Asynchronous))
                using (var textReader = new StreamReader(
                    fileStream,
                    detectEncodingFromByteOrderMarks: true))
                {
                    // Read text efficiently
                    char[] buffer = new char[4096];
                    int charsRead;
                    while ((charsRead = await textReader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await Response.WriteAsync(new string(buffer, 0, charsRead));
                    }
                }
            }
        }
        else
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            HandleError(reqVars, "Could not locate file", filePathOs, 404);
        }
    }

    /// <summary>
    /// Sets the appropriate headers for a file stream
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="contentType"></param>
    /// <param name="fileSize"></param>
    /// <param name="forceDownload"></param>
    public static void SetHeaders(HttpResponse Response, String fileName, String contentType, int fileSize, bool forceDownload)
    {
        if (forceDownload)
        {
            Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";
        }
        Response.Headers["Content-Length"] = fileSize.ToString();

        // Response.Charset = "UTF-8";
        Response.ContentType = contentType + ((contentType.Contains("text")) ? "; charset=utf-8" : "");
    }

    /// <summary>
    /// Tests if a file extension is a binary based on the file definitions in application_configuration.xml
    /// </summary>
    /// <param name="extension">the extension of the file to test (i.e. 'xml' or '.xml')</param>
    /// <returns></returns>
    private static bool isBinary(String extension)
    {
        //normalize the extension that has been passed
        if (extension.StartsWith(".", StringComparison.CurrentCulture))
        {
            extension = RegExpReplace(@".*\.(.+)", extension, "$1").ToLower();
        }

        if (xmlApplicationConfiguration.SelectNodes("/configuration/file_definitions/text/file[@extension='" + extension + "']").Count > 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Deletes files older than the amount of seconds passed from the directory
    /// </summary>
    /// <param name="folderPathOs"></param>
    /// <param name="seconds"></param>
    public static void RemoveFilesOfSpecificAgeFromFolder(string folderPathOs, int seconds)
    {
        string[] files = Directory.GetFiles(folderPathOs);

        foreach (string file in files)
        {
            var fi = new FileInfo(file);
            if (fi.LastAccessTime < DateTime.Now.AddSeconds(-seconds))
            {
                fi.Delete();
            }
        }
    }

    /// <summary>
    ///  Deletes files that matches the passed regular expression older than the amount of seconds passed from the directory
    /// </summary>
    /// <param name="folderPathOs"></param>
    /// <param name="seconds"></param>
    /// <param name="regularExpression">Regular expression to match the files against</param>
    public static void RemoveFilesOfSpecificAgeFromFolder(string folderPathOs, int seconds, string regularExpression)
    {
        string[] files = Directory.GetFiles(folderPathOs);

        foreach (string file in files)
        {
            var fi = new FileInfo(file);
            string fileName = fi.Name;
            if (RegExpTest(regularExpression, fileName))
            {
                if (fi.LastAccessTime < DateTime.Now.AddSeconds(-seconds))
                {
                    fi.Delete();
                }
            }
        }
    }

    /// <summary>
    /// Removes all files from a folder recursively. Optionally accepts search patterns to allow searching for specific files
    /// </summary>
    /// <param name="folderPathOs"></param>
    /// <param name="searchPattern"></param>
    public static void RemoveFilesResursivelyFromFolder(string folderPathOs, string searchPattern = "*")
    {
        var allFilesToDelete = Directory.EnumerateFiles(folderPathOs, searchPattern, SearchOption.AllDirectories);
        foreach (var file in allFilesToDelete)
            File.Delete(file);
    }

    /// <summary>
    /// Copies an entire directory structure recursively
    /// </summary>
    /// <param name="sourceFolderPathOs">Path (OS Type) to source folder</param>
    /// <param name="targetFolderPathOs">Path (OS Type) to target directory - the directory will be created if it doesn't exist</param>
    /// <param name="overwriteExistingFiles">Optional: Specifiy if any existing files should be overwritten (default: true)</param>
    public static void CopyDirectoryRecursive(string sourceFolderPathOs, string targetFolderPathOs, bool overwriteExistingFiles = true)
    {
        CopyDirectoryAdvanced(sourceFolderPathOs, targetFolderPathOs, true, true);
    }


    /// <summary>
    /// Clones/copies a complete directory structure
    /// </summary>
    /// <param name="sourceDirectoryPathOs">Source folder</param>
    /// <param name="targetDirectoryPathOs">Target folder</param>
    /// <param name="recursiveMode">Clone all subdirectories and files or not</param>
    /// <param name="overwriteExistingFiles">Override existing files or not</param>
    /// <param name="folderFilter">Subpath that should not be copied</param>
    public static void CopyDirectoryAdvanced(string sourceDirectoryPathOs, string targetDirectoryPathOs, bool recursiveMode, bool overwriteExistingFiles = true, string folderFilter = null)
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo sourceDirectory = new DirectoryInfo(sourceDirectoryPathOs);

        if (!sourceDirectory.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: " +
                sourceDirectoryPathOs);
        }

        DirectoryInfo[] dirs = sourceDirectory.GetDirectories();
        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(targetDirectoryPathOs))
        {
            Directory.CreateDirectory(targetDirectoryPathOs);
        }

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = sourceDirectory.GetFiles();
        foreach (FileInfo file in files)
        {
            string temppath = Path.Combine(targetDirectoryPathOs, file.Name);
            file.CopyTo(temppath, overwriteExistingFiles);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (recursiveMode)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                if (folderFilter == null)
                {
                    string temppath = Path.Combine(targetDirectoryPathOs, subdir.Name);
                    CopyDirectoryAdvanced(subdir.FullName, temppath, recursiveMode, overwriteExistingFiles, folderFilter);
                }
                else
                {
                    if (!subdir.ToString().Contains(folderFilter))
                    {
                        string temppath = Path.Combine(targetDirectoryPathOs, subdir.Name);
                        CopyDirectoryAdvanced(subdir.FullName, temppath, recursiveMode, overwriteExistingFiles, folderFilter);
                    }
                }
            }
        }
    }


    /// <summary>
    /// Normalizes the filename to remove non-webfriendly characters
    /// </summary>
    /// <param name="fileNameOriginal"></param>
    /// <returns></returns>
    public static string NormalizeFileName(string fileNameOriginal)
    {
        var fileNameNormalized = Path.GetFileNameWithoutExtension(fileNameOriginal).ToLower().Trim();
        return NormalizeFileNameWithoutExtension(fileNameNormalized) + Path.GetExtension(fileNameOriginal).ToLower();
    }

    /// <summary>
    /// Normalizes the filename without extension part of a file
    /// </summary>
    /// <param name="filenameWithoutExtension"></param>
    /// <returns></returns>
    public static string NormalizeFileNameWithoutExtension(string filenameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(filenameWithoutExtension))
            return "unnamed";

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var normalized = new string(filenameWithoutExtension
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Remove apostrophes and other punctuation we don't want in filenames
        // Keep: letters, numbers, spaces, hyphens
        // Remove: apostrophes, quotes, commas, periods, parentheses, brackets, etc.
        normalized = Regex.Replace(normalized, @"['`,:;!?()[\]{}\""]", "");
        normalized = normalized.Replace(".", ""); // Remove periods separately
        // Also remove fancy quotes and apostrophes (Unicode variants)
        normalized = normalized.Replace("\u2018", "").Replace("\u2019", "")
                                .Replace("\u201C", "").Replace("\u201D", "")
                                .Replace("\u00B4", ""); // acute accent

        // Replace spaces and multiple hyphens with single hyphen
        normalized = Regex.Replace(normalized, @"\s+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-");

        // Convert to lowercase
        normalized = normalized.ToLower();

        // Trim hyphens from ends
        normalized = normalized.Trim('-');

        // Limit length to reasonable filename size
        if (normalized.Length > 50)
            normalized = normalized.Substring(0, 50).TrimEnd('-');

        return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized;
    }



    /// <summary>
    /// Renders an MD5 hash based on the content of a file
    /// </summary>
    /// <param name="filePathOs"></param>
    /// <returns></returns>
    public static string RenderFileHash(string filePathOs)
    {
        using (FileStream fStream = File.OpenRead(filePathOs))
        {
            return RenderStreamHash<MD5>(fStream);
        }
    }


    /// <summary>
    /// Copies the content of the input stream to an output stream
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    private static void Pump(Stream input, Stream output)
    {
        byte[] bytes = new byte[4096];
        int n;
        // Use a loop with proper error handling for inexact reads
        while ((n = input.Read(bytes, 0, bytes.Length)) > 0)
        {
            output.Write(bytes, 0, n);
        }
    }

    /// <summary>
    /// Returns "yes" if the path is a dir, false if it's a file and null if it's neither or doesn't exist.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool IsDirectory(string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }
        return false;
    }

    /// <summary>
    /// Test if a path is a symbolic link (reference to another file or directory)
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool IsSymbolicLink(string path)
    {
        if (IsDirectory(path))
        {
            FileInfo file = new FileInfo(path);
            return file.LinkTarget != null;
        }
        return false;
    }

    /// <summary>
    /// Pauses execution until we have obtained exclusive access to a file on the disk.
    /// </summary>
    /// <param name="fullPath">Path to the file we want to test for access</param>
    /// <param name="attempts">How many loops we need to try to gain access</param>
    /// <param name="sleepTime">How long the thread needs to sleep before we will try again</param>
    /// <returns></returns>
    public static bool HasExclusiveFileAccess(string fullPath, int attempts = 20, int sleepTime = 50)
    {
        if (!File.Exists(fullPath)) return true;
        var hasAccessToFile = false;
        for (int numTries = 0; numTries < attempts; numTries++)
        {
            try
            {
                // Attempt to open the file exclusively.
                using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 100))
                {
                    fs.ReadByte();
                    fs.Seek(0, SeekOrigin.Begin);
                    // If we got this far the file is ready
                    hasAccessToFile = true;
                }
                if (hasAccessToFile) break;
            }
            catch (Exception)
            {
                // Wait for the lock to be released
                PauseExecution(sleepTime).GetAwaiter().GetResult();
            }
        }

        return hasAccessToFile;
    }

    /// <summary>
    /// Pauses execution until we have obtained exclusive access to a file on the disk.
    /// </summary>
    /// <param name="fullPath">Path to the file we want to test for access</param>
    /// <param name="attempts">How many loops we need to try to gain access</param>
    /// <param name="sleepTime">How long the thread needs to sleep before we will try again</param>
    /// <returns></returns>
    public static async Task<bool> HasExclusiveFileAccessAsync(string fullPath, int attempts = 20, int sleepTime = 50)
    {
        if (!File.Exists(fullPath)) return true;

        var hasAccessToFile = false;
        for (int numTries = 0; numTries < attempts; numTries++)
        {
            try
            {
                // Attempt to open the file exclusively.
                using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 100))
                {
                    fs.ReadByte();
                    fs.Seek(0, SeekOrigin.Begin);
                    // If we got this far the file is ready
                    hasAccessToFile = true;
                }
                if (hasAccessToFile) break;
            }
            catch (Exception)
            {
                // Wait for the lock to be released
                await PauseExecution(sleepTime);
            }
        }

        return hasAccessToFile;
    }

    /// <summary>
    /// Recursively deletes a directory as well as any subdirectories and files. If the files are read-only, they are flagged as normal and then deleted.
    /// </summary>
    /// <param name="directory">The path of the directory to remove.</param>
    /// <param name="deleteRootFolder">Indicate if we need to remove the root folder passed as well (defaults to true)</param>
    public static void DelTree(string directory, bool deleteRootFolder = true)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
            if (!deleteRootFolder) Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Delete a complete directory tree asyn
    /// </summary>
    /// <param name="directoryPath"></param>
    /// <param name="maxRetries"></param>
    /// <param name="millisecondsDelay"></param>
    /// <returns></returns>
    public static async Task<bool> DelTreeAsync(string directoryPath, int maxRetries = 10, int millisecondsDelay = 30)
    {
        if (directoryPath == null)
            throw new ArgumentNullException(directoryPath);
        if (maxRetries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (millisecondsDelay < 1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

        for (int i = 0; i < maxRetries; ++i)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }

                return true;
            }
            catch (IOException)
            {
                await Task.Delay(millisecondsDelay);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(millisecondsDelay);
            }
        }

        return false;
    }

    /// <summary>
    /// Recurive function for removing a complete directory structure
    /// </summary>
    /// <param name="target_dir"></param>
    public static void DelTreeRecursive(string target_dir)
    {
        try
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DelTreeRecursive(dir);
            }

            Directory.Delete(target_dir, false);
        }
        catch (System.Exception)
        {

            Console.WriteLine($"ERROR: Failed to remove files or directories in: '{target_dir}', stack-trace: {GetStackTrace()}");
        }
    }

    /// <summary>
    /// Returns the filename of the last updated (youngest) file in a directory
    /// </summary>
    /// <param name="folderPathOs">Path of folder to search in</param>
    /// <param name="searchPattern">Pattern of files to look for</param>
    /// <param name="searchOption">Use SearchOption.AllDirectories to recursively search in all directories</param>
    /// <returns></returns>
    public static string GetLastUpdatedFileInDirectory(string folderPathOs, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return GetLastUpdatedFileInDirectory(new DirectoryInfo(folderPathOs), searchPattern, searchOption);
    }

    /// <summary>
    /// Returns the filename of the last updated (youngest) file in a directory
    /// </summary>
    /// <param name="directoryInfo">DirectoryInfo that contains the root folder path to search in</param>
    /// <param name="searchPattern">Pattern of files to look for</param>
    /// <param name="searchOption">Use SearchOption.AllDirectories to recursively search in all directories</param>
    /// <returns></returns>
    public static string GetLastUpdatedFileInDirectory(DirectoryInfo directoryInfo, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        try
        {
            FileInfo[] filesList = directoryInfo.GetFiles();
            if (filesList.Length > 0)
            {
                var fileInfo = (from f in filesList orderby f.LastWriteTime descending select f).First();
                return fileInfo.Name;
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            appLogger.LogError(ex, "Error occurred in GetLastUpdatedFileInDirectory()");
            return "";
        }

        return "";
    }

    /// <summary>
    /// Base64 encodes a binary file asynchronously.
    /// </summary>
    /// <returns>Base64 representation of the file content</returns>
    /// <param name="filePathOs">File path os.</param>
    public static async Task<string> Base64EncodeBinaryFileAsync(string filePathOs)
    {
        if (!File.Exists(filePathOs))
        {
            appLogger.LogError($"Could not find binary file '{filePathOs}' to encode, stack-trace: {GetStackTrace()}");
            return null;
        }

        try
        {
            // Get file size to determine the processing approach
            var fileInfo = new FileInfo(filePathOs);

            // Create optimized file stream
            using var fileStream = new FileStream(
                filePathOs,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024, // 64KB buffer size
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            // For large files (> 5MB), process in chunks to avoid large memory allocations
            if (fileInfo.Length > 5 * 1024 * 1024)
            {
                // Process in chunks to reduce memory pressure
                StringBuilder base64Builder = new StringBuilder(capacity: (int)(fileInfo.Length * 1.4)); // Base64 is ~1.33x larger

                // Rent a buffer from the pool
                const int bufferSize = 3 * 1024 * 1024; // 3MB buffer (results in ~4MB base64 chunks)
                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Convert each chunk to base64 and append to the string builder
                        string base64Chunk = Convert.ToBase64String(buffer, 0, bytesRead);
                        base64Builder.Append(base64Chunk);
                    }

                    return base64Builder.ToString();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else
            {
                // For smaller files, read all bytes at once
                // Still use the file stream we already opened for better consistency
                byte[] bytes = new byte[fileInfo.Length];
                int bytesRead = 0;
                int totalBytesRead = 0;

                // Handle inexact reads by reading until we get the full content or reach EOF
                while (totalBytesRead < fileInfo.Length &&
                      (bytesRead = await fileStream.ReadAsync(bytes, totalBytesRead, (int)fileInfo.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;
                }

                // Verify if we got all the expected bytes
                if (totalBytesRead != fileInfo.Length)
                {
                    appLogger.LogWarning($"Only read {totalBytesRead} of {fileInfo.Length} bytes from file {filePathOs}");
                }
                return Convert.ToBase64String(bytes);
            }
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"Could not Base64 encode file '{filePathOs}', stack-trace: {GetStackTrace()}");
            return null;
        }
    }

    /// <summary>
    /// Base64 encodes a binary file.
    /// </summary>
    /// <returns>Base64 representation of the file content</returns>
    /// <param name="filePathOs">File path os.</param>
    public static string Base64EncodeBinaryFile(string filePathOs)
    {
        if (!File.Exists(filePathOs))
        {
            appLogger.LogError($"Could not find binary file '{filePathOs}' to encode, stack-trace: {GetStackTrace()}");
            return null;
        }

        try
        {
            // Get file size to determine the processing approach
            var fileInfo = new FileInfo(filePathOs);

            // Create optimized file stream
            using var fileStream = new FileStream(
                filePathOs,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024); // 64KB buffer size

            // For large files (> 5MB), process in chunks to avoid large memory allocations
            if (fileInfo.Length > 5 * 1024 * 1024)
            {
                // Process in chunks to reduce memory pressure
                StringBuilder base64Builder = new StringBuilder(capacity: (int)(fileInfo.Length * 1.4)); // Base64 is ~1.33x larger

                // Rent a buffer from the pool
                const int bufferSize = 3 * 1024 * 1024; // 3MB buffer (results in ~4MB base64 chunks)
                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // Convert each chunk to base64 and append to the string builder
                        string base64Chunk = Convert.ToBase64String(buffer, 0, bytesRead);
                        base64Builder.Append(base64Chunk);
                    }

                    return base64Builder.ToString();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else
            {
                // For smaller files, read all bytes at once
                byte[] bytes = new byte[fileInfo.Length];
                int bytesRead = 0;
                int totalBytesRead = 0;

                // Handle inexact reads by reading until we get the full content or reach EOF
                while (totalBytesRead < fileInfo.Length &&
                      (bytesRead = fileStream.Read(bytes, totalBytesRead, (int)fileInfo.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;
                }

                // Verify if we got all the expected bytes
                if (totalBytesRead != fileInfo.Length)
                {
                    appLogger.LogWarning($"Only read {totalBytesRead} of {fileInfo.Length} bytes from file {filePathOs}");
                }
                return Convert.ToBase64String(bytes);
            }
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"Could not Base64 encode file '{filePathOs}', stack-trace: {GetStackTrace()}");
            return null;
        }
    }

    /// <summary>
    /// Stores a binary file on the disk based on the Base64 string that we have received
    /// </summary>
    /// <returns><c>true</c>, if decode as binary file was base64ed, <c>false</c> otherwise.</returns>
    /// <param name="base64EncodedString">Base64 encoded string.</param>
    /// <param name="filePathOs">File path os.</param>
    public static async Task<bool> Base64DecodeAsBinaryFileAsync(string base64EncodedString, string filePathOs)
    {
        // Create directory if needed
        string? directoryName = Path.GetDirectoryName(filePathOs);
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        // Open file stream with optimized settings for sequential writing
        using var fileStream = new FileStream(
            filePathOs,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024, // 64KB buffer
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        // Process in manageable chunks
        int base64Length = base64EncodedString.Length;
        int chunkSize = 64 * 1024; // 64KB chunks for base64 string

        // Calculate expected output buffer size (base64 encoding makes data ~1.33x larger)
        int outputBufferSize = (chunkSize * 3) / 4; // Approximate decoded size

        // Get a pooled buffer, sized appropriately for the chunks
        byte[] buffer = ArrayPool<byte>.Shared.Rent(outputBufferSize);

        try
        {
            for (int i = 0; i < base64Length; i += chunkSize)
            {
                int currentChunkSize = Math.Min(chunkSize, base64Length - i);
                string base64Chunk = base64EncodedString.Substring(i, currentChunkSize);

                // Convert chunk to bytes (reuse the same buffer)
                int bytesCount = Convert.TryFromBase64String(
                    base64Chunk,
                    buffer,
                    out int bytesWritten) ? bytesWritten : 0;

                // Write only the valid bytes
                if (bytesCount > 0)
                    await fileStream.WriteAsync(buffer, 0, bytesCount);
            }

            // Ensure data is flushed to disk
            await fileStream.FlushAsync();
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"Could not Base64 decode file '{filePathOs}', stack-trace: {GetStackTrace()}");
            return false;
        }
        finally
        {
            // Return the buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return true;
    }

    /// <summary>
    /// Stores a binary file on the disk based on the Base64 string that we have received
    /// </summary>
    /// <returns><c>true</c>, if decode as binary file was base64ed, <c>false</c> otherwise.</returns>
    /// <param name="base64EncodedString">Base64 encoded string.</param>
    /// <param name="filePathOs">File path os.</param>
    public static bool Base64DecodeAsBinaryFile(string base64EncodedString, string filePathOs)
    {
        // Create directory if needed
        string? directoryName = Path.GetDirectoryName(filePathOs);
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        // Open file stream with optimized settings for sequential writing
        using var fileStream = new FileStream(
            filePathOs,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024); // 64KB buffer

        // Process in manageable chunks
        int base64Length = base64EncodedString.Length;
        int chunkSize = 64 * 1024; // 64KB chunks for base64 string

        // Calculate expected output buffer size (base64 encoding makes data ~1.33x larger)
        int outputBufferSize = (chunkSize * 3) / 4; // Approximate decoded size

        // Get a pooled buffer, sized appropriately for the chunks
        byte[] buffer = ArrayPool<byte>.Shared.Rent(outputBufferSize);

        try
        {
            for (int i = 0; i < base64Length; i += chunkSize)
            {
                int currentChunkSize = Math.Min(chunkSize, base64Length - i);
                string base64Chunk = base64EncodedString.Substring(i, currentChunkSize);

                // Convert chunk to bytes (reuse the same buffer)
                int bytesCount = Convert.TryFromBase64String(
                    base64Chunk,
                    buffer,
                    out int bytesWritten) ? bytesWritten : 0;

                // Write only the valid bytes
                if (bytesCount > 0)
                    fileStream.Write(buffer, 0, bytesCount);
            }

            // Ensure data is flushed to disk
            fileStream.Flush();
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, $"Could not Base64 decode file '{filePathOs}', stack-trace: {GetStackTrace()}");
            return false;
        }
        finally
        {
            // Return the buffer to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return true;
    }

    /// <summary>
    /// Determines the file type by querying the file extension in the application configuration
    /// </summary>
    /// <returns>The file type.</returns>
    /// <param name="path">Path.</param>
    public static string GetFileType(string path)
    {
        string? fileType = null;
        var xPath = $"/configuration/file_definitions/*/file[@extension='{Path.GetExtension(path).Replace(".", "")}']";
        var nodeFileType = xmlApplicationConfiguration.SelectSingleNode(xPath);
        if (nodeFileType != null)
        {
            fileType = nodeFileType.ParentNode.LocalName;
            if (fileType.EndsWith("s", StringComparison.CurrentCulture))
            {
                fileType = fileType.TrimEnd('s');
            }
        }
        else
        {
            appLogger.LogError($"Unknown file type, path: {path}, xpath: {xPath}, stack-trace: {GetStackTrace()}");
        }

        return fileType;
    }

    /// <summary>
    /// Cleans a directory from all files and folders
    /// </summary>
    /// <param name="folderPathOs"></param>
    public static void EmptyDirectory(string folderPathOs)
    {
        var directory = new DirectoryInfo(folderPathOs);
        foreach (System.IO.FileInfo file in directory.GetFiles()) file.Delete();
        foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
    }

    /// <summary>
    /// Cleans a folder from empty sub-folders
    /// </summary>
    /// <param name="folderPathOs"></param>
    public static void DeleteEmptyDirectories(string folderPathOs)
    {
        if (string.IsNullOrEmpty(folderPathOs))
            throw new ArgumentException(
                "Starting directory is a null reference or an empty string",
                "dir");

        if (!Directory.Exists(folderPathOs))
        {
            Console.WriteLine($"Root directory {folderPathOs} does not exist, so there is nothing to cleanup");
        }
        else
        {
            try
            {
                foreach (var d in Directory.EnumerateDirectories(folderPathOs))
                {
                    DeleteEmptyDirectories(d);
                }

                var entries = Directory.EnumerateFileSystemEntries(folderPathOs);

                if (!entries.Any())
                {
                    try
                    {
                        Directory.Delete(folderPathOs);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
        }
    }



}