using System.Xml;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with replacing placeholders in paths with actual values
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Resolves the dynamic placeholders in the XML document and remembers the original values
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument DynamicPlaceholdersResolve(XmlDocument xmlDocument, RequestVariables reqVars, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {

            /*
                    xhtml:img[starts-with(@src, '/publications')] | 
                    xhtml:object[starts-with(@data, '/publications')] | 
            */

            var nodeListVisuals = xmlDocument.SelectNodes("//img[starts-with(@src, '/dataserviceassets')]");
            foreach (XmlNode nodeVisual in nodeListVisuals)
            {
                var originalSrc = nodeVisual.GetAttribute("src");
                nodeVisual.SetAttribute("src", _replacePlaceholders(originalSrc, reqVars, projectVars));
                nodeVisual.SetAttribute("data-originalvalue-src", originalSrc);
            }

            var nodeListDrawings = xmlDocument.SelectNodes("//object[starts-with(@data, '/dataserviceassets')]");
            foreach (XmlNode nodeDrawing in nodeListDrawings)
            {
                var originalDataSource = nodeDrawing.GetAttribute("data");
                nodeDrawing.SetAttribute("data", _replacePlaceholders(originalDataSource, reqVars, projectVars));
                nodeDrawing.SetAttribute("data-originalvalue-data", originalDataSource);
            }

            return xmlDocument;
        }

        /// <summary>
        /// Uses the remembered values to restore the original values of the dynamically replaced {placeholder} values
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlDocument DynamicPlaceholdersRestore(XmlDocument xmlDocument, ProjectVariables projectVars)
        {
            var nodeListVisuals = xmlDocument.SelectNodes("//img[@data-originalvalue-src]");
            foreach (XmlNode nodeVisual in nodeListVisuals)
            {
                nodeVisual.SetAttribute("src", nodeVisual.GetAttribute("data-originalvalue-src"));
                nodeVisual.RemoveAttribute("data-originalvalue-src");
            }

            var nodeListDrawings = xmlDocument.SelectNodes("//object[@data-originalvalue-data]");
            foreach (XmlNode nodeDrawing in nodeListDrawings)
            {
                nodeDrawing.SetAttribute("data", nodeDrawing.GetAttribute("data-originalvalue-data"));
                nodeDrawing.RemoveAttribute("data-originalvalue-data");
            }

            return xmlDocument;
        }

        /// <summary>
        /// Generic function to replace placeholders in paths with their relevant value
        /// </summary>
        /// <param name="path"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        private static string _replacePlaceholders(string path, RequestVariables reqVars, ProjectVariables projectVars)
        {
            if (path.Contains("{"))
            {
                if (path.Contains("{siteid}")) path = path.Replace("{siteid}", projectVars.projectId);
                if (path.Contains("{projectid}")) path = path.Replace("{projectid}", projectVars.projectId);
            }


            return path;
        }


        /// <summary>
        /// Transforms data-contenteditable attributes to contenteditable attributes used in the Taxxor Editor to make an element explicitly editable or not
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument ContentEditableResolve(XmlDocument xmlDocument, RequestVariables reqVars, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {
            var nodeListExplicitContentEditable = xmlDocument.SelectNodes("//*[@data-contenteditable]");
            foreach (XmlNode nodeExplicitContentEditable in nodeListExplicitContentEditable)
            {
                nodeExplicitContentEditable.SetAttribute("contenteditable", nodeExplicitContentEditable.GetAttribute("data-contenteditable") ?? "false");
            }

            return xmlDocument;
        }


    }
}