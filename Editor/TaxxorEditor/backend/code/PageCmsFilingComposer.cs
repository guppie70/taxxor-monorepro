using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic used for the filing composers
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Renders the dropdown list for selecting the different output channels of a report
        /// </summary>
        /// <param name="onlyRenderChannelsThisUserHasAccessTo"></param>
        /// <param name="classValue"></param>
        /// <returns></returns>
        public static string RenderOutputChannelSelector(bool onlyRenderChannelsThisUserHasAccessTo = true, string classValue = "select-outputchannels form-control")
        {
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");


            // Build a dictionary containing the hierarchy id and a string for the first accessible section id
            Dictionary<string, string> hierarchyProperties = new Dictionary<string, string>();
            for (int i = 0; i < projectVars.cmsMetaData.Count; i++)
            {
                string metadataKeyHierarchy = projectVars.cmsMetaData.ElementAt(i).Key;

                if (projectVars.cmsMetaData.ContainsKey(metadataKeyHierarchy))
                {
                    MetaData metaData = projectVars.cmsMetaData.ElementAt(i).Value;
                    var xmlFilingDocumentHierarchy = new XmlDocument();
                    xmlFilingDocumentHierarchy = projectVars.cmsMetaData[metadataKeyHierarchy].Xml;
                    var hierarchyXpath = "/items/structured/item/sub_items/item[1]";
                    if (metadataKeyHierarchy.Contains("web"))
                    {
                        hierarchyXpath = "/items/structured/item";
                    }

                    if (onlyRenderChannelsThisUserHasAccessTo)
                    {
                        hierarchyXpath += "[permissions/permission/@id='view' or permissions/permission/@id='all']";

                        var nodeIdFirstOpenInEditor = xmlFilingDocumentHierarchy.SelectSingleNode(hierarchyXpath);
                        var editableForUser = (nodeIdFirstOpenInEditor != null);
                        if (editableForUser) hierarchyProperties.Add(metadataKeyHierarchy, GetAttribute(nodeIdFirstOpenInEditor, "id"));
                    }
                    else
                    {
                        var nodeIdFirstOpenInEditor = xmlFilingDocumentHierarchy.SelectSingleNode(hierarchyXpath);
                        hierarchyProperties.Add(metadataKeyHierarchy, GetAttribute(nodeIdFirstOpenInEditor, "id"));
                    }
                }
            }

            // Build the select box for in the editor
            StringBuilder sbHtml = new StringBuilder();
            sbHtml.AppendLine($"<select class=\"{classValue}\">");

            if (!string.IsNullOrEmpty(projectVars.editorId))
            {
                var nodeListOutputChannels = projectVars.xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels");
                if (nodeListOutputChannels.Count == 0) nodeListOutputChannels = xmlApplicationConfiguration.SelectNodes("/configuration/editors/editor[@id=" + GenerateEscapedXPathString(projectVars.editorId) + "]/output_channels");

                foreach (XmlNode nodeOutputChannels in nodeListOutputChannels)
                {
                    var nodeListOutputChannel = nodeOutputChannels.SelectNodes("output_channel");
                    foreach (XmlNode nodeOutputChannel in nodeListOutputChannel)
                    {
                        string? outputChannelType = GetAttribute(nodeOutputChannel, "type");
                        var nodeListOutputChannelVariants = nodeOutputChannel.SelectNodes("variants/variant");
                        foreach (XmlNode nodeVariant in nodeListOutputChannelVariants)
                        {
                            var isEditable = false;
                            var hierarchyId = GetAttribute(nodeVariant, "metadata-id-ref");
                            var firstEditableId = "";
                            if (!string.IsNullOrEmpty(hierarchyId))
                            {
                                if (hierarchyProperties.ContainsKey(hierarchyId))
                                {
                                    isEditable = true;
                                    firstEditableId = hierarchyProperties[hierarchyId];
                                }
                            }

                            var isAvailableInProjectConfig = true;
                            if (RetrieveOutputchannelHierarchyLocationXmlNode(projectVars.projectId, hierarchyId) == null) isAvailableInProjectConfig = false;

                            if (isEditable && isAvailableInProjectConfig)
                            {

                                var currentOutputChannelVariantId = GetAttribute(nodeVariant, "id");
                                var currentOutputChannelLanguage = GetAttribute(nodeVariant, "lang");

                                var outputChannelName = nodeVariant.SelectSingleNode("name").InnerText;

                                sbHtml.AppendLine($"<option data-octype=\"{outputChannelType}\" data-ocvariant=\"{currentOutputChannelVariantId}\" data-oclang=\"{currentOutputChannelLanguage}\" data-firstedit=\"{firstEditableId}\">{outputChannelName}</option>");
                            }

                        }
                    }

                }

            }
            else
            {
                appLogger.LogError("!! Could not determine first editable page ID because projectVars.editorId is empty !!");
            }

            sbHtml.AppendLine("</select>");

            return sbHtml.ToString();

        }

    }
}