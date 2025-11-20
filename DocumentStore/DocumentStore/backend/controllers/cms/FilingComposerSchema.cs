using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    /// <summary>
    /// Utilities involved in loading XML data into the editor
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
	{

		//logic that is referred to in <<cms_root>>/utilities/load_schema.aspx
		/// <summary>
		/// Routine which modifies the XSD content which is fed to the Xopus editor
		/// </summary>
        public static async Task RetrieveFilingComposerSchema(HttpRequest request, HttpResponse response, RouteData routeData)
		{
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Retrieve posted variables
            var projectId = context.Request.RetrievePostedValue("pid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var versionId = context.Request.RetrievePostedValue("vid", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var dataType = context.Request.RetrievePostedValue("type", true, ReturnTypeEnum.Xml);
            var id = context.Request.RetrievePostedValue("did", true, ReturnTypeEnum.Xml);
            projectVars.editorContentType = context.Request.RetrievePostedValue("ctype", true, ReturnTypeEnum.Xml);
            projectVars.reportTypeId = context.Request.RetrievePostedValue("rtype", true, ReturnTypeEnum.Xml);

            var baseDebugInfo = $"projectId: '{projectId}', versionId: '{versionId}', dataType: '{dataType}', id (section): '{id}', projectVars.editorContentType: '{projectVars.editorContentType}', projectVars.reportTypeId: '{projectVars.reportTypeId}'";


            var xPath = $"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(projectId)}]/content_types/content_management/type[@id={GenerateEscapedXPathString(projectVars.editorContentType)}]/xsd";
            var mainXsdFilePathOs = CalculateFullPathOs(xPath);
			var mainXsdFolderPathOs = Path.GetDirectoryName(mainXsdFilePathOs);
            if (!File.Exists(mainXsdFilePathOs)) HandleError(ReturnTypeEnum.Xml, "Could not load main XSD file", $"mainXsdFilePathOs: {mainXsdFilePathOs}, xPath: {xPath}, stack-trace: {GetStackTrace()}", 424);

			XmlDocument xsdMain = new XmlDocument();
			XmlNamespaceManager nsManagerXsd = new XmlNamespaceManager(xsdMain.NameTable);
			// TO DO: store namespace in constant string
			nsManagerXsd.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
			xsdMain.Load(mainXsdFilePathOs);


			//test if there are any sub xsd files to include
			var nodeListIncludes = xsdMain.SelectNodes("/xs:schema/xs:include", nsManagerXsd);
			foreach (XmlNode nodeInclude in nodeListIncludes)
			{
				//import the nested schema
				XmlDocument xsdSub = new XmlDocument();
				XmlNamespaceManager nsManagerSubXsd = new XmlNamespaceManager(xsdSub.NameTable);
                // TO DO: store namespace in constant string
                nsManagerSubXsd.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");

				//load the nested schema
				var subXsdFilePathOs = mainXsdFolderPathOs + "/" + GetAttribute(nodeInclude, "schemaLocation");
                if (!File.Exists(subXsdFilePathOs)) HandleError(ReturnTypeEnum.Xml ,"Could not load sub XSD file", $"subXsdFilePathOs: {subXsdFilePathOs}, stack-trace: {GetStackTrace()}", 424);
				xsdSub.Load(subXsdFilePathOs);

				//import the data of the sub xsd file into the main xsd file
				var nodeListSubXsdDefinitions = xsdSub.SelectNodes("/xs:schema/*", nsManagerSubXsd);
				foreach (XmlNode nodeDefinition in nodeListSubXsdDefinitions)
				{
					var nodeImported = xsdMain.ImportNode(nodeDefinition, true);
					xsdMain.DocumentElement.AppendChild(nodeImported);
				}

				RemoveXmlNode(nodeInclude);
			}

			//report type specific stuff
            switch (projectVars.reportTypeId)
			{
				case "lextra-website":
				case "lextra-ourbrand-website:":

					break;

				default:
					//to make the editor capable of per section editing
					xsdMain = XsdDataCompileGeneric(xsdMain, nsManagerXsd);
					break;
			}

            // Stick the file content in the message field of the success xml and the file path into the debuginfo node
            await response.OK(GenerateSuccessXml(HttpUtility.HtmlEncode(xsdMain.OuterXml), baseDebugInfo), ReturnTypeEnum.Xml, true);
		}

		/// <summary>
		/// Modifies the XSD document to allow "per section" editing
		/// </summary>
		/// <param name="xsdDocument"></param>
		/// <param name="nsManagerXsd"></param>
		/// <returns></returns>
		public static XmlDocument XsdDataCompileGeneric(XmlDocument xsdDocument, XmlNamespaceManager nsManagerXsd)
		{
			//to make the editor capable of per section editing
			var nodeListPresentationElements = xsdDocument.SelectNodes("/xs:schema/xs:element[@name='presentation' or @name='Presentation']/xs:complexType/xs:sequence/xs:element", nsManagerXsd);
			foreach (XmlNode nodePresentationElement in nodeListPresentationElements)
			{
				SetAttribute(nodePresentationElement, "minOccurs", "0");
			}

			//fix a problem with xs:any -> a hack that we should remove in a later stage
			var nodelistAny = xsdDocument.SelectNodes("/xs:schema//xs:any", nsManagerXsd);
			foreach (XmlNode nodeAny in nodelistAny)
			{
				SetAttribute(nodeAny, "minOccurs", "0");
			}


			return xsdDocument;

		}


	}
}