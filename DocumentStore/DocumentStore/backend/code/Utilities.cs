using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Generic utility functions to work for the Taxxor Data Store
    /// </summary>

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the data folder path os for a project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static string RetrieveContentDataFolderPathOs(string projectId)
        {
            var context = System.Web.Context.Current;
            ProjectVariables projectVars = RetrieveProjectVariables(context, false);

            // Global variables
            var xmlSectionFolderPathOs = "";
            // TODO: Hard coded for now, but we could find this dynamically
            var currentEditorContentType = "regular";

            var nodeProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]");
            if (nodeProject == null) HandleError($"Could not find project", $"stack-trace: {GetStackTrace()}");

            //add the version path to the cms data root path
            var versionId = "1";
            var xpath = "/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/versions/version[@id=" + GenerateEscapedXPathString(versionId) + "]/path";
            var versionSubPath = RetrieveNodeValueIfExists(xpath, xmlApplicationConfiguration);
            versionSubPath = versionSubPath.Replace("[versionid]", versionId);
            if (string.IsNullOrEmpty(versionSubPath)) HandleError($"Could not find the version sub path", $"xpath: {xpath}, stack-trace: {GetStackTrace()}");

            // appLogger.LogDebug($"- projectVars.cmsDataRootPathOs: {projectVars.cmsDataRootPathOs}, versionSubPath: {versionSubPath}");

            var nodeSourceDataLocation = nodeProject.SelectSingleNode($"content_types/content_management/type[@id={GenerateEscapedXPathString(currentEditorContentType)}]/xml");

            if (projectVars?.cmsDataRootPathOs?.Contains(versionSubPath)??false)
            {
                xmlSectionFolderPathOs = Path.GetDirectoryName($"{projectVars.cmsDataRootPathOs}{nodeSourceDataLocation.InnerText}");
            }
            else
            {
                var currentDataRootBasePathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectId) + "]/location[@id='reportdataroot']", xmlApplicationConfiguration);

                var currentDataRootPathOs = currentDataRootBasePathOs + versionSubPath;
                xmlSectionFolderPathOs = Path.GetDirectoryName($"{currentDataRootPathOs}{nodeSourceDataLocation.InnerText}");
            }

            if (!Directory.Exists(xmlSectionFolderPathOs)) HandleError($"Could not find the folder for the content data", $"xmlSectionFolderPathOs: {xmlSectionFolderPathOs}, stack-trace: {GetStackTrace()}");

            return xmlSectionFolderPathOs;
        }

        /// <summary>
        /// Retrieves all the paths to xml content files for a project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static List<string> RetrieveSectionDataFilePaths(string projectId)
        {
            List<string> contentXmlPaths = new List<string>();
            var dataFolderPathOs = RetrieveContentDataFolderPathOs(projectId);

            string[] files = Directory.GetFiles(dataFolderPathOs);

            foreach (string file in files)
            {
                //var fi = new FileInfo(file);

                if (file.EndsWith(".xml") && !file.EndsWith("_footnotes.xml"))
                {
                    if (!file.Contains("__table-") && !file.StartsWith("__structured-data"))
                    {
                        contentXmlPaths.Add(file);
                    }

                }

            }

            return contentXmlPaths;
        }

        /// <summary>
        /// Retrieves all the paths to xml cached table files for a project
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public static List<string> RetrieveTableDataFilePaths(string projectId)
        {
            List<string> tableXmlPaths = new List<string>();
            var dataFolderPathOs = RetrieveContentDataFolderPathOs(projectId);

            string[] files = Directory.GetFiles(dataFolderPathOs);

            foreach (string file in files)
            {
                //var fi = new FileInfo(file);

                if (file.EndsWith(".xml") && !file.EndsWith("_footnotes.xml"))
                {
                    if (file.Contains("__table-"))
                    {
                        tableXmlPaths.Add(file);
                    }

                }

            }

            return tableXmlPaths;
        }

    }
}