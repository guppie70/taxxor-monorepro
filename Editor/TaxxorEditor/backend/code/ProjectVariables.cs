using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// The instance of this class will contain the variables that are unique for this project and the request
        /// </summary>
        public class ProjectVariables : ProjectPropertiesBase
        {

            /// <summary>
            /// The ID of the current tenant
            /// </summary>
            public string tenantId = "default";

            public string projectRootPath = string.Empty;
            public string projectRootPathOs = string.Empty;

            /// <summary>
            /// Filing composer "editor"
            /// ("//editors/editor/@id")
            /// </summary>
            public string? editorId = null;

            /// <summary>
            /// ID of the type of content that the filing composer is editing ("//content_types/content_management/type/@id") - ctype parameter in querystring
            /// "regular" is currently the only implemented value
            /// </summary>
            public string? editorContentType = null;

            /// <summary>
            /// Version of the edited document ("latest" or a number) - vid parameter in querystring
            /// </summary>
            public string? versionId = null;

            /// <summary>
            /// Document section id - did parameter in querystring
            /// </summary>
            public string? did = null;

            // For content editor in composer
            public string? idFirstEditablePage = null;

            public string? reportingPeriod;

            /// <summary>
            /// Type of output channel - octype parameter in querystring
            /// "pdf", "xbrl" and "website" are implemented values
            /// </summary>
            public string? outputChannelType = null;

            /// <summary>
            /// Output channel variant - ocvariantid parameter in querystring
            /// Each language variant of an output channel is implemented as a variant
            /// </summary>
            public string? outputChannelVariantId = null;

            /// <summary>
            /// Language used in the outputchannel - oclang parameter in querystring
            /// </summary>
            public string? outputChannelVariantLanguage = null;

            /// <summary>
            /// The default (master) language that will be used in multi lingual environments
            /// </summary>
            public string? outputChannelDefaultLanguage = null;

            public Dictionary<string, MetaData>? cmsMetaData = null;

            /// <summary>
            /// Session token as stored by session system
            /// </summary>
            public string? token = "";

            public bool isMiddlewareCreated;

            public AppUserTaxxor? currentUser;

            public bool isInternalServicePage = false;

            /// <summary>
            /// Contains the base URL of the static assets of Taxxor DM
            /// </summary>
            public string? uriStaticAssets = null;

            // Taxxor guids
            // a) client, entity group, legal entity
            public string? guidClient
            {
                get
                {
                    if (!_guidLegalEntityRetrieved) _retrieveGuidLegalEntity();
                    if (_guidClient == null && _guidLegalEntity != null)
                    {
                        _guidClient = xmlApplicationConfiguration.SelectSingleNode($"/configuration/taxxor/clients/client[entity_groups/entity_group/entity/@guidLegalEntity='{_guidLegalEntity ?? "thiswillnevermatch"}']")?.GetAttribute("id");
                    }
                    return _guidClient;
                }

                set
                {
                    _guidClient = value;
                }
            }
            private string? _guidClient = null;


            public string? guidEntityGroup
            {
                get
                {
                    if (!_guidLegalEntityRetrieved) _retrieveGuidLegalEntity();
                    if (_guidEntityGroup == null && _guidLegalEntity != null)
                    {
                        _guidEntityGroup = xmlApplicationConfiguration.SelectSingleNode($"/configuration/taxxor/clients/client/entity_groups/entity_group[entity/@guidLegalEntity='{_guidLegalEntity ?? "thiswillnevermatch"}']")?.GetAttribute("id");
                    }
                    return _guidEntityGroup;
                }

                set
                {
                    _guidEntityGroup = value;
                }
            }
            private string? _guidEntityGroup = null;


            public string? guidLegalEntity
            {
                get
                {
                    _guidLegalEntityRetrieved = true;
                    _retrieveGuidLegalEntity();
                    return _guidLegalEntity;
                }

                set
                {
                    _guidLegalEntity = value;
                }
            }
            private bool _guidLegalEntityRetrieved = false;
            private string? _guidLegalEntity = null;

            /// <summary>
            /// Helper method to retrieve the guid of the legal entity
            /// </summary>
            private void _retrieveGuidLegalEntity()
            {
                if (this._guidLegalEntity == null && !string.IsNullOrEmpty(this.projectId))
                {
                    var nodeProject = ProjectLogic.xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id={GenerateEscapedXPathString(this.projectId)}]");
                    //a) legal entity guids
                    this._guidLegalEntity = nodeProject?.SelectSingleNode("system/entities/entity")?.GetAttribute("guidLegalEntity");
                }
            }





            // b) regulator, regulation, report
            public string? guidRegulator = null;
            public string? guidRegulation = null;
            public string? guidRegulationReport = null;
            // c) taxonomies
            public string? guidTaxonomy = null;
            public string? guidTaxonomyVersion = null;

            // d) link between (a) and (b)
            public string? guidLegalEntityRequirement = null;
            public string? guidLegalEntityRequirementFiling = null;

            public XmlDocument xmlApplicationConfiguration = new();

            /// <summary>
            /// The user identifier from HTTP header.
            /// </summary>
            public string? userIdFromHeader = null;

            /// <summary>
            /// Indicates if this request was used to setup a new session
            /// </summary>
            public bool sessionCreated = false;

            /// <summary>
            /// For caching permissions and generated hierarchies
            /// </summary>
            public RbacCache? rbacCache;

            /// <summary>
            /// Contains the status of the project (open or closed)
            /// </summary>
            public string? projectStatus = null;


            public ProjectVariables(bool lightWeight = false)
            {
                this.isMiddlewareCreated = false;
                this.cmsMetaData = new Dictionary<string, MetaData>();

                // Create the user object instance
                if (!lightWeight) this.currentUser = new AppUserTaxxor();
            }

            public ProjectVariables(string pageId)
            {
                this.isMiddlewareCreated = false;
                this.cmsMetaData = new Dictionary<string, MetaData>();

                // Create the user object instance
                this.currentUser = new AppUserTaxxor();

                switch (pageId)
                {
                    case "filingcomposer-redirect":
                    case "cms_content-editor":
                    case "cms_preview-pdfdocument":
                    case "cms_hierarchy-manager":
                    case "cms_version-manager":
                    case "cms_auditor-view":
                        try
                        {
                            this.xmlApplicationConfiguration.ReplaceContent(Framework.xmlApplicationConfiguration);
                        }
                        catch (Exception ex)
                        {
                            HandleError("Unable to find all configuration information", $"error: {ex}, pageId: {pageId}");
                        }

                        break;

                    default:
                        break;
                }
            }

            /// <summary>
            /// Constructor that pre-fills a ProjectVariables instance with default values based on the project id and outputchannel variant id
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            public ProjectVariables(string projectId, string outputChannelVariantId)
            {
                this.Fill(projectId, outputChannelVariantId);
            }

            /// <summary>
            /// Pre-fills a project variables object with what we can deduct from an ouputchannel variant ID
            /// </summary>
            /// <param name="projectId"></param>
            /// <param name="outputChannelVariantId"></param>
            public void Fill(string projectId, string outputChannelVariantId)
            {
                this.projectId = projectId;
                this.versionId = "latest";
                this.editorContentType = "regular";
                this.reportTypeId = RetrieveReportTypeIdFromProjectId(projectId);
                this.outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectId, outputChannelVariantId);
                this.editorId = RetrieveEditorIdFromProjectId(projectId);
                this.outputChannelVariantId = outputChannelVariantId;
                this.outputChannelVariantLanguage = RetrieveOutputChannelLanguageFromOutputChannelVariantId(projectId, outputChannelVariantId);
            }

            /// <summary>
            /// Pre-fills the project variables object when it consists of only a project- and an Output channel variant ID
            /// </summary>
            public void Fill()
            {
                this.Fill(this.projectId, this.outputChannelVariantId);
            }

            /// <summary>
            /// Dumps the contents of the ProjectVariables to a string for debugging
            /// </summary>
            /// <param name="summary"></param>
            /// <returns></returns>
            public string DumpToString(bool summary = true)
            {

                var sb = new StringBuilder();
                try
                {
                    sb.AppendLine($"- projectId: {this.projectId}");
                    sb.AppendLine($"- reportTypeId: {this.reportTypeId}");

                    sb.AppendLine($"- editorId: {this.editorId}");
                    sb.AppendLine($"- editorContentType: {this.editorContentType}");
                    sb.AppendLine($"- versionId: {this.versionId}");
                    sb.AppendLine($"- did: {this.did}");
                    sb.AppendLine($"- outputChannelType: {this.outputChannelType}");

                    sb.AppendLine($"- outputChannelVariantId: {this.outputChannelVariantId}");
                    sb.AppendLine($"- outputChannelVariantLanguage: {this.outputChannelVariantLanguage}");
                    sb.AppendLine($"- uriStaticAssets: {this.uriStaticAssets}");
                    sb.AppendLine($"- projectStatus: {this.projectStatus}");
                    if (this.cmsMetaData == null)
                    {
                        sb.AppendLine($"- cmsMetaData(.Count): notset");
                    }
                    else
                    {
                        sb.AppendLine($"- cmsMetaData(.Count): {this.cmsMetaData.Count}");
                    }
                    sb.AppendLine($"- currentUser(.Id): {this.currentUser?.Id ?? "notset"}");
                    if (this.rbacCache == null)
                    {
                        sb.AppendLine($"- rbacCache: null");
                    }
                    else
                    {
                        sb.AppendLine($"- rbacCache(.Count): {this.rbacCache.CountHierarchies()}");
                    }



                    if (!summary)
                    {
                        sb.AppendLine($"- projectRootPath: {this.projectRootPath}");
                        sb.AppendLine($"- projectRootPathOs: {this.projectRootPathOs}");

                        sb.AppendLine($"- idFirstEditablePage: {this.idFirstEditablePage}");
                        sb.AppendLine($"- reportingPeriod: {this.reportingPeriod}");


                        sb.AppendLine($"- token: {this.token}");
                        sb.AppendLine($"- isMiddlewareCreated: {this.isMiddlewareCreated}");

                        sb.AppendLine($"- isInternalServicePage: {this.isInternalServicePage}");

                        sb.AppendLine($"- guidClient: {this.guidClient}");
                        sb.AppendLine($"- guidEntityGroup: {this.guidEntityGroup}");
                        sb.AppendLine($"- guidLegalEntity: {this.guidLegalEntity}");
                        sb.AppendLine($"- guidRegulator: {this.guidRegulator}");
                        sb.AppendLine($"- guidRegulation: {this.guidRegulation}");
                        sb.AppendLine($"- guidTaxonomy: {this.guidTaxonomy}");
                        sb.AppendLine($"- guidTaxonomyVersion: {this.guidTaxonomyVersion}");
                        sb.AppendLine($"- guidLegalEntityRequirement: {this.guidLegalEntityRequirement}");
                        sb.AppendLine($"- guidLegalEntityRequirementFiling: {this.guidLegalEntityRequirementFiling}");

                        sb.AppendLine($"- xmlApplicationConfiguration (root element name): {this.xmlApplicationConfiguration?.DocumentElement?.LocalName ?? "null"}");
                        sb.AppendLine($"- userIdFromHeader: {this.userIdFromHeader}");
                        sb.AppendLine($"- sessionCreated: {this.sessionCreated}");
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, $"Unable to extract all information from the project variables object");
                }


                return sb.ToString();
            }

            /// <summary>
            /// Converts a project variables object to a dictionary we can use to post to another Taxxor component
            /// </summary>
            /// <returns></returns>
            public Dictionary<string, string> RenderPostDictionary()
            {
                var dataToPost = new Dictionary<string, string>
                {
                    { "pid", this.projectId },
                    { "vid", this.versionId },
                    // Data types supported are "text", "config" - but since we need the content for the editor we will fix it to "text"
                    { "type", "text" },
                    { "did", this.did ?? "" },
                    { "ctype", this.editorContentType },
                    { "rtype", this.reportTypeId },
                    { "octype", this.outputChannelType },
                    { "ocvariantid", this.outputChannelVariantId },
                    { "oclang", this.outputChannelVariantLanguage }
                };

                return dataToPost;
            }

        }

        /// <summary>
        /// Retrieves the request variables object
        /// </summary>
        /// <returns>The request variables.</returns>
        /// <param name="context">Context.</param>
        /// <param name="logError"></param>
        public static ProjectVariables RetrieveProjectVariables(HttpContext context, bool logError = true)
        {
            if (context == null)
            {
                if (siteType == "local" || siteType == "dev")
                {
                    if (logError) appLogger.LogWarning($"Could not retrieve project variables because context was null. stack-trace: {GetStackTrace()}");
                }
                return new ProjectVariables();
            }
            else
            {
                return (Taxxor.Project.ProjectLogic.ProjectVariables)context.Items[keyProjectVariables];
            }
        }

        /// <summary>
        /// Routine that tests if a project variables object exists in the context
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static bool ProjectVariablesExistInContext(HttpContext context)
        {
            var exists = false;
            foreach (var pair in context.Items)
            {
                if ((string)pair.Key == keyProjectVariables)
                {
                    exists = true;
                    break;
                }
            }
            return exists;
        }



    }
}