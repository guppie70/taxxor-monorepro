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

            //editor id
            public string? editorId = null;
            //editor type (for xsd, xsl, etc in the editor)

            // "regular" most commonly used value
            public string? editorContentType = null;

            public string? cmsDataRootPath = null;
            public string? cmsDataRootPathOs = null;
            public string? cmsDataRootBasePathOs = null; //the data root path without the version folder

            public string? cmsContentRootPathOs = null;
            public string? cmsTemplatesRootPathOs = null;

            public string? projectRootPath = null;
            public string? projectRootPathOs = null;

            //user data storage folder
            public string? cmsUserDataRootPathOs = null;

            public string? versionId = null;

            /// <summary>
            /// Document section id
            /// </summary>
            public string? did = null;
            public string? reportingPeriod;
            public string? outputChannelType = null;
            public string? outputChannelVariantId = null;
            public string? outputChannelVariantLanguage = null;

            /// <summary>
            /// The default (master) language that will be used in multi lingual environments
            /// </summary>
            public string? outputChannelDefaultLanguage = null;

            public bool isMiddlewareCreated;

            public AppUserTaxxor currentUser;

            public bool isInternalServicePage = false;

            public Dictionary<string, MetaData> cmsMetaData;

            // Taxxor guids
            // a) client, entity group, legal entity
            public string? guidClient = null;
            public string? guidEntityGroup = null;
            public string? guidLegalEntity = null;
            // b) regulator, regulation, report
            public string? guidRegulator = null;
            public string? guidRegulation = null;
            public string? guidRegulationReport = null;
            // c) taxonomies
            public string? guidTaxonomy = null;
            public string? guidTaxonomyVersion = null;
            public string? guidTaxonomyVersionEntryPoint = null;

            // d) link between (a) and (b)
            public string? guidLegalEntityRequirement = null;
            public string? guidLegalEntityRequirementFiling = null;

            // e) calendar
            public string? guidCalendarEvent = null;

            public string? token = null;

            /// <summary>
            /// For caching permissions and generated hierarchies
            /// </summary>
            public RbacCache? rbacCache;

            public ProjectVariables()
            {
                this.isMiddlewareCreated = false;

                this.cmsMetaData = new Dictionary<string, MetaData>();

                // Create the user instance
                this.currentUser = new AppUserTaxxor();
            }

            public ProjectVariables Clone()
            {
                var clone = new ProjectVariables
                {
                    tenantId = this.tenantId,
                    editorId = this.editorId,
                    editorContentType = this.editorContentType,
                    cmsDataRootPath = this.cmsDataRootPath,
                    cmsDataRootPathOs = this.cmsDataRootPathOs,
                    cmsDataRootBasePathOs = this.cmsDataRootBasePathOs,
                    cmsContentRootPathOs = this.cmsContentRootPathOs,
                    cmsTemplatesRootPathOs = this.cmsTemplatesRootPathOs,
                    projectRootPath = this.projectRootPath,
                    projectRootPathOs = this.projectRootPathOs,
                    cmsUserDataRootPathOs = this.cmsUserDataRootPathOs,
                    versionId = this.versionId,
                    did = this.did,
                    reportingPeriod = this.reportingPeriod,
                    outputChannelType = this.outputChannelType,
                    outputChannelVariantId = this.outputChannelVariantId,
                    outputChannelVariantLanguage = this.outputChannelVariantLanguage,
                    outputChannelDefaultLanguage = this.outputChannelDefaultLanguage,
                    isMiddlewareCreated = this.isMiddlewareCreated,
                    isInternalServicePage = this.isInternalServicePage,
                    guidClient = this.guidClient,
                    guidEntityGroup = this.guidEntityGroup,
                    guidLegalEntity = this.guidLegalEntity,
                    guidRegulator = this.guidRegulator,
                    guidRegulation = this.guidRegulation,
                    guidRegulationReport = this.guidRegulationReport,
                    guidTaxonomy = this.guidTaxonomy,
                    guidTaxonomyVersion = this.guidTaxonomyVersion,
                    guidTaxonomyVersionEntryPoint = this.guidTaxonomyVersionEntryPoint,
                    guidLegalEntityRequirement = this.guidLegalEntityRequirement,
                    guidLegalEntityRequirementFiling = this.guidLegalEntityRequirementFiling,
                    guidCalendarEvent = this.guidCalendarEvent,
                    token = this.token,
                    projectId = this.projectId,
                    reportTypeId = this.reportTypeId
                };

                // Deep copy of complex objects
                clone.currentUser.Id = this.currentUser.Id;
                clone.currentUser.FirstName = this.currentUser.FirstName;
                clone.currentUser.LastName = this.currentUser.LastName;
                clone.currentUser.Email = this.currentUser.Email;
                clone.currentUser.DisplayName = this.currentUser.DisplayName;
                clone.currentUser.IsAuthenticated = this.currentUser.IsAuthenticated;

                return clone;
            }

            public string DumpToString(bool summary = true)
            {

                var sb = new StringBuilder();
                sb.AppendLine($"- projectId: {this.projectId}");
                sb.AppendLine($"- reportTypeId: {this.reportTypeId}");

                sb.AppendLine($"- editorId: {this.editorId}");
                sb.AppendLine($"- editorContentType: {this.editorContentType}");
                sb.AppendLine($"- versionId: {this.versionId}");
                sb.AppendLine($"- did: {this.did}");
                sb.AppendLine($"- outputChannelType: {this.outputChannelType}");

                sb.AppendLine($"- outputChannelVariantId: {this.outputChannelVariantId}");
                sb.AppendLine($"- outputChannelVariantLanguage: {this.outputChannelVariantLanguage}");

                sb.AppendLine($"- cmsMetaData(.Count): {this.cmsMetaData.Count}");
                sb.AppendLine($"- currentUser(.Id): {this.currentUser.Id}");
                if (this.rbacCache != null) sb.AppendLine($"- rbacCache(.Count): {this.rbacCache.CountHierarchies()}");


                if (!summary)
                {
                    sb.AppendLine($"- projectRootPath: {this.projectRootPath}");
                    sb.AppendLine($"- projectRootPathOs: {this.projectRootPathOs}");

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
                    sb.AppendLine($"- guidTaxonomyVersionEntryPoint: {this.guidTaxonomyVersionEntryPoint}");
                    sb.AppendLine($"- guidLegalEntityRequirement: {this.guidLegalEntityRequirement}");
                    sb.AppendLine($"- guidLegalEntityRequirementFiling: {this.guidLegalEntityRequirementFiling}");

                    sb.AppendLine($"- guidCalendarEvent: {this.guidCalendarEvent}");
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Retrieves the request variables object
        /// </summary>
        /// <returns>The request variables.</returns>
        /// <param name="context">Context.</param>
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




    }
}