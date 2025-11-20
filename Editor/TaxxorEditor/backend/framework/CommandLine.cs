using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

/// <summary>
/// Contains helper functions for interacting with the command line
/// </summary>
public abstract partial class Framework
{
    /*
     * Consider using
     * SlavaGu.ConsoleAppLauncher
     * as a commandline interface helper (DLL in bin dir)
     * supports async, etc.
     * 
     */

    /// <summary>
    /// Executes a single command line and impersonates the process.
    /// Usage: ExecuteCommandLine&lt;System.Xml.XmlDocument&gt;("systeminfo""test", "", "T3st!ng");
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="commandToExecute">Command to execute on cmd</param>
    /// <param name="userName">Username to impersonate with</param>
    /// <param name="domainName">Domain name of user (leave "" if current server is the domain)</param>
    /// <param name="password">Password of the impersonated user</param>
    /// <param name="stopOnError">Stop any further processing if the command line returns an error</param>
    /// <param name="waitForExit">Defines if the function should caputure the result of the command line (true) or if it should execute in the background (false)</param>
    /// <returns></returns>
    public static T ExecuteCommandLine<T>(string commandToExecute, string? userName = null, string domainName = "", string password = "", bool stopOnError = false, bool waitForExit = true, string startInfoFileName = "cmd.exe", string workingDirectory = "c:\\")
    {
        var context = System.Web.Context.Current;
        RequestVariables reqVars = RetrieveRequestVariables(context);

        string output = "";

        ReturnTypeEnum returnType = GetReturnTypeEnum(GenericTypeToNormalizedString(typeof(T).ToString()));

        if (userName != null)
        {
            try
            {
                //impersonate the process
                //alternative http://stackoverflow.com/questions/4624113/how-to-process-start-with-impersonated-domain-user, http://stackoverflow.com/questions/4422084/impersonating-in-net-c-opening-a-file-via-process-start
                // See: authentication.cs
                //            using (new Impersonator(userName, domainName, password))
                //{
                //	return ExecuteCommandLine<T>(commandToExecute, stopOnError, waitForExit, startInfoFileName, workingDirectory);
                //}
            }
            catch (Exception ex)
            {
                var message = "Failed to impersonate user";
                var debugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";
                // Handle exception
                if (stopOnError)
                {
                    HandleError(reqVars, message, debugInfo);
                }
                else
                {
                    WriteErrorMessageToConsole(message, debugInfo);
                    output = message;
                }
            }

        }
        else
        {
            return ExecuteCommandLine<T>(commandToExecute, stopOnError, waitForExit, startInfoFileName, workingDirectory);
        }

        //cast it back to the correct format
        switch (returnType)
        {
            case ReturnTypeEnum.Xml:
                XmlDocument returnDoc = new XmlDocument();
                returnDoc.LoadXml(output);
                return (T) Convert.ChangeType(returnDoc, typeof(T));

            default:
                return (T) Convert.ChangeType(output, typeof(T));

        }

    }

    /// <summary>
    /// Execute a single command line.
    /// Usage: ExecuteCommandLine&lt;System.Xml.XmlDocument&gt;("systeminfo");
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="commandToExecute">Command to run on cmd</param>
    /// <returns></returns>
    public static T ExecuteCommandLine<T>(string commandToExecute)
    {
        return ExecuteCommandLine<T>(commandToExecute, false);
    }

    /// <summary>
    /// Executes a command line
    /// </summary>
    /// <param name="commandToExecute">Command to execute on the cmd prompt</param>
    /// <param name="stopOnError">Stop thread if the command line returns an error</param>
    /// <param name="waitForExit">Defines if the function should caputure the result of the command line (true) or if it should execute in the background (false)</param>
    /// <returns></returns>
    public static T ExecuteCommandLine<T>(string commandToExecute, bool stopOnError, bool waitForExit = true, string startInfoFileName = "cmd.exe", string workingDirectory = "c:\\")
    {

        var commandList = new List<string>();
        commandList.Add(commandToExecute);

        return ExecuteCommandLine<T>(commandList, null, "", "", stopOnError, waitForExit, startInfoFileName, workingDirectory);
    }

    /// <summary>
    /// Executes a series of commands on the command prompt.
    /// Usage: ExecuteCommandLine&lt;System.Xml.XmlDocument&gt;(commands, "test", "", "T3st!ng");
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="commandList">List of commands to fire sequentially on the command prompt</param>
    /// <param name="userName">Username to impersonate with</param>
    /// <param name="domainName">Domain name of user (leave "" if current server is the domain)</param>
    /// <param name="password">Password of the impersonated user</param>
    /// <param name="stopOnError">Stop thread if the command line returns an error</param>
    /// <returns></returns>
    public static T ExecuteCommandLine<T>(List<string> commandList, string? userName = null, string domainName = "", string password = "", bool stopOnError = false, bool waitForExit = true, string startInfoFileName = "cmd.exe", string workingDirectory = "c:\\")
    {
        string output = "";

        ReturnTypeEnum returnType = GetReturnTypeEnum(GenericTypeToNormalizedString(typeof(T).ToString()));

        if (userName != null)
        {
            try
            {
                //using (new Impersonator(userName, domainName, password))
                //{
                //	return ExecuteCommandLine<T>(commandList, stopOnError, true, true, startInfoFileName, workingDirectory);
                //}
            }
            catch (Exception ex)
            {
                var message = "Failed to impersonate user";
                var debugInfo = $"error: {ex}, stack-trace: {GetStackTrace()}";
                // Handle exception
                if (stopOnError)
                {
                    HandleError(message, debugInfo);
                }
                else
                {
                    WriteErrorMessageToConsole(message, debugInfo);
                    output = message;
                }
            }

        }
        else
        {
            return ExecuteCommandLine<T>(commandList, stopOnError, waitForExit, true, startInfoFileName, workingDirectory);
        }

        //cast it back to the correct format
        switch (returnType)
        {
            case ReturnTypeEnum.Xml:
                XmlDocument returnDoc = new XmlDocument();
                returnDoc.LoadXml(output);
                return (T) Convert.ChangeType(returnDoc, typeof(T));

            default:
                return (T) Convert.ChangeType(output, typeof(T));

        }

    }

    /// <summary>
    /// Executes a series of commands on the command prompt.
    /// Usage: ExecuteCommandLine&lt;System.Xml.XmlDocument&gt;(commands);
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="commandList">List of commands to fire sequentially on the command prompt</param>
    /// <returns></returns>
    public static T ExecuteCommandLine<T>(List<string> commandList)
    {
        return ExecuteCommandLine<T>(commandList, false);
    }

    /// <summary>
    /// Utility method that converts the command prompt to an OS specific version
    /// Windows: cmd.exe
    /// Mac/Linux: /bin/bash
    /// </summary>
    /// <returns>The command line start info file name for os.</returns>
    /// <param name="startInfoFileName">Start info file name.</param>
    private static string _convertCommandLineStartInfoFileNameForOs(string? startInfoFileName = null)
    {
        if (string.IsNullOrEmpty(startInfoFileName))
        {
            return (isWindows) ? "cmd.exe" : "/bin/bash";
        }
        else
        {
            if (startInfoFileName == "cmd.exe")
            {
                if (isWindows)
                {
                    return startInfoFileName;
                }
                else
                {
                    return "/bin/bash";
                }
            }
            else
            {
                return startInfoFileName;
            }
        }
    }

    /// <summary>
    /// Asynchronously executes a commandline call
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="workingDirectory"></param>
    /// <returns></returns>
    public static async Task<TaxxorReturnMessage> ExecuteCommandLineAsync(string cmd, string workingDirectory)
    {
        List<string> commandList = new List<string>
        {
            cmd
        };
        return await ExecuteCommandLineAsync(commandList, workingDirectory);
    }


    /// <summary>
    /// Asynchronously executes a commandline call
    /// </summary>
    /// <param name="commandList"></param>
    /// <param name="workingDirectory"></param>
    /// <returns></returns>
    public static async Task<TaxxorReturnMessage> ExecuteCommandLineAsync(List<string> commandList, string workingDirectory)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo(_convertCommandLineStartInfoFileNameForOs())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        try
        {
            using(var process = new Process
            {
                StartInfo = processStartInfo,
                    EnableRaisingEvents = true
            })
            {

                return await RunProcessAsync(process, commandList).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return new TaxxorReturnMessage(false, "There was an error executing your command", $"commands: {string.Join(",", commandList.ToArray())}, workingDirectory: {workingDirectory}, error: {ex}");
        }
    }

    /// <summary>
    /// Helper routine for executing a cmd process async
    /// </summary>
    /// <param name="process"></param>
    /// <param name="commandList"></param>
    /// <returns></returns>
    private static Task<TaxxorReturnMessage> RunProcessAsync(Process process, List<string> commandList)
    {
        var tcs = new TaskCompletionSource<TaxxorReturnMessage>();


        // Formats the output of the process to a string
        var sbSuccessOutput = new StringBuilder();
        var sbErrorOutput = new StringBuilder();

        // Code that executes when the process has finished
        process.Exited += (sender, line) =>
        {
            var successOutput = sbSuccessOutput.ToString();
            var errorOutput = sbErrorOutput.ToString();

            // Console.WriteLine("-------------");
            // Console.WriteLine(successOutput);
            // Console.WriteLine(errorOutput);
            // Console.WriteLine("-------------");


            var executedCommands = string.Join(",", commandList.ToArray());

            if (string.IsNullOrEmpty(errorOutput.Trim()) || (!string.IsNullOrEmpty(successOutput.Trim()) && !string.IsNullOrEmpty(errorOutput.Trim())))
            {
                var output = successOutput;
                if (!string.IsNullOrEmpty(errorOutput.Trim())) output += $", {errorOutput}";
                tcs.SetResult(new TaxxorReturnMessage(true, "Successfully completed command", successOutput, $"executedCommands: {executedCommands}, workingDirectory: {process.StartInfo.WorkingDirectory}"));
            }
            else
            {
                tcs.SetResult(new TaxxorReturnMessage(false, "Process returned an error", errorOutput, $"exitcode: {process.ExitCode}, executedCommands: {executedCommands}, workingDirectory: {process.StartInfo.WorkingDirectory}"));
            }
        };

        // Captures success and error output
        process.OutputDataReceived += (s, ea) => sbSuccessOutput.AppendLine(ea.Data);
        process.ErrorDataReceived += (s, ea) => sbErrorOutput.AppendLine(ea.Data);

        bool started = process.Start();

        // Write the commands that we need to execute
        foreach (var command in commandList)
        {
            process.StandardInput.WriteLine(command);
        }
        process.StandardInput.Close();


        if (!started)
        {
            throw new InvalidOperationException("Could not start process: " + process);
        }


        // Start capturing ouitput
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Return the task
        return tcs.Task;
    }

    /// <summary>
    /// Executes a series of commands on the command prompt.
    /// Usage: ExecuteCommandLine&lt;System.Xml.XmlDocument&gt;(commands, false);
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="commandList">List of commands to fire sequentially on the command prompt</param>
    /// <param name="stopOnError">Stop thread if the command line returns an error</param>
    /// <param name="echoOff">Minimizes the information which will be returned</param>
    /// <returns></returns>
    public static T ExecuteCommandLine<T>(List<string> commandList, bool stopOnError, bool waitForExit = true, bool echoOff = true, string startInfoFileName = "cmd.exe", string workingDirectory = "c:\\")
    {
        string output = "";

        var executedCommands = string.Join(",", commandList.ToArray());
        bool errorDetected = false;

        ReturnTypeEnum returnType = GetReturnTypeEnum(GenericTypeToNormalizedString(typeof(T).ToString()));

        ProcessStartInfo processStartInfo = new ProcessStartInfo(_convertCommandLineStartInfoFileNameForOs(startInfoFileName));
        processStartInfo.RedirectStandardInput = true;
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;
        processStartInfo.UseShellExecute = false;
        processStartInfo.CreateNoWindow = true;
        processStartInfo.WorkingDirectory = workingDirectory;

        var successOutput = "";
        string errorOutput = "";
        try
        {
            using(Process? process = Process.Start(processStartInfo))
            {
                if (process != null)
                {
                    if (echoOff)
                    {
                        if (isWindows)
                        {
                            process.StandardInput.WriteLine("@echo off");
                        }
                    }

                    foreach (var command in commandList)
                    {
                        // Console.WriteLine($"- command: {command}");
                        process.StandardInput.WriteLine(command);
                    }
                    process.StandardInput.Close(); // line added to stop process from hanging on ReadToEnd()
                    //output = process.StandardOutput.ReadToEnd();

                    successOutput = process.StandardOutput.ReadToEnd();
                    errorOutput = process.StandardError.ReadToEnd();

                    if (string.IsNullOrEmpty(errorOutput.Trim()) || (!string.IsNullOrEmpty(successOutput.Trim()) && !string.IsNullOrEmpty(errorOutput.Trim())))
                    {
                        output = successOutput;
                        if (!string.IsNullOrEmpty(errorOutput.Trim())) output += $", {errorOutput}";
                    }
                    else
                    {
                        errorDetected = true;
                        errorDetected = true;
                        var message = "Could not execute command";

                        var debugInfo = $"executedCommands: {executedCommands}, workingDirectory: {workingDirectory}, Exit code: {_RetrieveProcessExitCode(process).ToString()}, Command line output: {errorOutput} , Success output: {successOutput}, stack-trace: {GetStackTrace()}";
                        // Handle exception
                        if (stopOnError)
                        {
                            HandleError(message, debugInfo);
                        }
                        else
                        {
                            WriteErrorMessageToConsole(message, debugInfo);
                            output = message;
                        }
                    }
                    if (waitForExit) process.WaitForExit(20000);
                    process.Close();
                }
                else
                {
                    errorDetected = true;
                    var message = "Process object not available";
                    var debugInfo = $"executedCommands: {executedCommands}, workingDirectory: {workingDirectory}, stack-trace: {GetStackTrace()}";;
                    // Handle exception
                    if (stopOnError)
                    {
                        HandleError(message, debugInfo);
                    }
                    else
                    {
                        WriteErrorMessageToConsole(message, debugInfo);
                        output = message;
                    }
                }

            }
        }
        catch (Exception ex)
        {
            errorDetected = true;
            var message = "Failed to start the process";
            var debugInfo = $"error: {ex}, executedCommands: {executedCommands}, workingDirectory: {workingDirectory}, stack-trace: {GetStackTrace()}";
            // Handle exception
            if (stopOnError)
            {
                HandleError(returnType, message, debugInfo);
            }
            else
            {
                WriteErrorMessageToConsole(message, debugInfo);
                output = message;
            }
        }

        //format the output when the command was successfully executed
        if (!errorDetected)
        {
            output = FormatCommandLineOutput(output, returnType);
        }


        //cast it back to the correct format
        switch (returnType)
        {
            case ReturnTypeEnum.Xml:
                XmlDocument returnDoc = new XmlDocument();

                if (errorDetected)
                {
                    returnDoc.LoadXml("<result/>");
                    var nodeError = returnDoc.CreateElement("error");
                    nodeError.InnerText = errorOutput;
                    var nodeSuccess = returnDoc.CreateElement("success");
                    nodeSuccess.InnerText = successOutput;
                    returnDoc.DocumentElement.AppendChild(nodeSuccess);
                    returnDoc.DocumentElement.AppendChild(nodeError);

                }
                else
                {
                    returnDoc.LoadXml(output);
                }
                return (T) Convert.ChangeType(returnDoc, typeof(T));

            default:
                return (T) Convert.ChangeType(output, typeof(T));

        }
    }

    /// <summary>
    /// Executes a request on the commandline
    /// </summary>
    /// <param name="cmd"></param>
    /// <returns></returns>
    public static string ExecuteCommandLine(string cmd)
    {
        try
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            ProcessStartInfo processStartInfo = new ProcessStartInfo(_convertCommandLineStartInfoFileNameForOs());
            processStartInfo.Arguments = $"-c \"{escapedArgs}\"";
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;

            using(Process? process = Process.Start(processStartInfo))
            {
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex}";
        }
    }

    /// <summary>
    /// Utility function that will return a string based on the passed ReturnTypeEnum
    /// </summary>
    /// <param name="commandlineOutput"></param>
    /// <param name="returnType"></param>
    /// <returns></returns>
    private static string FormatCommandLineOutput(string commandlineOutput, ReturnTypeEnum returnType = ReturnTypeEnum.Txt)
    {
        string output = commandlineOutput;
        switch (returnType)
        {
            case ReturnTypeEnum.Xml:
                XmlDocument returnXml = new XmlDocument();
                returnXml.LoadXml("<result/>");
                returnXml.DocumentElement.InnerText = commandlineOutput;
                output = returnXml.OuterXml;
                break;

        }

        return output;
    }

    /// <summary>
    /// Attempts to retrieve the exit code from a commandline process
    /// </summary>
    /// <param name="process"></param>
    /// <returns></returns>
    private static int _RetrieveProcessExitCode(Process process)
    {
        var exitCode = 500;
        try
        {
            exitCode = process?.ExitCode ?? 501;
        }
        catch (Exception) { }
        return exitCode;
    }
}