using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{

	/// <summary>
	/// Logic for dealing with CMS (section) metadata
	/// </summary>
	public abstract partial class ProjectLogic : Framework
	{


		/// <summary>
		/// Updates the in-memory CMS Metadata XML object by retrieving it from the Project Data Store or by using the XmlDocument passed
		/// </summary>
		/// <param name="projectId">When passed, updates only the metadata structure of a specific project in the structure</param>
		/// <param name="xmlNewContentMetadata">Optionally pass a new cms content metadata XML object</param>
		/// <returns></returns>
		public static async Task UpdateCmsMetadata(string? projectId = null, XmlDocument? xmlNewCmsContentMetadata = null)
		{
			// Execute the update routine
			var updateResult = await _UpdateCmsMetadata(projectId, xmlNewCmsContentMetadata);

			// Handle result
			if (!updateResult.Success)
			{
				Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
				Console.WriteLine(updateResult.Message);
				Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
			}
		}

		/// <summary>
		/// Update the CMS Metadata object in the Project Data Store and then use that information to update the local XML object as well
		/// </summary>
		/// <param name="projectId"></param>
		/// <param name="xmlNewCmsContentMetadata"></param>
		/// <returns></returns>
		public static async Task UpdateCmsMetadataRemoteAndLocal(string? projectId = null)
		{
			try
			{
				var updateResult = await DocumentStoreService.FilingData.CompileCmsMetadata(projectId);
				if (updateResult.Success)
				{
					try
					{
						await UpdateCmsMetadata(projectId, updateResult.XmlPayload);
						appLogger.LogInformation("Successfully updated the local and remote version of the XML CMS Metadata object");
					}
					catch (Exception ex)
					{
						appLogger.LogError($"ERROR: Unable to update the local XML CMS Metadata object. error: {ex}");
					}
				}
				else
				{
					appLogger.LogError($"ERROR: Unable to update the remote XML CMS Metadata object on the Project Data Store. Message: {updateResult.Message}");
				}
			}
			catch (Exception ex)
			{
				appLogger.LogError($"ERROR: Unable to update the remote XML CMS Metadata object on the Project Data Store. error: {ex}");
			}
		}

		/// <summary>
		/// Retrieves the CMS Metadata information using the local XML object or by requesting it from the Project Data Store
		/// </summary>
		/// <param name="projectId"></param>
		/// <returns></returns>
		public static async Task<TaxxorReturnMessage> RetrieveCmsMetadata(string projectId)
		{

			//
			// => Test if we have the contents in the local version - if so then return a copy
			//
			var nodeProjectMetadata = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
			if (nodeProjectMetadata != null)
			{
				return new TaxxorReturnMessage(true, "Successfully retrieved CMS metadata", RenderXmlContentToReturn(nodeProjectMetadata), "Used local version");
			}
			else
			{
				//
				// => Retrieve the CMS Metadata object from the server
				//
				// Execute the update routine
				var updateResult = await _UpdateCmsMetadata(projectId);

				// Handle result
				if (updateResult.Success)
				{
					nodeProjectMetadata = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
					if (nodeProjectMetadata != null)
					{
						return new TaxxorReturnMessage(true, "Successfully retrieved CMS metadata", RenderXmlContentToReturn(nodeProjectMetadata), "Used updated local version from the server");
					}
					else
					{
						return new TaxxorReturnMessage(false, $"Unable to find project with ID '{projectId}' locally or from a fresh set retrieved from the Project Data Store");
					}
				}
				else
				{
					return updateResult;
				}
			}


			XmlDocument RenderXmlContentToReturn(XmlNode nodeContent)
			{
				// Retrieve the attributes set on the project node
				var xmlDoc = nodeContent.OwnerDocument;

				var xmlDocToReturn = new XmlDocument();
				var nodeProjectToReturn = xmlDocToReturn.CreateElement("projects");
				nodeProjectToReturn.SetAttribute("succes-count", xmlDoc.DocumentElement?.GetAttribute("succes-count") ?? "");
				nodeProjectToReturn.SetAttribute("failure-count", xmlDoc.DocumentElement?.GetAttribute("failure-count") ?? "");

				nodeProjectToReturn.AppendChild(xmlDocToReturn.ImportNode(nodeContent, true));

				xmlDocToReturn.AppendChild(nodeProjectToReturn);

				return xmlDocToReturn;
			}

		}


		/// <summary>
		/// Updates the in-memory CMS Metadata XML object by retrieving it from the Project Data Store
		/// </summary>
		/// <param name="projectId">When passed, updates only the metadata structure of a specific project in the structure</param>
		/// <param name="xmlNewContentMetadata">Optionally pass a new cms content metadata XML object</param>
		/// <returns></returns>
		public static async Task<TaxxorReturnMessage> _UpdateCmsMetadata(string? projectId = null, XmlDocument? xmlNewCmsContentMetadata = null)
		{
			var debugRoutine = (siteType == "local" || siteType == "prev");

			try
			{
				// Render the CMS Content metadata
				if (!MetadataBeingUpdated)
				{
					MetadataBeingUpdated = true;

					//
					// => Retrieve the metadata cache of all the files used in this project
					//
					XmlDocument xmlUpdatedCmsContentMetadata = new XmlDocument();
					if (xmlNewCmsContentMetadata == null || xmlNewCmsContentMetadata.DocumentElement == null)
					{
						var contentMetadataRequestResult = await DocumentStoreService.FilingData.LoadContentMetadata(projectId ?? "all");
						if (!contentMetadataRequestResult.Success)
						{
							return contentMetadataRequestResult;
						}
						xmlUpdatedCmsContentMetadata.ReplaceContent(contentMetadataRequestResult.XmlPayload);
					}
					else
					{
						xmlUpdatedCmsContentMetadata.ReplaceContent(xmlNewCmsContentMetadata);
					}

					// Console.WriteLine(TruncateString(xmlUpdatedCmsContentMetadata.OuterXml, 1000));

					var errorCount = GetAttribute(xmlUpdatedCmsContentMetadata.DocumentElement, "failure-count") ?? "";
					if (errorCount == "0")
					{

						if (projectId == null || XmlCmsContentMetadata == null || XmlCmsContentMetadata.DocumentElement == null)
						{
							XmlCmsContentMetadata.ReplaceContent(xmlUpdatedCmsContentMetadata);
						}
						else
						{
							// Inject the retrieved cms_project node into the overall XML structure
							var nodeUpdatedProject = xmlUpdatedCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
							if (nodeUpdatedProject != null)
							{
								var nodeUpdatedProjectImported = XmlCmsContentMetadata.ImportNode(nodeUpdatedProject, true);
								var nodeProjectToReplace = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectId}']");
								if (nodeProjectToReplace != null)
								{
									// Replace
									appLogger.LogDebug("Project replaced in XmlCmsContentMetadata");
									ReplaceXmlNode(nodeProjectToReplace, nodeUpdatedProjectImported);
								}
								else
								{
									// Append
									appLogger.LogDebug("Project appended to XmlCmsContentMetadata");
									XmlCmsContentMetadata.DocumentElement.AppendChild(nodeUpdatedProjectImported);
								}
							}
							else
							{
								return new TaxxorReturnMessage(false, "Unable to update XmlCmsContentMetadata for a single project because we could not find a node to import");
							}
						}

						MetadataBeingUpdated = false;

						// Store the content in the log directory so that we can inspect it
						if (debugRoutine) await XmlCmsContentMetadata.SaveAsync($"{logRootPathOs}/_cms-content-metadata.xml");
					}
					else
					{
						MetadataBeingUpdated = false;

						return new TaxxorReturnMessage(false, $"FAILED XmlCmsContentMetadata (errorCount: {errorCount})");
					}

				}
				else
				{
					return new TaxxorReturnMessage(true, "Unable to update XmlCmsContentMetadata because another process is updating it");
				}

				// Return a success message
				return new TaxxorReturnMessage(true, "Successfully updated XmlCmsContentMetadata");
			}
			catch (Exception ex)
			{
				MetadataBeingUpdated = false;

				return new TaxxorReturnMessage(false, $"FAILED to update local XmlCmsContentMetadata (error: {ex})");
			}
		}

	}
}