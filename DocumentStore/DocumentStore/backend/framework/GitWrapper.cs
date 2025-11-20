using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Extensions.Logging;


/// <summary>
/// GIT Wrapper Implementation
/// </summary>
public abstract partial class Framework
{
    /// <summary>
    /// Defines the different GIT reposotory types
    /// </summary>
    public enum GitTypeEnum
    {
        Normal,
        Submodule,
        None
    }

    /// <summary>
    /// Determines the type of GIT repository in the folder path that was passed
    /// </summary>
    /// <returns>The git repository type.</returns>
    /// <param name="repositoryPathOs">Repository path os.</param>
    public static GitTypeEnum RetrieveGitRepositoryType(string repositoryPathOs)
    {
        try
        {
            if (Directory.Exists($"{repositoryPathOs}/.git"))
            {
                return GitTypeEnum.Normal;

            }
            else
            {
                if (File.Exists($"{repositoryPathOs}/.git"))
                {
                    return GitTypeEnum.Submodule;
                }
                else
                {
                    return GitTypeEnum.None;
                }
            }
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, "Could not determine the GIT repository type");
            return GitTypeEnum.None;
        }
    }


    /// <summary>
    /// Utility function to execute a single GIT command on the command line
    /// </summary>
    /// <param name="gitCommand">GIT command to execute</param>
    /// <param name="directoryPathOs">Optional: an alternative path to point the GIT executable to (defaults to application root path)</param>
    /// <param name="userName">Optional username to impersonate with</param>
    /// <param name="domainName"></param>
    /// <param name="password"></param>
    /// <returns>An XmlDocument object containing the information returned by the GIT command</returns>
    public static XmlDocument ExecuteGitCommand(string gitCommand, string? directoryPathOs = null, string? userName = null, string domainName = "", string password = "")
    {
        List<string> commands = [gitCommand];

        return ExecuteGitCommand(commands, directoryPathOs, userName, domainName, password);
    }

    /// <summary>
    /// Utility function to execute a series of GIT commands on the command line
    /// </summary>
    /// <param name="commandList">List of git commands to sequentially execute</param>
    /// <param name="directoryPathOs">Optional: an alternative path to point the GIT executable to (defaults to application root path)</param>
    /// <param name="userName">Optional username to impersonate with</param>
    /// <param name="domainName"></param>
    /// <param name="password"></param>
    /// <returns>An XmlDocument object containing the information returned by the GIT command</returns>
    public static XmlDocument ExecuteGitCommand(List<string> commandList, string? directoryPathOs = null, string? userName = null, string domainName = "", string password = "")
    {


        if (string.IsNullOrEmpty(directoryPathOs))
        {
            directoryPathOs = applicationRootPathOs;
        }
        else
        {
            if (!Directory.Exists(directoryPathOs))
            {
                HandleError(new RequestVariables { returnType = ReturnTypeEnum.Xml }, "Could not find path", "Path: " + directoryPathOs + " was not found");
            }
        }


        string gitExecutableDosPathOs = "git";
        //string gitExecutableDosPathOs = "C:/PROGRA~1/Git/bin/git.exe";
        //if (Environment.Is64BitOperatingSystem) gitExecutableDosPathOs = "C:/PROGRA~2/Git/bin/git.exe";

        //build a list of normalized GIT commands
        List<string> commands = new List<string>();
        foreach (var gitCommand in commandList)
        {
            var commandToExecute = gitCommand;
            if (commandToExecute.StartsWith("git ", StringComparison.CurrentCulture))
            {
                commandToExecute = commandToExecute.Replace("git ", "");
            }
            commands.Add(gitExecutableDosPathOs + " " + commandToExecute);
        }


        XmlDocument xmlResult = ExecuteCommandLine<System.Xml.XmlDocument>(commands, userName, domainName, password, false, false, _convertCommandLineStartInfoFileNameForOs("cmd.exe"), directoryPathOs);
        //XmlDocument xmlResult = ExecuteCommandLine<System.Xml.XmlDocument>(latestGitCommand, userName, domainName, password, false, true, "cmd.exe", directoryPathOs);
        if (xmlResult.DocumentElement.LocalName != "error")
        {
            var gitOutput = "unknown";
            var regExp = "";
            var gitResult = "";
            try
            {
                gitResult = xmlResult.DocumentElement.InnerText;
                if (gitResult.Contains("fatal:"))
                {
                    gitOutput = gitResult;
                }
                else
                {
                    regExp = "^.*(=?\r\n)(.*)$";
                    gitOutput = RegExpReplace(regExp, gitResult, "$1", true);
                }
            }
            catch (Exception ex)
            {
                appLogger.LogWarning($"There was a problem retrieving the result from the git operation. regExp: {regExp}, gitResult: {gitResult}, error: {ex}");
            }

            xmlResult.LoadXml("<result/>");
            xmlResult.DocumentElement.InnerText = gitOutput;
        }

        return xmlResult;
    }

    /// <summary>
    /// Wrapper function for a single GIT command: returns the result or null if something went wrong (optionally die using the supplied error type format)
    /// </summary>
    /// <param name="gitCommand">Git command to execute</param>
    /// <param name="directoryPathOs">Directory where the GIT command needs to run (defaults to the application root pat of the current site)</param>
    /// <param name="returnTypeError"></param>
    /// <param name="stopOnError"></param>
    /// <param name="userName">Optional username to impersonate with</param>
    /// <param name="domainName"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static string? GitCommand(string gitCommand, string? directoryPathOs = null, ReturnTypeEnum returnTypeError = ReturnTypeEnum.Txt, bool stopOnError = true, string? userName = null, string domainName = "", string password = "")
    {
        string? result = null;

        var xmlGitResult = ExecuteGitCommand(gitCommand, directoryPathOs);

        if (XmlContainsError(xmlGitResult))
        {
            if (stopOnError)
            {
                HandleError(new RequestVariables { returnType = returnTypeError }, xmlGitResult.SelectSingleNode("/error/message").InnerText, xmlGitResult.SelectSingleNode("/error/debuginfo").InnerText);
            }
            else
            {
                WriteErrorMessageToConsole(xmlGitResult.SelectSingleNode("/error/message").InnerText, xmlGitResult.SelectSingleNode("/error/debuginfo").InnerText);
            }
        }
        else
        {
            result = xmlGitResult.DocumentElement.InnerText;
        }

        return result;
    }

    /// <summary>
    /// Wrapper function for a list of GIT commands that need to be sequentially executed: returns the result or null if something went wrong (optionally die using the supplied error type format)
    /// </summary>
    /// <param name="commandList"></param>
    /// <param name="directoryPathOs">Directory where the GIT command needs to run (defaults to the application root pat of the current site)</param>
    /// <param name="returnTypeError"></param>
    /// <param name="stopOnError"></param>
    /// <param name="userName">Optional username to impersonate with</param>
    /// <param name="domainName"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static string? GitCommand(List<string> commandList, string? directoryPathOs = null, ReturnTypeEnum returnTypeError = ReturnTypeEnum.Txt, bool stopOnError = true, string? userName = null, string domainName = "", string password = "")
    {
        string? result = null;

        var xmlGitResult = ExecuteGitCommand(commandList, directoryPathOs);

        if (XmlContainsError(xmlGitResult))
        {
            if (stopOnError)
            {
                HandleError(new RequestVariables { returnType = returnTypeError }, xmlGitResult.SelectSingleNode("/error/message").InnerText, xmlGitResult.SelectSingleNode("/error/debuginfo").InnerText);
            }
            else
            {
                WriteErrorMessageToConsole(xmlGitResult.SelectSingleNode("/error/message").InnerText, xmlGitResult.SelectSingleNode("/error/debuginfo").InnerText);
            }
        }
        else
        {
            result = xmlGitResult.DocumentElement.InnerText;
        }

        return result;
    }


}