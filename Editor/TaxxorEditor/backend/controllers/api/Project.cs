using System.Xml;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {



        /// <summary>
        /// List available projects
        /// </summary>
        /// <param name="projectStatusFilter"></param>
        /// <param name="reportTypeFilter"></param>
        /// <returns></returns>
        public static XmlDocument ListProjects(string projectStatusFilter = null, string reportTypeFilter = null)
        {
            var xmlProjectOverview = new XmlDocument();
            var nodeProjects = xmlProjectOverview.CreateElement("projects");

            // Create a filter selection
            var filter = "";
            if (projectStatusFilter != null || reportTypeFilter != null)
            {
                if (projectStatusFilter == null && reportTypeFilter != null)
                {
                    filter = $"[@report-type='{reportTypeFilter}']";
                }
                else if (projectStatusFilter != null && reportTypeFilter == null)
                {
                    filter = $"[versions/version/status='{projectStatusFilter}']";
                }
                else
                {
                    filter = $"[@report-type='{reportTypeFilter}' and versions/version/status='{projectStatusFilter}']";
                }
            }
            

            var nodeListProjects = xmlApplicationConfiguration.SelectNodes($"/configuration/cms_projects/cms_project{filter}");
            foreach (XmlNode nodeProject in nodeListProjects)
            {
                var nodeMetaProject = xmlProjectOverview.CreateElement("project");

                var nodeMetaProjectId = xmlProjectOverview.CreateElement("id");
                nodeMetaProjectId.InnerText = GetAttribute(nodeProject, "id");
                nodeMetaProject.AppendChild(nodeMetaProjectId);

                var nodeMetaReportType = xmlProjectOverview.CreateElement("report-type");
                nodeMetaReportType.InnerText = GetAttribute(nodeProject, "report-type");
                nodeMetaProject.AppendChild(nodeMetaReportType);

                var nodeMetaProjectName = xmlProjectOverview.CreateElement("name");
                nodeMetaProjectName.InnerText = RetrieveNodeValueIfExists("name", nodeProject, false);
                nodeMetaProject.AppendChild(nodeMetaProjectName);

                var nodeMetaProjectState = xmlProjectOverview.CreateElement("status");
                nodeMetaProjectState.InnerText = RetrieveNodeValueIfExists("versions/version/status", nodeProject, false);
                nodeMetaProject.AppendChild(nodeMetaProjectState);

                nodeProjects.AppendChild(nodeMetaProject);
            }


            xmlProjectOverview.AppendChild(nodeProjects);

            return xmlProjectOverview;
        }

    }

}