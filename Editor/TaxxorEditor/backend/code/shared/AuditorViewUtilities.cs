using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities for the auditor view
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// To describe the auditor area/scope in a standardized way
        /// </summary>
        public enum AuditorAreaEnum
        {
            FilingData,
            FilingContent,
            UserManagement,
            AccessRights,
            Unknown
        }

        /// <summary>
        /// Converts an AuditorAreaEnum value to a string
        /// </summary>
        /// <returns>The area enum from string.</returns>
        /// <param name="method">Method.</param>
        public static AuditorAreaEnum AuditorAreaEnumFromString(string method)
        {
            switch (method.ToLower())
            {
                case "filingdata":
                    return AuditorAreaEnum.FilingData;

                case "filingcontent":
                    return AuditorAreaEnum.FilingContent;

                case "usermanagement":
                    return AuditorAreaEnum.UserManagement;

                case "accessrights":
                    return AuditorAreaEnum.AccessRights;

                default:
                    return AuditorAreaEnum.Unknown;
            }
        }

        /// <summary>
        /// Converts a string to an AuditorAreaEnum
        /// </summary>
        /// <returns>The area enum to string.</returns>
        /// <param name="auditorAreaEnum">Auditor area enum.</param>
        public static string AuditorAreaEnumToString(AuditorAreaEnum auditorAreaEnum)
        {
            switch (auditorAreaEnum)
            {
                case AuditorAreaEnum.FilingData:
                    return "filingdata";

                case AuditorAreaEnum.FilingContent:
                    return "filingcontent";

                case AuditorAreaEnum.UserManagement:
                    return "usermanagement";

                case AuditorAreaEnum.AccessRights:
                    return "accessrights";

                default:
                    return "unknown";
            }
        }

        /// <summary>
        /// Creates an auditor message.
        /// </summary>
        /// <returns>The auditor message.</returns>
        /// <param name="auditorAreaEnum">Auditor area enum.</param>
        /// <param name="auditorMessage">Message.</param>
        public static async Task<TaxxorReturnMessage> CreateAuditorMessage(AuditorAreaEnum auditorAreaEnum, string auditorMessage, string projectId)
        {
            /*
             * Auditor messages are currently implemented as a GIT commit.
             * The auditor area enum determines which GIT repository to use
             */

            var gitRepositoryPathOs = "";

            // Check passed enum
            if (auditorAreaEnum == AuditorAreaEnum.Unknown) return new TaxxorReturnMessage(false, "Cound not store auditor message.", $"Unknown auditor area, stack-trace: {GetStackTrace()}");

            if (applicationId == "documentstore")
            {

                // Now we can directly work with the GIT repositories

                if (auditorAreaEnum == AuditorAreaEnum.FilingData || auditorAreaEnum == AuditorAreaEnum.FilingContent)
                {
                    // 0) Determine the path we need to work with
                    var dataConfigurationPathOs = CalculateFullPathOs("/configuration/configuration_system//config/location[@id='data_configuration']");
                    var xmlDataConfig = new XmlDocument();
                    xmlDataConfig.Load(dataConfigurationPathOs);

                    // 1) Retrieve project details
                    var nodeProjectsRoot = xmlDataConfig.SelectSingleNode("//cms_projects");
                    var nodeCurrentProject = xmlDataConfig.SelectSingleNode("//cms_project[@id='" + projectId + "']");
                    // Handle error
                    if (nodeCurrentProject == null) return new TaxxorReturnMessage(false, "An application with this ID does not exist.", $"Project with xpath //cms_project[@id='{projectId}'] does not exist, stack-trace: {GetStackTrace()}");

                    //var projectPathOs = cmsRootPathOs + xmlProjectConfiguration.SelectSingleNode("//cms_project[@id='" + projectIdToRemove + "']/path").InnerText;
                    var projectDataPathOs = dataRootPathOs + RetrieveNodeValueIfExists("/configuration/cms_projects/cms_project[@id='" + projectId + "']/location[@id='reportdataroot']", xmlDataConfig);

                    // Handle error
                    if (!Directory.Exists(projectDataPathOs)) return new TaxxorReturnMessage(false, "Project data folder could not be located", $"Folder '{projectDataPathOs}' does not exist, stack-trace: {GetStackTrace()}");

                    // Determine the path to the GIT repository to use
                    if (auditorAreaEnum == AuditorAreaEnum.FilingData)
                    {
                        gitRepositoryPathOs = projectDataPathOs;
                    }
                    else
                    {
                        gitRepositoryPathOs = projectDataPathOs + xmlApplicationConfiguration.SelectSingleNode("//locations/location[@id='cmscontentroot']").InnerText;
                    }

                    // Commit the data in the repository and return an error if it failed
                    if (!GitCommit(auditorMessage, gitRepositoryPathOs)) return new TaxxorReturnMessage(false, "Failed to store auditor message", $"auditorMessage: {auditorMessage}, gitRepositoryPathOs: {gitRepositoryPathOs}, stack-trace: {GetStackTrace()}");
                }
                else
                {
                    return new TaxxorReturnMessage(false, "Auditor area not yet supported", $"auditorAreaEnum: {AuditorAreaEnumToString(auditorAreaEnum)}, stack-trace: {GetStackTrace()}");
                }

            }
            else
            {
                // We need to call the Taxxor Data service to store the auditor information
                var createProjectDataToPost = new Dictionary<string, string>();
                createProjectDataToPost.Add("pid", projectId);
                createProjectDataToPost.Add("message", auditorMessage);
                createProjectDataToPost.Add("auditorarea", AuditorAreaEnumToString(auditorAreaEnum));

                XmlDocument auditorStoreResult = await CallTaxxorDataService<XmlDocument>(RequestMethodEnum.Put, "taxxoreditorauditordatamessage", createProjectDataToPost, true);

                // Convert the result to a TaxxorReturnMessage and return it
                return new TaxxorReturnMessage(auditorStoreResult);
            }

            // Return success message
            return new TaxxorReturnMessage(true, "Successfully created auditor message", $"gitRepositoryPathOs: {gitRepositoryPathOs}");
        }

    }
}
