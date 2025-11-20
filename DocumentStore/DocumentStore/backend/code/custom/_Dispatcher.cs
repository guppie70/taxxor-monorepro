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
    /// Dispatches calls to custom logic per Taxxor customer
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Routine that takes care of customer specific logic when filing section data is requested
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument CustomFilingComposerDataGet(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {

            switch (TaxxorClientId)
            {
                case "philips":
                    return PhilipsFilingComposerDataGet(xmlSection, projectVars, xmlSectionFolderPathOs);

                default:
                    return xmlSection;
            }
        }

        /// <summary>
        /// Routine that takes care of customer specific logic when filing section data is stored
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFilePathOs"></param>
        /// <returns></returns>
        public static XmlDocument CustomFilingComposerDataSave(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFilePathOs)
        {

            switch (TaxxorClientId)
            {
                case "philips":
                    return PhilipsFilingComposerDataSave(xmlSection, projectVars, xmlSectionFilePathOs);

                default:
                    return xmlSection;
            }
        }

        /// <summary>
        /// Post processing logic when a section is cloned for translation
        /// </summary>
        /// <param name="nodeArticle"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlNode CustomFPostProcessLangClone(XmlNode nodeArticle, ProjectVariables projectVars)
        {
            switch (TaxxorClientId)
            {
                case "philips":
                    return PhilipsPostProcessLangClone(nodeArticle, projectVars);

                default:
                    return nodeArticle;
            }
        }

    }
}