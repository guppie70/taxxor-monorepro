using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;


namespace Taxxor.Project
{

    /// <summary>
    /// Logic for removing projects/documents/filings
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {




        /// <summary>
        /// Removes a project from the CMS
        /// </summary>
        /// <param name="projectIdToRemove"></param>
        /// <param name="backupProject"></param>
        /// <param name="returnType"></param>
        public static async Task<TaxxorReturnMessage> ProjectDelete(string projectIdToRemove, bool backupProject = false, ReturnTypeEnum returnType = ReturnTypeEnum.Xml)
        {
            var context = System.Web.Context.Current;

            await DummyAwaiter();

            // 0) Find paths involved by checking project configuration
            var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
            var xmlDataConfiguration = new XmlDocument();
            xmlDataConfiguration.Load(dataConfigurationPathOs);

            // 1) Retrieve project details
            var nodeProjectsRoot = xmlDataConfiguration.SelectSingleNode("//cms_projects");
            var nodeCurrentProjectToDelete = xmlDataConfiguration.SelectSingleNode("//cms_project[@id='" + projectIdToRemove + "']");
            // Handle error
            if (nodeCurrentProjectToDelete == null) return new TaxxorReturnMessage(false, "An application with this ID does not exist.", $"Project with xpath //cms_project[@id='{projectIdToRemove}'] does not exist, stack-trace: {GetStackTrace()}");
            var projectName = nodeCurrentProjectToDelete.SelectSingleNode("name").InnerText;

            //var projectPathOs = cmsRootPathOs + xmlProjectConfiguration.SelectSingleNode("//cms_project[@id='" + projectIdToRemove + "']/path").InnerText;
            var projectDataPathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id='" + projectIdToRemove + "']/location[@id='reportdataroot']", xmlDataConfiguration);

            // Handle error
            if (!Directory.Exists(projectDataPathOs)) return new TaxxorReturnMessage(false, "Project data folder could not be located", $"Folder '{projectDataPathOs}' does not exist, stack-trace: {GetStackTrace()}");

            // 2) Remove the node from project config and save it
            nodeProjectsRoot.RemoveChild(nodeCurrentProjectToDelete);
            var saved = await SaveXmlDocument(xmlDataConfiguration, dataConfigurationPathOs);
            if (!saved) return new TaxxorReturnMessage(false, "Could not save the data configuration file.", $"dataConfigurationPathOs: {dataConfigurationPathOs}, stack-trace: {GetStackTrace()}");

            // 3) Remove the files and folders
            try
            {
                //used this function in order to be able to remove the GIT repositories
                DelTree(projectDataPathOs);
            }
            catch (Exception e)
            {
                return new TaxxorReturnMessage(false, "Data folder could not be deleted", $"Folder '{projectDataPathOs}' could not be removed. Details: {e.ToString()}, stack-trace: {GetStackTrace()}");
            }

            // 4) Update the CMS metadata content
            await UpdateCmsMetadata();

            // 5) Return a success message
            return new TaxxorReturnMessage(true, @"Successfully removed project '" + projectName + @"'", $"projectDataPathOs: {projectDataPathOs}");
        }


    }
}