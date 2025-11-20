using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Retrieves the latest version of the content that is being used in the editor
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task RetrieveContentVersion(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var scope = request.RetrievePostedValue("scope", RegexEnum.None, true, reqVars.returnType, "latest");
            var includeDateStampString = request.RetrievePostedValue("includedatestamp", RegexEnum.Boolean, false, reqVars.returnType, "false");
            var includeDateStamp = (includeDateStampString == "true");

            try
            {
                var tagsRaw = "";
                var tags = "";

                //
                // => Retrieve the raw output from the version control repository
                //
                switch (scope)
                {
                    case "latest":
                        // Retrieve the content version by retrieving the latest GIT tag
                        tagsRaw = GitCommand("git tag --sort=-refname", projectVars.cmsDataRootBasePathOs).Trim();

                        break;

                    case "all":
                        tagsRaw = GitCommand("git tag --sort=-refname", projectVars.cmsDataRootBasePathOs).Trim();

                        break;
                }


                //
                // => Create an array of version tags
                //
                string[] allTags = tagsRaw.Split('\n');

                if (allTags.Length == 0)
                {
                    tags = "not set";
                }
                else
                {
                    //
                    // => Create a sorted list
                    //
                    var ver = new List<Version>();
                    foreach (string versionLabel in allTags)
                    {
                        if (versionLabel.StartsWith('v'))
                        {
                            try
                            {
                                ver.Add(new Version(versionLabel.Replace("v", "")));
                            }
                            catch (Exception ex)
                            {
                                appLogger.LogWarning(ex, $"Unable to add version {versionLabel}, error: {ex}");
                            }
                        }
                        else
                        {
                            appLogger.LogWarning($"Unexpected version label {versionLabel} detected");
                        }
                    }
                    ver.Sort();
                    ver.Reverse();

                    //
                    // => Determine what to return
                    //
                    switch (scope)
                    {
                        case "latest":
                            // Latest tag is the first item in the list
                            tags = $"v{ver.First()}";

                            // git tag -l --format='%(contents)' v2.0
                            // git tag -l --format='%(creatordate:raw)' v2.0
                            if (includeDateStamp)
                            {
                                var tagCreationDateEpoch = GitCommand($"git tag -l --format='%(creatordate:raw)' {tags}", projectVars.cmsDataRootBasePathOs).Trim();
                                tags += $"|{tagCreationDateEpoch}";
                            }


                            break;

                        case "all":
                            // Prepend a 'v' before each element
                            var versionsNormalized = new List<string>();
                            foreach (Version versionLabel in ver)
                            {
                                versionsNormalized.Add($"v{versionLabel.ToString()}");
                            }

                            tags = string.Join(",", versionsNormalized.ToArray());
                            break;
                    }
                }

                // Generate a standard success XmlDocument
                var xmlSuccess = GenerateSuccessXml("Successfully retrieved tag", $"projectVars.cmsDataRootBasePathOs: {projectVars.cmsDataRootBasePathOs}", tags);

                // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                await context.Response.OK(xmlSuccess, reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                // Generate a standard error XmlDocument
                var xmlError = GenerateErrorXml("There was an error retrieving the content version", $"error: {ex}, projectVars.cmsDataRootBasePathOs: {projectVars.cmsDataRootBasePathOs}, stack-trace: {GetStackTrace()}");

                // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                await context.Response.Error(xmlError, reqVars.returnType, true);
            }
        }

        /// <summary>
        /// Commits a revision of the data content assets in the revision system
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task CommitFilingContentRevision(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var debugRoutine = (siteType == "local" || siteType == "dev");

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request and project variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve posted values
            var linkNames = request.RetrievePostedValue("linknames", RegexEnum.Default, true, ReturnTypeEnum.Xml);
            var sectionIds = request.RetrievePostedValue("sectionids", RegexEnum.None, true, ReturnTypeEnum.Xml);
            var crudAbbreviation = request.RetrievePostedValue("crud", RegexEnum.None, false, ReturnTypeEnum.Xml, "u");
            var sourceApplication = request.RetrievePostedValue("application", RegexEnum.Default.Value, false, ReturnTypeEnum.Xml, "");


            try
            {
                // HandleError("Stopped on purpose");

                // Construct the commit message
                XmlDocument xmlCommitMessage = RetrieveTemplate("git-data-save_message");
                var nodeCrud = xmlCommitMessage.SelectSingleNode("/root/crud");
                nodeCrud.InnerText = crudAbbreviation; // Update
                if (!string.IsNullOrEmpty(sourceApplication))
                {
                    SetAttribute(nodeCrud, "application", sourceApplication);
                }
                xmlCommitMessage.SelectSingleNode("/root/linkname").InnerText = linkNames; // Linkname
                if (!string.IsNullOrEmpty(projectVars.outputChannelVariantLanguage)) xmlCommitMessage.SelectSingleNode("/root/linkname").SetAttribute("lang", projectVars.outputChannelVariantLanguage);
                xmlCommitMessage.SelectSingleNode("/root/id").InnerText = sectionIds; // Page ID
                var message = xmlCommitMessage.DocumentElement.InnerXml;

                // Commit the result in the GIT repository
                CommitFilingData(message, ReturnTypeEnum.Xml, true);

                // Generate a standard success XmlDocument
                var xmlSuccess = GenerateSuccessXml("Successfully committed filing content revision", $"projectVars.cmsDataRootBasePathOs: {projectVars.cmsDataRootBasePathOs}");

                // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                await context.Response.OK(xmlSuccess, reqVars.returnType, true);
            }
            catch (Exception ex)
            {
                if (ex.ToString().ToLower().Contains("could not commit to repository as there is nothing to commit"))
                {
                    // Generate a standard success XmlDocument
                    var xmlSuccess = GenerateSuccessXml("There were no changes that needed to be committed", $"projectVars.cmsDataRootBasePathOs: {projectVars.cmsDataRootBasePathOs}");

                    // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                    await context.Response.OK(xmlSuccess, reqVars.returnType, true);
                }
                else
                {
                    // Generate a standard error XmlDocument
                    var xmlError = GenerateErrorXml("There was an error committing the filing content revision", $"error: {ex}, projectVars.cmsDataRootBasePathOs: {projectVars.cmsDataRootBasePathOs}, stack-trace: {GetStackTrace()}");

                    // Render an error response - the content that will be returned to the client depends on the value of reqVars.returnType -> which is dynamically determined using content negotiation
                    await context.Response.Error(xmlError, reqVars.returnType, true);
                }
            }

        }

    }

}