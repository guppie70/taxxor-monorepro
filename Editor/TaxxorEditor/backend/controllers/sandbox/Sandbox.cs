using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using CustomerCodeInterface;
using DocumentStore.Protos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;
using static Taxxor.ConnectedServices.DocumentStoreService;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        public async static Task SandboxChangeTaxesPaidMappingCluster(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar20");
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            var factIds = "a31516fc-0440-469a-903b-154da24c8aaa";
            factIds = "all";
            var newPeriodEndDate = "20201231";

            var logInfo = new LogInfo();

            var xmlMappingInformation = new XmlDocument();
            xmlMappingInformation.Load($"{dataRootPathOs}/taxespaidmappinginfo.xml");

            var xPathMappingClusters = "/mappingClusters/mappingCluster";
            if (factIds != "all")
            {
                xPathMappingClusters = $"{xPathMappingClusters}[@requestId='{factIds}']";
            }

            var sdeCounter = 0;
            var nodeListMappingClusters = xmlMappingInformation.SelectNodes(xPathMappingClusters);
            foreach (XmlNode nodeMappingCluster in nodeListMappingClusters)
            {
                sdeCounter++;
                var factId = nodeMappingCluster.GetAttribute("requestId");
                if (string.IsNullOrEmpty(factId))
                {
                    logInfo.AddErrorLogEntry("Unable to find fact ID");
                    continue;
                }
                else
                {
                    // logInfo.AddInformationLogEntry($"Processing SDE with ID: {factId}");
                }

                // Console.WriteLine($"** Original Cluster **");
                // Console.WriteLine(PrettyPrintXml(nodeMappingCluster));
                // Console.WriteLine($"**********************");


                var xmlUpdatedMappingCluster = new XmlDocument();


                if (nodeMappingCluster != null)
                {
                    string? existingPeriod = null;
                    string? newPeriod = null;
                    bool isFixedDate = false;

                    xmlUpdatedMappingCluster.LoadXml(nodeMappingCluster.OuterXml);

                    var nodeInternalEntry = _getInternalEntry(nodeMappingCluster, factId);

                    if (nodeInternalEntry != null)
                    {
                        // Console.WriteLine("###### INTERNAL MAPPING ######");
                        // Console.WriteLine(nodeInternalEntry.OuterXml);
                        // Console.WriteLine("##############################");

                        existingPeriod = nodeInternalEntry.GetAttribute("period");

                        var isFixedDateString = nodeInternalEntry.GetAttribute("isAbsolute") ?? "false";
                        isFixedDate = (isFixedDateString == "true");
                    }
                    else
                    {
                        logInfo.AddErrorLogEntry($"Could not find internal mapping entry for {factId}");
                        continue;
                    }

                    if (isFixedDate)
                    {
                        logInfo.AddInformationLogEntry($"Skipping period shift for {factId}, because it uses a fixed date");
                    }
                    else
                    {
                        //
                        // => Test if this is a period that we need to shift
                        //
                        if (!existingPeriod.Contains("20200630"))
                        {
                            logInfo.AddErrorLogEntry($"factId: {factId} with period {existingPeriod} could not be shifted");
                            continue;
                        }


                        var isDuration = existingPeriod.Contains("_");

                        if (!string.IsNullOrEmpty(existingPeriod))
                        {
                            //
                            // => Calculate the new period
                            //

                            if (isDuration)
                            {
                                newPeriod = $"{existingPeriod.SubstringBefore("_")}_{newPeriodEndDate}";
                            }
                            else
                            {
                                newPeriod = newPeriodEndDate;
                            }
                        }
                        else
                        {
                            logInfo.AddWarningLogEntry($"Could not find period for fact-id: {factId}");
                            continue;
                        }

                        Console.WriteLine("***************************");
                        Console.WriteLine($"existingPeriod: {existingPeriod}, newPeriod: {newPeriod} ({sdeCounter}/{nodeListMappingClusters.Count})");
                        Console.WriteLine("***************************");

                        if (newPeriod != existingPeriod)
                        {
                            //
                            // => Update the mapping cluster with the new period in the database
                            //
                            var nodeNewInternalEntry = _getInternalEntry(xmlUpdatedMappingCluster.DocumentElement, factId);
                            if (nodeNewInternalEntry != null)
                            {
                                nodeNewInternalEntry.SetAttribute("period", newPeriod);

                                var currentValueFormatting = nodeNewInternalEntry.GetAttribute("displayOption");
                                if (!string.IsNullOrEmpty(currentValueFormatting))
                                {
                                    if (currentValueFormatting == "Rounded x.xx million text")
                                    {
                                        nodeNewInternalEntry.SetAttribute("displayOption", "Rounded x.xx million without unit");
                                    }
                                }

                                xmlUpdatedMappingCluster.DocumentElement.SetAttribute("projectId", projectVars.projectId);
                                xmlUpdatedMappingCluster.DocumentElement.RemoveAttribute("requestId");

                                var xmlUpdateResult = await Taxxor.ConnectedServices.MappingService.UpdateMappingEntry(xmlUpdatedMappingCluster);
                                if (XmlContainsError(xmlUpdateResult))
                                {
                                    logInfo.AddErrorLogEntry($"Updating factId {factId} to period {newPeriod} failed. Error: {xmlUpdateResult.OuterXml}");
                                }
                                else
                                {
                                    logInfo.AddSuccessLogEntry($"Successfully updated mapping cluster for {factId} to period from {existingPeriod} to {newPeriod}");
                                }
                                // if (debugRoutine)
                                // {
                                //     Console.WriteLine("***************** UPDATE RESULT ********************");
                                //     Console.WriteLine(PrettyPrintXml(xmlUpdateResult));
                                //     Console.WriteLine("*************************************");
                                // }
                            }
                            else
                            {
                                logInfo.AddErrorLogEntry($"Could not find internal entry node for factId: {factId} to update to period {newPeriod}");
                            }
                        }
                        else
                        {
                            logInfo.AddErrorLogEntry($"No need to store the mapping cluster as we did not calculate a new period for this factId: {factId}");
                        }

                    }

                }



            }


            var responseToClient = $@"
                <html>
                    <head>
                        <title>Testing page</title>
                    </head>
                    <body>
                        <h2>Taxes Paid Summary Mapping Cluster Change - {DateTime.Now.ToString()}</h2>
                        <pre>

                        - sdeTotal: {nodeListMappingClusters.Count}
                        {logInfo.LogToString()}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);

        }


        public async static Task SandboxChangeTaxesPaidMappingClusterFlipSign(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar20");
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            var factIds = "03ead6b1-1a56-4730-b48f-3356e60476b7";
            factIds = "all";

            var logInfo = new LogInfo();

            var xmlTaxesPaidOverview = new XmlDocument();
            xmlTaxesPaidOverview.Load($"{dataRootPathOs}/taxespaidoverview.xml");

            // Find the SDE's for which we need to change the flipsign attribute
            var sdeIds = new List<string>();
            var nodeListFacts = xmlTaxesPaidOverview.SelectNodes("/overview/geographies/geography/country/span[@label='ctr']");
            foreach (XmlNode nodeFact in nodeListFacts)
            {
                var id = nodeFact.GetAttribute("data-fact-id");
                if (!string.IsNullOrEmpty(id))
                {
                    if (!sdeIds.Contains(id)) sdeIds.Add(id);
                }
                else
                {
                    appLogger.LogWarning("Unable to find SDE id");
                }
            }


            //
            // => Retrieve the mappings
            //
            Console.WriteLine("** Rerieving mapping information **");
            var xmlMappingInformation = await Taxxor.ConnectedServices.MappingService.RetrieveMappingInformation(sdeIds, projectVars.projectId, false);

            if (!XmlContainsError(xmlMappingInformation))
            {

                var xPathMappingClusters = "/mappingClusters/mappingCluster";
                if (factIds != "all")
                {
                    xPathMappingClusters = $"{xPathMappingClusters}[@requestId='{factIds}']";
                }

                var sdeCounter = 0;
                var nodeListMappingClusters = xmlMappingInformation.SelectNodes(xPathMappingClusters);
                foreach (XmlNode nodeMappingCluster in nodeListMappingClusters)
                {
                    sdeCounter++;
                    var factId = nodeMappingCluster.GetAttribute("requestId");
                    if (string.IsNullOrEmpty(factId))
                    {
                        logInfo.AddErrorLogEntry("Unable to find fact ID");
                        continue;
                    }
                    else
                    {
                        logInfo.AddInformationLogEntry($"Processing SDE with ID: {factId}");
                    }

                    Console.WriteLine($"** Original Cluster **");
                    Console.WriteLine(PrettyPrintXml(nodeMappingCluster));
                    Console.WriteLine($"**********************");


                    var xmlUpdatedMappingCluster = new XmlDocument();


                    if (nodeMappingCluster != null)
                    {

                        xmlUpdatedMappingCluster.LoadXml(nodeMappingCluster.OuterXml);

                        var nodeInternalEntry = _getInternalEntry(nodeMappingCluster, factId);

                        if (nodeInternalEntry != null)
                        {
                            // Console.WriteLine("###### INTERNAL MAPPING ######");
                            // Console.WriteLine(nodeInternalEntry.OuterXml);
                            // Console.WriteLine("##############################");


                            Console.WriteLine("***************************");
                            Console.WriteLine($"Updating mapping cluster ({sdeCounter}/{nodeListMappingClusters.Count})");
                            Console.WriteLine("***************************");


                            //
                            // => Update the mapping cluster with the new period in the database
                            //
                            var nodeNewInternalEntry = _getInternalEntry(xmlUpdatedMappingCluster.DocumentElement, factId);
                            if (nodeNewInternalEntry != null)
                            {

                                nodeNewInternalEntry.SetAttribute("flipsign", "true");


                                xmlUpdatedMappingCluster.DocumentElement.SetAttribute("projectId", projectVars.projectId);
                                xmlUpdatedMappingCluster.DocumentElement.RemoveAttribute("requestId");

                                var xmlUpdateResult = await Taxxor.ConnectedServices.MappingService.UpdateMappingEntry(xmlUpdatedMappingCluster);
                                if (XmlContainsError(xmlUpdateResult))
                                {
                                    logInfo.AddErrorLogEntry($"Updating factId {factId} to flip the sign failed. Error: {xmlUpdateResult.OuterXml}");
                                }
                                else
                                {
                                    logInfo.AddSuccessLogEntry($"Successfully updated mapping cluster and flipped sign for {factId}");
                                }
                                // if (debugRoutine)
                                // {
                                //     Console.WriteLine("***************** UPDATE RESULT ********************");
                                //     Console.WriteLine(PrettyPrintXml(xmlUpdateResult));
                                //     Console.WriteLine("*************************************");
                                // }
                            }
                            else
                            {
                                logInfo.AddErrorLogEntry($"Could not find internal entry node for factId: {factId}");
                            }
                        }
                        else
                        {
                            logInfo.AddErrorLogEntry($"Could not find internal mapping entry for {factId}");
                            continue;
                        }


                    }



                }
            }
            else
            {
                HandleError(xmlMappingInformation);
            }


            var responseToClient = $@"
                <html>
                    <head>
                        <title>Testing page</title>
                    </head>
                    <body>
                        <h2>Taxes Paid Summary Flip Sign Change - {DateTime.Now.ToString()}</h2>
                        <pre>

                        - sdeTotal: {sdeIds.Count}
                        {logInfo.LogToString()}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);

        }

        public async static Task SandboxRetrieveTool(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            try
            {
                var htmlContent = await RetrieveTextFile(websiteRootPathOs + "/utilities/pdf-viewer.html");
                await response.OK(htmlContent, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to render tool page", ex.ToString());
            }
        }


        public async static Task SandboxRetrievePdfHtml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            var projectId = "q12018";

            string retrievedHtml = await RetrieveLatestPdfHtml(projectId, "latest", "all", false, false);

            //string retrievedHtml = await RetrieveLatestPdfHtmlFromServer("http://localhost:4812/report_editors/default_filing/assets/pages/preview_pdfsource.html?pid=johantest_1528030309671&vid=1&ocvariantid=&oclang=en&did=all&rnd=c539978tv8rntycm");

            var responseToClient = $@"
                <html>
                    <head>
                        <title>{reqVars.pageTitle}</title>
                    </head>
                    <body>
                        <h1>HTML for PDF Generation</h1>
                        <pre>
                        {HtmlEncodeForDisplay(retrievedHtml)}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);
        }




        public async static Task SandboxCmd(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);
            var projectVars = RetrieveProjectVariables(context);

            // var cmdResult = ExecuteCommandLine<string>("ls -all", null, null, null, false, true, "bash", applicationRootPathOs);
            var cmdResult = "";
            var result = await ExecuteCommandLineAsync("git --version", applicationRootPathOs);
            if (!result.Success)
            {
                cmdResult = result.ToString();
            }
            else
            {
                cmdResult = result.Payload;
            }

            // var cmdResult = ExecuteCommandLine("ls -all");

            var responseToClient = $@"
                <html>
                    <head>
                        <title>{reqVars.pageTitle}</title>
                    </head>
                    <body>
                        <h2>Render XBRL Result</h2>
                        <pre>
                        {cmdResult}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);
        }

        public async static Task SandboxViewRbacContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var xmlResult = await AccessControlService.DumpDbAsXml();


            var responseToClient = $@"
                <html>
                    <head>
                        <title>Done with test</title>
                    </head>
                    <body>
                        <h1>Users In Group</h1>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlResult.OuterXml))}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);
        }

        public async static Task SandboxCleanupRbacContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var clientId = "taxxor";

            var xmlRbac = new XmlDocument();
            xmlRbac.Load($"/Users/jthijs/Documents/my_projects/taxxor/tdm/data/{clientId}/AccessControlService/rbac.xml");

            var availableUsers = new List<string>();
            var referredUsers = new List<string>();
            var orphanedUsers = new List<string>();

            var nodeListAvailableUsers = xmlRbac.SelectNodes("/rbac/users/user");
            foreach (XmlNode node in nodeListAvailableUsers)
            {
                if (!availableUsers.Contains(node.GetAttribute("id"))) availableUsers.Add(node.GetAttribute("id"));
            }

            var nodeListReferredUsers = xmlRbac.SelectNodes("/rbac/groups/group/user");
            foreach (XmlNode node in nodeListReferredUsers)
            {
                var refferdUserid = node.GetAttribute("ref");
                if (!availableUsers.Contains(refferdUserid) && !orphanedUsers.Contains(refferdUserid)) orphanedUsers.Add(refferdUserid);
            }

            foreach (var orphanedUserId in orphanedUsers)
            {
                var nodeListToDelete = xmlRbac.SelectNodes($"/rbac/groups/group/user[@ref='{orphanedUserId}']");
                if (nodeListToDelete.Count > 0) RemoveXmlNodes(nodeListToDelete);

                nodeListToDelete = xmlRbac.SelectNodes($"/rbac/accessRecords/accessRecord[userRef/@ref='{orphanedUserId}']");
                if (nodeListToDelete.Count > 0) RemoveXmlNodes(nodeListToDelete);
            }

            // Normalize group ID's
            var groupIds = new Dictionary<string, string>();
            var nodeListGroupsToNormalize = xmlRbac.SelectNodes($"/rbac/groups/group[starts-with(@id, 'philips') and not(@id='philips-design')]");
            foreach (XmlNode nodeGroup in nodeListGroupsToNormalize)
            {
                if (!groupIds.ContainsKey(nodeGroup.GetAttribute("id"))) groupIds.Add(nodeGroup.GetAttribute("id"), nodeGroup.GetAttribute("id").Replace("philips-", ""));
            }


            foreach (var pair in groupIds)
            {
                var groupid = pair.Key;
                var groupidNew = pair.Value;
                xmlRbac.SelectSingleNode($"/rbac/groups/group[@id='{groupid}']")?.SetAttribute("id", groupidNew);
                var nodeListAccessRecords = xmlRbac.SelectNodes($"/rbac/accessRecords/accessRecord/groupRef[@ref='{groupid}']");
                foreach (XmlNode node in nodeListAccessRecords)
                {
                    node.SetAttribute("ref", groupidNew);
                }

            }

            // Strip access records for projects that are not used in the initial setup
            var nodeListAccessRecordsToRemove = xmlRbac.SelectNodes($"/rbac/accessRecords/accessRecord[not(contains(resourceRef/@ref, '__ar19')) and not(contains(resourceRef/@ref, 'cms-overview'))  and not(contains(resourceRef/@ref, 'q120')) and not(resourceRef/@ref = 'root')]");
            if (nodeListAccessRecordsToRemove.Count > 0) RemoveXmlNodes(nodeListAccessRecordsToRemove);

            nodeListAccessRecordsToRemove = xmlRbac.SelectNodes($"/rbac/accessRecords/accessRecord[contains(resourceRef/@ref, '__q120-1')]");
            if (nodeListAccessRecordsToRemove.Count > 0) RemoveXmlNodes(nodeListAccessRecordsToRemove);

            xmlRbac = TransformXmlToDocument(xmlRbac, "/Users/jthijs/Documents/my_projects/taxxor/tdm/services/AccessControlService/utilities/sort.xsl");
            xmlRbac.LoadXml(PrettyPrintXml(xmlRbac));

            await xmlRbac.SaveAsync($"{logRootPathOs}/rbac.{clientId}.cleanup.xml", true, true);
            var responseToClient = $@"
                <html>
                    <head>
                        <title>Done with test</title>
                    </head>
                    <body>
                        <h1>Orphaned users:</h1>
                        <pre>
                        {string.Join(",", orphanedUsers)}
                        </pre>
                        <h1>Rbac XML:</h1>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlRbac.OuterXml))}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);
        }


        public async static Task SandboxFixRbacContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var clientId = "taxxor";

            var xmlRbac = new XmlDocument();
            xmlRbac.Load($"/Users/jthijs/Documents/my_projects/taxxor/tdm/data/{clientId}/AccessControlService/rbac.xml");

            var resourceIds = new List<string>();
            var nodeListAccessRecords = xmlRbac.SelectNodes("/rbac/accessRecords/accessRecord/resourceRef");
            foreach (XmlNode node in nodeListAccessRecords)
            {
                var reference = node.GetAttribute("ref");
                if (!resourceIds.Contains(reference)) resourceIds.Add(reference);
            }

            var nodeRoot = xmlRbac.SelectSingleNode("/rbac/resources");
            foreach (var reference in resourceIds)
            {
                // Check it this resource already exists in the XML file
                if (xmlRbac.SelectSingleNode($"/rbac/resources/resource[@id='{reference}']") == null)
                {
                    var nodeNewResource = xmlRbac.CreateElement("resource");
                    nodeNewResource.SetAttribute("id", reference);
                    nodeRoot.AppendChild(nodeNewResource);
                }
            }


            xmlRbac = TransformXmlToDocument(xmlRbac, "/Users/jthijs/Documents/my_projects/taxxor/tdm/services/AccessControlService/utilities/sort.xsl");
            xmlRbac.LoadXml(PrettyPrintXml(xmlRbac));

            await xmlRbac.SaveAsync($"{logRootPathOs}/rbac.{clientId}.cleanup.xml", true, true);
            var responseToClient = $@"
                <html>
                    <head>
                        <title>Done with test</title>
                    </head>
                    <body>
                        <h1>Resource references found in access records:</h1>
                        <pre>
                        {string.Join(",", resourceIds)}
                        </pre>
                        <h1>Rbac XML:</h1>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlRbac.OuterXml))}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);
        }

        private static async Task<string> GetAmazonS3FileContent(string s3Uri)
        {
            var bucketName = RegExpReplace(@"^s3:\/\/([^\/]*)(.*)$", s3Uri, "$1");
            var fileName = RegExpReplace(@"^s3:\/\/([^\/]*)(.*)$", s3Uri, "$2");
            fileName = RegExpReplace(@"^\/(.*)$", fileName, "$1");

            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName
                };

                using var client = new AmazonS3Client(RegionEndpoint.EUWest1);
                using GetObjectResponse response = await client.GetObjectAsync(request);
                using Stream responseStream = response.ResponseStream;
                using StreamReader reader = new StreamReader(responseStream);
                string title = response.Metadata["x-amz-meta-title"]; // Assume you have "title" as medata added to the object.
                string contentType = response.Headers["Content-Type"];
                Console.WriteLine("Object metadata, Title: {0}", title);
                Console.WriteLine("Content type: {0}", contentType);

                return reader.ReadToEnd(); // Now you process the response body.
            }
            catch (AmazonS3Exception e)
            {
                return $"ERROR: Amazon Exception. error: {e.Message} when retrieving an object. bucketName: {bucketName}, fileName: {fileName}";
            }
            catch (Exception e)
            {
                return $"ERROR: Generic Exception. Message: {e.ToString()} when retrieving the object. bucketName: {bucketName}, fileName: {fileName}";
            }
        }


        public async static Task SandboxGenerateFullHtml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar23", true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            // /Users/jthijs/Documents/my_projects/taxxor/tdm/data/_shared/temp/website_ar20-en/publication/website.xml
            var bogusFilePathOs = "/mnt/shared/temp/website_ar23-en/publication/index_en.html";
            var fullWebsiteGenerationResult = await RenderFullHtmlAssets(context, bogusFilePathOs, "en");

            try
            {

                var responseToClient = $@"
                <html>
                    <head>
                        <title>Done with test</title>
                    </head>
                    <body>
                        <h1>Generated full website</h1>
                        - success: {fullWebsiteGenerationResult.Success}
                        <br/>
                        - message: {fullWebsiteGenerationResult.Message}
                        <br/>
                        - debuginfo: {fullWebsiteGenerationResult.DebugInfo}
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to generate full website content", ex.ToString());
            }

        }


        public async static Task SandboxGenerateWebsite(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar22", true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            // Hard code the legal entity for now
            projectVars.guidEntityGroup = "taxxor-main";
            projectVars.guidLegalEntity = "voltana";
            SetProjectVariables(context, projectVars);

            try
            {
                var nodeListWebsiteVariants = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='htmlsite']/variants/variant");
                if (nodeListWebsiteVariants.Count == 0) throw new Exception("No website hierarchies found");
                foreach (XmlNode nodeWebsiteVariant in nodeListWebsiteVariants)
                {
                    // <variant id="arsiteen" lang="en" metadata-id-ref="hierarchy-ar-htmlsite-en">
                    Console.WriteLine(nodeWebsiteVariant.GetAttribute("id") + " " + nodeWebsiteVariant.GetAttribute("lang") + " " + nodeWebsiteVariant.GetAttribute("metadata-id-ref"));
                }

                var websiteRenderResult = await RenderFullWebsite(context, "arsiteen", false, false, new List<string>());

                var xmlWebsiteHierarchy = projectVars.cmsMetaData["hierarchy-ar-htmlsite-en"];



                var responseToClient = $@"
                <html>
                    <head>
                        <title>Website Generation</title>
                    </head>
                    <body>
                        <h1>Website Generation Result</h1>
                        <pre>{websiteRenderResult.ToString()}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to render complete website", ex.ToString());
            }
        }



        public async static Task SandboxPostProcessDataImport(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            var projectId = "ar22";
            var importMode = "replace"; // append | replace

            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId, true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            var resultsToShow = new List<string>();
            try
            {
                resultsToShow.Add($"- projectId: {projectId}");
                resultsToShow.Add($"- importMode: {importMode}");

                //
                // => Global variables
                //
                var stopOnMultiplePdfVariants = false;

                //
                // => Test if uploaded files are present
                //
                var uploadedDataFilePathOs = CalculateFullPathOs("/temp/import/data.zip", "dataroot");
                resultsToShow.Add($"- uploadedDataFilePathOs: {uploadedDataFilePathOs}");
                if (!File.Exists(uploadedDataFilePathOs)) throw new Exception("Unable to locate uploaded data file");

                var uploadedDataManifestFilePathOs = CalculateFullPathOs("/temp/import/manifest.yml", "dataroot");
                resultsToShow.Add($"- uploadedDataManifestFilePathOs: {uploadedDataManifestFilePathOs}");
                if (!File.Exists(uploadedDataManifestFilePathOs)) throw new Exception("Unable to locate uploaded data manifest file");

                //
                // => Validate manifest file - check if all referenced files exist
                //
                var importBasePath = Path.GetDirectoryName(uploadedDataManifestFilePathOs);
                var manifestContent = await File.ReadAllTextAsync(uploadedDataManifestFilePathOs);
                var lines = manifestContent.Split('\n');

                var filesToCheck = new List<string>();
                var missingFiles = new List<string>();
                var currentSection = "";

                string hierarchyFilePathOs = null;
                var sectionXmlFiles = new List<string>();
                var imageFiles = new List<string>();

                // Parse YAML manifest to extract file paths
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Detect sections
                    if (trimmedLine.StartsWith("hierarchy:"))
                    {
                        currentSection = "hierarchy";
                    }
                    else if (trimmedLine.StartsWith("sections:"))
                    {
                        currentSection = "sections";
                    }
                    else if (trimmedLine.StartsWith("images:"))
                    {
                        currentSection = "images";
                    }
                    else if (trimmedLine.StartsWith("file:") && currentSection == "hierarchy")
                    {
                        // Extract file path after "file:"
                        var filePath = trimmedLine.Substring(5).Trim();
                        filesToCheck.Add(filePath);

                        // Store the full path to the hierarchy file for later use
                        hierarchyFilePathOs = Path.Combine(importBasePath, filePath);
                    }
                    else if (trimmedLine.StartsWith("- ") && (currentSection == "sections" || currentSection == "images"))
                    {
                        // Extract file path from list item
                        var filePath = trimmedLine.Substring(2).Trim();
                        filesToCheck.Add(filePath);

                        // Store the full path based on section
                        var fullPath = Path.Combine(importBasePath, filePath);
                        if (currentSection == "sections")
                        {
                            sectionXmlFiles.Add(fullPath);
                        }
                        else if (currentSection == "images")
                        {
                            imageFiles.Add(fullPath);
                        }
                    }
                }

                resultsToShow.Add($"- Total files referenced in manifest: {filesToCheck.Count}");

                // Check each file exists
                foreach (var relativeFilePath in filesToCheck)
                {
                    var fullFilePath = Path.Combine(importBasePath, relativeFilePath);

                    if (!File.Exists(fullFilePath))
                    {
                        missingFiles.Add(relativeFilePath);
                    }
                }

                if (missingFiles.Count > 0)
                {
                    resultsToShow.Add($"- ERROR: {missingFiles.Count} file(s) missing:");
                    foreach (var missingFile in missingFiles)
                    {
                        resultsToShow.Add($"  * {missingFile}");
                    }
                    throw new Exception($"Manifest validation failed: {missingFiles.Count} file(s) referenced in manifest are missing from the upload");
                }
                else
                {
                    resultsToShow.Add($"- Manifest validation: SUCCESS - All {filesToCheck.Count} files exist");
                    resultsToShow.Add($"- hierarchyFilePathOs: {hierarchyFilePathOs}");
                }

                resultsToShow.Add($"- sectionXmlFiles.Count: {sectionXmlFiles.Count}");
                resultsToShow.Add($"- imageFiles.Count: {imageFiles.Count}");

                // resultsToShow.Add($"- hierarchyFilePathOs: {hierarchyFilePathOs}");

                //
                // => Check how many PDF output channels are defined for this project
                //
                var nodeListPdfOutputChannels = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='pdf']/variants/variant");
                resultsToShow.Add($"- Total PDF output channels: {nodeListPdfOutputChannels.Count}");
                if (nodeListPdfOutputChannels.Count == 0) throw new Exception("No PDF output channels defined for this project");
                if (nodeListPdfOutputChannels.Count > 1 && stopOnMultiplePdfVariants) throw new Exception("Multiple PDF output channels defined for this project - not supported in automatic import");
                // resultsToShow.Add($"- nodeListPdfOutputChannels.Item(0).OuterXml: {HtmlEncodeForDisplay(nodeListPdfOutputChannels.Item(0).OuterXml)}");
                var outputChannelVariantId = nodeListPdfOutputChannels.Item(0).GetAttribute("id");
                var outputChannelLanguage = nodeListPdfOutputChannels.Item(0).GetAttribute("lang");
                resultsToShow.Add($"- outputChannelVariantId: {outputChannelVariantId}");
                resultsToShow.Add($"- outputChannelLanguage: {outputChannelLanguage}");

                //
                // => Load the current hierarchy file and prepare for updates
                //
                XmlDocument xmlOriginalHierarchy = await FilingData.LoadHierarchy(projectVars.projectId, projectVars.versionId, projectVars.editorId, "pdf", outputChannelVariantId, outputChannelLanguage);
                // resultsToShow.Add($"- xmlOriginalHierarchy: {HtmlEncodeForDisplay(xmlOriginalHierarchy.OuterXml)}");
                if (XmlContainsError(xmlOriginalHierarchy))
                {
                    appLogger.LogError($"Unable to load original output channel hierarchy from the document store for projectId: {projectVars.projectId}, versionId: {projectVars.versionId}, editorId: {projectVars.editorId}, outputChannelType: pdf, outputChannelVariantId: {outputChannelVariantId}, outputChannelLanguage: {outputChannelLanguage}");
                    throw new Exception("Unable to load original output channel hierarchy from the document store");
                }

                //
                // => Create a pre-import version
                //
                var preSyncVerionResult = await GenerateVersion(projectId, $"Pre import data version", false, projectVars);
                if (!preSyncVerionResult.Success)
                {
                    appLogger.LogError($"Unable to create pre-import version. Error: {preSyncVerionResult.Message}, DebugInfo: {preSyncVerionResult.DebugInfo}");
                    throw new Exception($"Unable to create pre-import version. Error: {preSyncVerionResult.Message}");
                }

                //
                // => Post process the hierarchy file - depending on the import mode
                //

                // Load the imported hierarchy file
                var xmlImportedHierarchy = new XmlDocument();
                xmlImportedHierarchy.Load(hierarchyFilePathOs);
                resultsToShow.Add($"- Loaded imported hierarchy from: {hierarchyFilePathOs}");

                // Debug flag for detailed logging
                var debugRoutine = true;

                //
                // => Post process the hierarchy file - depending on the import mode
                //
                if (importMode == "replace")
                {
                    resultsToShow.Add($"- Import mode: REPLACE");

                    // In the original hierarchy, remove all items between "toc" and "back-cover"
                    var nodeOriginalSubItems = xmlOriginalHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                    if (nodeOriginalSubItems == null)
                    {
                        throw new Exception("Could not find /items/structured/item/sub_items in original hierarchy");
                    }

                    // Find the toc and back-cover nodes in original hierarchy
                    var nodeToc = nodeOriginalSubItems.SelectSingleNode("item[@id='toc']");
                    var nodeBackCover = nodeOriginalSubItems.SelectSingleNode("item[@id='back-cover']");

                    if (nodeToc == null)
                    {
                        throw new Exception("Could not find toc marker in original hierarchy");
                    }

                    if (nodeBackCover == null)
                    {
                        throw new Exception("Could not find back-cover marker in original hierarchy");
                    }

                    // Remove all nodes between toc and back-cover
                    var nodesToRemove = new List<XmlNode>();
                    var currentNode = nodeToc.NextSibling;

                    while (currentNode != null && currentNode != nodeBackCover)
                    {
                        // Only collect element nodes (skip text nodes, comments, etc.)
                        if (currentNode.NodeType == XmlNodeType.Element)
                        {
                            nodesToRemove.Add(currentNode);
                        }
                        currentNode = currentNode.NextSibling;
                    }

                    resultsToShow.Add($"- Removing {nodesToRemove.Count} items from original hierarchy between toc and back-cover");
                    foreach (var nodeToRemove in nodesToRemove)
                    {
                        var removedId = nodeToRemove.Attributes?["id"]?.Value ?? "unknown";
                        resultsToShow.Add($"  - Removed item: {removedId}");
                        nodeOriginalSubItems.RemoveChild(nodeToRemove);
                    }

                    // Get all items from imported hierarchy (no toc/back-cover markers in imported hierarchy)
                    var nodeImportedSubItems = xmlImportedHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                    if (nodeImportedSubItems == null)
                    {
                        throw new Exception("Could not find /items/structured/item/sub_items in imported hierarchy");
                    }

                    // Collect ALL item nodes from imported hierarchy
                    var importedItems = nodeImportedSubItems.SelectNodes("item");
                    if (importedItems == null || importedItems.Count == 0)
                    {
                        resultsToShow.Add($"- WARNING: No items found in imported hierarchy");
                    }
                    else
                    {
                        resultsToShow.Add($"- Inserting {importedItems.Count} items from imported hierarchy into original hierarchy");

                        // Insert imported nodes into original hierarchy between toc and back-cover
                        var insertAfterNode = nodeToc;
                        foreach (XmlNode importedItem in importedItems)
                        {
                            var importedId = importedItem.Attributes?["id"]?.Value ?? "unknown";
                            resultsToShow.Add($"  - Inserting item: {importedId}");

                            // Import the node into the original document
                            var importedNode = xmlOriginalHierarchy.ImportNode(importedItem, true);

                            // Insert after the current position
                            nodeOriginalSubItems.InsertAfter(importedNode, insertAfterNode);

                            // Update the insertion point for the next iteration
                            insertAfterNode = importedNode;
                        }

                        resultsToShow.Add($"- Successfully merged hierarchies in REPLACE mode");
                    }
                }
                else if (importMode == "append")
                {
                    resultsToShow.Add($"- Import mode: APPEND");

                    //
                    // Step 0: Check if "import-root" already exists and remove it
                    //
                    var sectionId = "import-root";
                    var sectionTitle = "Imported Structure";

                    var nodeOriginalSubItems = xmlOriginalHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                    if (nodeOriginalSubItems == null)
                    {
                        throw new Exception("Could not find /items/structured/item/sub_items in original hierarchy");
                    }

                    var existingImportRootItem = nodeOriginalSubItems.SelectSingleNode($"item[@id='{sectionId}']");
                    if (existingImportRootItem != null)
                    {
                        resultsToShow.Add($"- Found existing '{sectionId}' item in hierarchy - removing it");
                        nodeOriginalSubItems.RemoveChild(existingImportRootItem);
                        resultsToShow.Add($"- Removed existing '{sectionId}' item");
                    }
                    else
                    {
                        resultsToShow.Add($"- No existing '{sectionId}' item found - proceeding with clean import");
                    }

                    //
                    // Step 1: Create a new Section XML file named "import-root"
                    //
                    var createTimeStamp = createIsoTimestamp();

                    // Retrieve the template for section content
                    var filingDataTemplate = RetrieveTemplate("inline-editor-content");
                    if (filingDataTemplate == null)
                    {
                        throw new Exception("Could not load inline-editor-content template");
                    }

                    // Fill the template with data
                    var nodeDateCreated = filingDataTemplate.SelectSingleNode("/data/system/date_created");
                    var nodeDateModified = filingDataTemplate.SelectSingleNode("/data/system/date_modified");
                    var nodeContentOriginal = filingDataTemplate.SelectSingleNode("/data/content");

                    if (nodeDateCreated == null || nodeDateModified == null || nodeContentOriginal == null)
                    {
                        throw new Exception("Could not find required elements in data template");
                    }

                    // Create one content node per language
                    var languages = RetrieveProjectLanguages(projectVars.editorId);
                    foreach (var lang in languages)
                    {
                        var nodeContent = nodeContentOriginal.CloneNode(true);
                        var nodeContentArticle = nodeContent.FirstChild;
                        var nodeContentHeader = nodeContentArticle.SelectSingleNode("div/section/h1");

                        if (nodeContentArticle == null || nodeContentHeader == null)
                        {
                            throw new Exception("Could not find article or header elements in content template");
                        }

                        nodeDateCreated.InnerText = createTimeStamp;
                        nodeDateModified.InnerText = createTimeStamp;
                        SetAttribute(nodeContent, "lang", lang);
                        SetAttribute(nodeContentArticle, "id", sectionId);
                        SetAttribute(nodeContentArticle, "data-guid", sectionId);
                        SetAttribute(nodeContentArticle, "data-last-modified", createTimeStamp);
                        nodeContentHeader.InnerText = sectionTitle;

                        filingDataTemplate.DocumentElement.AppendChild(nodeContent);
                    }

                    // Remove the original template node
                    RemoveXmlNode(nodeContentOriginal);

                    resultsToShow.Add($"- Created section template for: {sectionId}");

                    // Save the new section to the document store
                    var xmlCreateResult = await DocumentStoreService.FilingData.CreateSourceData(
                        sectionId,
                        filingDataTemplate,
                        projectVars.projectId,
                        projectVars.versionId,
                        "text",
                        projectVars.outputChannelDefaultLanguage,
                        debugRoutine
                    );

                    if (XmlContainsError(xmlCreateResult))
                    {
                        appLogger.LogError($"Failed to create import-root section: {xmlCreateResult.OuterXml}");
                        throw new Exception("Failed to create import-root section in document store");
                    }

                    resultsToShow.Add($"- Successfully created section in document store: {sectionId}");

                    //
                    // Step 2: Create the new <item> node in the hierarchy
                    //
                    // Note: nodeOriginalSubItems was already declared in Step 0

                    var nodeToc = nodeOriginalSubItems.SelectSingleNode("item[@id='toc']");
                    if (nodeToc == null)
                    {
                        throw new Exception("Could not find toc marker in original hierarchy");
                    }

                    var newItem = xmlOriginalHierarchy.CreateElement("item");
                    SetAttribute(newItem, "id", sectionId);
                    SetAttribute(newItem, "level", "0");
                    SetAttribute(newItem, "data-ref", $"{sectionId}.xml");

                    var webPage = xmlOriginalHierarchy.CreateElement("web_page");
                    var path = xmlOriginalHierarchy.CreateElement("path");
                    path.InnerText = "/";
                    var linkname = xmlOriginalHierarchy.CreateElement("linkname");
                    linkname.InnerText = sectionTitle;

                    webPage.AppendChild(path);
                    webPage.AppendChild(linkname);
                    newItem.AppendChild(webPage);

                    var subItems = xmlOriginalHierarchy.CreateElement("sub_items");
                    newItem.AppendChild(subItems);

                    // Insert the new item after the toc node
                    nodeOriginalSubItems.InsertAfter(newItem, nodeToc);

                    resultsToShow.Add($"- Added new hierarchy item: {sectionId}");

                    //
                    // Step 3: Add all items from imported hierarchy into the new item's sub_items
                    //
                    var nodeImportedSubItems = xmlImportedHierarchy.SelectSingleNode("/items/structured/item/sub_items");
                    if (nodeImportedSubItems == null)
                    {
                        throw new Exception("Could not find /items/structured/item/sub_items in imported hierarchy");
                    }

                    // Collect all item nodes from imported hierarchy
                    var importedItems = nodeImportedSubItems.SelectNodes("item");
                    if (importedItems == null || importedItems.Count == 0)
                    {
                        resultsToShow.Add($"- WARNING: No items found in imported hierarchy");
                    }
                    else
                    {
                        resultsToShow.Add($"- Importing {importedItems.Count} items from imported hierarchy");

                        foreach (XmlNode importedItem in importedItems)
                        {
                            var importedId = importedItem.Attributes?["id"]?.Value ?? "unknown";
                            resultsToShow.Add($"  - Importing item: {importedId}");

                            // Import the node into the original document
                            var importedNode = xmlOriginalHierarchy.ImportNode(importedItem, true);

                            // Add to the sub_items of the new import-root item
                            subItems.AppendChild(importedNode);
                        }

                        resultsToShow.Add($"- Successfully merged hierarchies in APPEND mode");
                    }
                }
                else
                {
                    throw new Exception($"Unknown import mode: {importMode}");
                }



                //
                // => Save the new hierarchy file
                //
                XmlDocument xmlSaveResult = await FilingData.SaveHierarchy(xmlOriginalHierarchy, projectVars.projectId, projectVars.versionId, projectVars.editorId, "pdf", outputChannelVariantId, outputChannelLanguage, true, true);
                if (XmlContainsError(xmlSaveResult))
                {
                    appLogger.LogError($"Unable to save updated output channel hierarchy to the document store for projectId: {projectVars.projectId}, versionId: {projectVars.versionId}, editorId: {projectVars.editorId}, outputChannelType: pdf, outputChannelVariantId: {outputChannelVariantId}, outputChannelLanguage: {outputChannelLanguage}. Error: {xmlSaveResult.OuterXml}");
                    throw new Exception("Unable to save updated output channel hierarchy to the document store");
                }


                //
                // => Store the section XML files
                //
                resultsToShow.Add($"- Storing {sectionXmlFiles.Count} section XML files");
                var failedSectionSaves = 0;

                foreach (var sectionXmlFilePathOs in sectionXmlFiles)
                {
                    try
                    {
                        // Extract the data reference (filename without extension)
                        var fileName = Path.GetFileName(sectionXmlFilePathOs);
                        var dataReference = Path.GetFileNameWithoutExtension(fileName);

                        // Load the XML file from disk
                        var xmlFilingContentSourceData = new XmlDocument();
                        xmlFilingContentSourceData.Load(sectionXmlFilePathOs);

                        // Save to document store
                        if (debugRoutine) appLogger.LogInformation($"Storing section data with reference: '{fileName}'");
                        var saveResult = await DocumentStoreService.FilingData.Save<XmlDocument>(xmlFilingContentSourceData, $"/textual/{fileName}", "cmsdataroot", debugRoutine, false);

                        if (XmlContainsError(saveResult))
                        {
                            appLogger.LogError($"{fileName} failed to save with error: {saveResult.OuterXml}");
                            failedSectionSaves++;
                            continue;
                        }

                        resultsToShow.Add($"  - Saved: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError($"Error saving section XML file {sectionXmlFilePathOs}: {ex.Message}");
                        failedSectionSaves++;
                    }
                }

                if (failedSectionSaves > 0)
                {
                    throw new Exception($"Failed to save {failedSectionSaves} out of {sectionXmlFiles.Count} section XML files");
                }

                resultsToShow.Add($"- Successfully stored {sectionXmlFiles.Count} section XML files");



                //
                // => Store the image files
                //




                //
                // => Create the post-import version
                //
                var postSyncVerionResult = await GenerateVersion(projectId, $"Post import data version", false, projectVars);
                if (!preSyncVerionResult.Success)
                {
                    appLogger.LogError($"Unable to create pre-import version. Error: {postSyncVerionResult.Message}, DebugInfo: {postSyncVerionResult.DebugInfo}");
                    throw new Exception($"Unable to create pre-import version. Error: {postSyncVerionResult.Message}");
                }

                //
                // => Clear the caches
                //

                // Clear framework memory cache
                RemoveMemoryCacheAll();

                // Clear RBAC cache
                projectVars.rbacCache.ClearAll();

                // Optionally clear Document Store cache
                await DocumentStoreService.FilingData.ClearCache(false);



                var responseToClient = $@"
                <html>
                    <head>
                        <title>Post Process Result</title>
                    </head>
                    <body>
                        <h1>Post Process Result</h1>
                        <pre>{string.Join("\n", resultsToShow)}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError($"Failed to post process uploaded data <pre>{string.Join("\n", resultsToShow)}</pre>", ex.ToString());
            }
        }

        public async static Task SandboxRenderHtmlSitePage(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var projectId = "ar24";
            var outputChannelVariantId = "arsitenl";
            var sectionId = "message-from-the-ceonl";


            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId(projectId, true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            // Hard code the legal entity for now
            projectVars.guidEntityGroup = "cvo-main";
            projectVars.guidLegalEntity = "cvo-vereniging";
            projectVars.outputChannelVariantId = outputChannelVariantId;
            projectVars.outputChannelType = RetrieveOutputChannelTypeFromOutputChannelVariantId(projectVars.projectId, projectVars.outputChannelVariantId);
            projectVars.editorId = RetrieveEditorIdFromProjectId(projectVars.projectId);

            SetProjectVariables(context, projectVars);

            var outputChannelHierarchyMetadataKey = RetrieveOutputChannelHierarchyMetadataId(projectVars);

            try
            {
                var nodeListWebsiteVariants = xmlApplicationConfiguration.SelectNodes($"/configuration/editors/editor[@id='{projectVars.editorId}']/output_channels/output_channel[@type='htmlsite']/variants/variant");
                if (nodeListWebsiteVariants.Count == 0) throw new Exception("No website hierarchies found");

                XmlDocument xmlWebsiteHierarchy = projectVars.cmsMetaData[outputChannelHierarchyMetadataKey].Xml;
                var dataReference = xmlWebsiteHierarchy.SelectSingleNode($"/items/structured//item[@id='{sectionId}']")?.GetAttribute("data-ref") ?? "unknown";
                // Console.WriteLine(PrettyPrintXml(xmlWebsiteHierarchy));
                Console.WriteLine($"- dataReference: {dataReference}");

                var (Success, Html, ErrorMessage) = await RenderSingleWebPage(projectVars, xmlWebsiteHierarchy, sectionId);

                var responseToClient = "";
                if (Success)
                {
                    responseToClient = $@"
                <html>
                    <head>
                        <title>HTML Site Page Generation</title>
                    </head>
                    <body>
                        <h1>HTML Site Page Generation Result</h1>
                        <pre>{HtmlEncodeForDisplay(Html)}</pre>
                    </body>
                </html>";
                }
                else
                {
                    responseToClient = $@"
                <html>
                    <head>
                        <title>HTML Site Page Generation</title>
                    </head>
                    <body>
                        <h1>Error Generating HTML Site Page</h1>
                        <p>{ErrorMessage}</p>
                    </body>
                </html>";
                }

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to render HTML site page", ex.ToString());
            }
        }

        public async static Task SandboxRestoreHierarchy(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar24", true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");

            // Hard code the legal entity for now
            projectVars.guidEntityGroup = "taxxor-main";
            projectVars.guidLegalEntity = "voltana";
            SetProjectVariables(context, projectVars);

            /*

            {"arguments":["{\"token\":\"2382b882367924ff33893c640857fe940536771e\",
            \"pid\":\"ar24\",
            \"repositoryidentifier\":\"project-data\",
            \"commithash\":\"8ad52678c5d32acad8f9e56a92e43ce2379c264b\",
            \"operationid\":\"u\",
            \"sitestructureid\":\"arpdfen\",
            \"linkname\":\"Annual Report (English) pdf hierarchy (arpdfen)\"}

            */

            try
            {

                var restoreResult = await RestoreSectionContent(projectVars.projectId, projectVars.currentUser.Id, "project-data", "8ad52678c5d32acad8f9e56a92e43ce2379c264b", "u", "arpdfen", "Annual Report (English) pdf hierarchy (arpdfen)", "hierarchy");



                var responseToClient = $@"
                <html>
                    <head>
                        <title>Restore Sandbox</title>
                    </head>
                    <body>
                        <h1>Restore Result</h1>
                        <pre>{restoreResult.ToString()}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to validate XHTML content", ex.ToString());
            }
        }


        public async static Task SandboxCustomerCode(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            ICustomerCode? clientCode = context.RequestServices.GetService<ICustomerCode>();
            var resultFromCustomCode = clientCode.DoSomethingOptional("Hello from NetInterface");

            var resultFromLoremIpsum = clientCode.LoremIpsum();


            try
            {

                var responseToClient = $@"
                <html>
                    <head>
                        <title>Test customer code</title>
                    </head>
                    <body>
                        <h1>Test customer code</h1>
                        <p>Result one</p>
                        <p>{resultFromCustomCode}</p>
                        <p>Result two</p>
                        <p>{resultFromLoremIpsum}</p>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to generate full website content", ex.ToString());
            }

        }

        public async static Task SandboxRetrieveUserSectionInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            try
            {
                // RetrieveUserSectionInformation(RequestVariables reqVars, ProjectVariables projectVars, bool processOnlyOpenProjects, bool includePermissionDetails, bool includeNonEditableSections, List<string> projectIdFilter)
                // Details: ProjectId: ar23, HierarchyId: hierarchy-ar-pdf-nl, HierarchyName: PDF Hierarchy (NL), UserId: johan@taxxor.com, ProcessOnlyOpenProjects: True, IncludePermissionDetails: False, IncludeNonEditableSections: False, ProjectIdFilter: []
                var result = await RetrieveUserSectionInformation(reqVars, projectVars, true, false, false, new List<string> { });


                var responseToClient = $@"
                <html>
                    <head>
                        <title>Retrieve User Section Information</title>
                    </head>
                    <body>
                        <h1>Retrieve User Section Information</h1>
                        {result.ToString()}
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to generate full website content", ex.ToString());
            }

        }


        public async static Task SandboxProjectPeriodProperties(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var sbHtmlOutput = new StringBuilder();

            var projectIds = new string[] { "ar23", "q124", "q224", "q324", "q424" };

            foreach (var projectId in projectIds)
            {

                projectVars.projectId = projectId;
                projectVars = await FillProjectVariablesFromProjectId(projectVars);
                projectVars.currentUser.Id = SystemUser;

                var projectPeriodProperties = new ProjectPeriodProperties(projectVars.projectId);

                sbHtmlOutput.AppendLine($"<h2>Project ID: {projectVars.projectId}</h2><p>Project period properties:</p><pre>{projectPeriodProperties.DumpToString()}</pre><hr/>");

                // Repeat the rest of the code here...
            }
            try
            {

                var responseToClient = $@"
                <html>
                    <head>
                        <title>Test projectperiod properties</title>
                    </head>
                    <body>
                        <h1>Test projectperiod properties</h1>
                        {sbHtmlOutput.ToString()}
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to generate full website content", ex.ToString());
            }

        }

        public async static Task SandboxXsltTwoTransform(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            //
            // => Variables for the routine that probably need to be passed into a method
            //
            var projectId = "";
            string[] projectIds = ["ar23", "ar22", "ar21"];
            // loop over the projectIds
            foreach (var projectIdCheck in projectIds)
            {
                // Check if the project exists
                var nodeCmsProject = xmlApplicationConfiguration.SelectSingleNode($"/configuration/cms_projects/cms_project[@id='{projectIdCheck}']");
                if (nodeCmsProject != null)
                {
                    projectId = projectIdCheck;
                    // exit the loop if the project exists
                    break;
                }
            }


            projectVars.projectId = projectId;
            projectVars = await FillProjectVariablesFromProjectId(projectVars);
            projectVars.currentUser.Id = SystemUser;


            SetupPdfStylesheetCache(reqVars);


            try
            {
                var xmlFilingContentSourceData = new XmlDocument();
                var xhtmlContent = await RetrieveLatestPdfHtml(projectVars, "all", false, false);

                // Save as a temporary file
                // File.WriteAllText(logRootPathOs + "/pdfcontent.xml", xhtmlContent);

                // Gets the size, in bytes, of the string
                var xhtmlContentSize = Encoding.UTF8.GetByteCount(xhtmlContent);

                try
                {
                    xmlFilingContentSourceData.LoadXml(xhtmlContent);
                }
                catch (Exception ex)
                {
                    HandleError("Could not load full content", $"error: {ex}, projectVars: {projectVars.ToString()}");
                }

                // var xmlLoremIpsum = await ConvertToLoremIpsum(xmlFilingContentSourceData, "en", "renderengine-test");
                // var xmlLoremIpsum = await TransformXmlToDocumentRenderEngine(xmlFilingContentSourceData, "used-tags-utility");
                // var xmlLoremIpsum = await TransformXmlToDocumentRenderEngine(xmlFilingContentSourceData, "xsl_whitespace-markup-remover");

                var xsltParameters = new NameValueCollection
                {
                    { "replace-lang", (projectVars.outputChannelVariantLanguage == "zh") ? "chinese-simplified" : "lorem" }
                };

                // => New XSLT 3 transformation
                var stopwatch = Stopwatch.StartNew();
                var xmlLoremIpsum = await Xslt3TransformXmlToDocument(xmlFilingContentSourceData, "cms_xsl_dynamic-header-levels", xsltParameters);
                stopwatch.Stop();
                var elapsedTimeNew = stopwatch.ElapsedMilliseconds;

                // => Native XmlDocument transformation
                stopwatch = Stopwatch.StartNew();
                XsltArgumentList xsltArgs = new XsltArgumentList();
                xsltArgs.AddParam("replace-lang", "", (projectVars.outputChannelVariantLanguage == "zh") ? "chinese-simplified" : "lorem");
                var xmlLoremIpsumNative = TransformXmlToDocument(xmlFilingContentSourceData, "cms_xsl_dynamic-header-levels", xsltArgs);
                stopwatch.Stop();
                var elapsedTimeNative = stopwatch.ElapsedMilliseconds;



                // => Output the results
                Console.WriteLine($"New XSLT 3 transformation: {elapsedTimeNew} ms");
                Console.WriteLine($"Native XmlDocument transformation: {elapsedTimeNative} ms");


                var logLength = 1000;
                var responseToClient = $@"
                <html>
                    <head>
                        <title>Test XSLT3 transform</title>
                    </head>
                    <body>
                        <h1>XSLT transformation test</h1>
                        <p>XML data (projectId: {projectId}, size: {CalculateFileSize(xhtmlContentSize)}):</p>
                        <pre>{TruncateString(HtmlEncodeForDisplay(PrettyPrintXml(xmlFilingContentSourceData)), logLength)}</pre>
                        <p>XSLT 3 Service (transformed in {elapsedTimeNew} ms)</p>
                        <pre>{TruncateString(HtmlEncodeForDisplay(PrettyPrintXml(xmlLoremIpsum)), logLength)}</pre>
                        <hr/>
                        <p>Native XmlDocument (transformed in {elapsedTimeNative} ms)</p>
                        <pre>{TruncateString(HtmlEncodeForDisplay(PrettyPrintXml(xmlLoremIpsumNative)), logLength)}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed XSLT 2 test", ex.ToString());
            }

        }


        public async static Task SandboxDumpRequestInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            appLogger.LogInformation("-------- PDF Generator Incoming Request Information --------");
            Console.WriteLine(await DebugIncomingRequest(request));
            Console.WriteLine("");

            try
            {

                var responseToClient = $@"
                <html>
                    <head>
                        <title>Request information</title>
                    </head>
                    <body>
                        <h1>This page was requested with the following HTTP headers</h1>
                        <pre>{await DebugIncomingRequest(request)}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to dump all request information", ex.ToString());
            }
        }


        public async static Task SandboxPostProcessTrackChanges(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            try
            {
                var xmlTrackChanges = new XmlDocument();
                xmlTrackChanges.Load($"{logRootPathOs}/_diff-pdf-auditor.html");
                var xmlTrackChangesOriginal = new XmlDocument();
                xmlTrackChangesOriginal.ReplaceContent(xmlTrackChanges);

                PostProcessTrackChangesHtml(ref xmlTrackChanges);


                // Wrap all diffs into a span element

                var responseToClient = $@"
                <html>
                    <head>
                        <meta charset='utf-8'>
                        <title>Track changes simplified</title>
                    </head>
                    <body>
                        <h1>Simplified Track Changes</h1>
                        <h2>Original</h2>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlTrackChangesOriginal))}
                        </pre>
                         <h2>Simplified</h2>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlTrackChanges))}
                        </pre>
                        <hr/>
                        {xmlTrackChanges.SelectSingleNode("//article").OuterXml}
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to dump all request information", ex.ToString());
            }
        }


        public async static Task SandboxSdeOverview(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            //
            // => Variables for the routine that probably need to be passed into a method
            //
            var projectId = "ar21";
            projectVars.projectId = projectId;
            projectVars = await FillProjectVariablesFromProjectId(projectVars);
            projectVars.currentUser.Id = SystemUser;

            try
            {
                var dataRefs = new List<string>();
                var dataRefsInUse = new List<string>();
                var dataRefsOrphaned = new List<string>();
                var uniqueFacts = new List<string>();

                var xmlSdeOverview = new XmlDocument();
                xmlSdeOverview.AppendChild(xmlSdeOverview.CreateElement("sdeoverview"));

                // Retrieve hierarchy overview
                var xmlHierarchyOverview = await RenderOutputChannelHierarchyOverview(projectVars, true, true, true);

                // Create the list of all data references
                var nodeListAllItems = xmlHierarchyOverview.SelectNodes("/hierarchies/item_overview/items/item");
                foreach (XmlNode nodeItem in nodeListAllItems)
                {
                    var dataReference = nodeItem.GetAttribute("data-ref");
                    if (dataReference != null)
                    {
                        if (!dataRefs.Contains(dataReference)) dataRefs.Add(dataReference);
                    }
                    else
                    {
                        appLogger.LogWarning($"Data reference cannot be found on item with ID: {nodeItem.GetAttribute("id") ?? "unknown"}");
                    }
                }

                // Create a list of data refereces in use
                var nodeListItemsInUse = xmlHierarchyOverview.SelectNodes("/hierarchies/output_channel/items/structured//item");
                foreach (XmlNode nodeItemInUse in nodeListItemsInUse)
                {
                    var dataReference = nodeItemInUse.GetAttribute("data-ref");
                    if (dataReference != null)
                    {
                        if (!dataRefsInUse.Contains(dataReference)) dataRefsInUse.Add(dataReference);
                    }
                    else
                    {
                        appLogger.LogWarning($"Data reference cannot be found on item with ID: {nodeItemInUse.GetAttribute("id") ?? "unknown"}");
                    }
                }

                // Cleanup the list of all data references so that only the orphaned ones are left
                foreach (XmlNode nodeItem in nodeListAllItems)
                {
                    var dataReference = nodeItem.GetAttribute("data-ref");
                    if (dataRefsInUse.Contains(dataReference)) nodeItem.SetAttribute("delete", "true");
                }
                var nodeListToRemove = xmlHierarchyOverview.SelectNodes("/hierarchies/item_overview/items/item[@delete]");
                if (nodeListToRemove.Count > 0)
                {
                    appLogger.LogInformation($"Removing {nodeListToRemove.Count} items from the overview so that we create a unique list of orphaned nodes");
                    RemoveXmlNodes(nodeListToRemove);
                }

                // Custom customer code
                Extensions.PostProcessSdeOverview(ref xmlHierarchyOverview);

                // Retrieve the SDE information
                var nodeDataReferences = xmlHierarchyOverview.CreateElement("datareferences");
                foreach (var dataReference in dataRefs)
                {
                    var nodeDataReference = xmlHierarchyOverview.CreateElement("datareference");
                    nodeDataReference.SetAttribute("data-ref", dataReference);

                    // Retrieve the raw unprocessed file
                    var xmlFilingContentSourceData = await DocumentStoreService.FilingData.Load<XmlDocument>($"/textual/{dataReference}", "cmsdataroot", true, false);

                    if (XmlContainsError(xmlFilingContentSourceData))
                    {
                        var errorMessage = $"Could not load data. dataReference: {dataReference}, response: {xmlFilingContentSourceData.OuterXml}";
                        appLogger.LogError(errorMessage);
                        nodeDataReference.SetAttribute("error", "500");
                        nodeDataReference.InnerText = errorMessage;
                    }
                    else
                    {
                        var nodeListContent = xmlFilingContentSourceData.SelectNodes("/data/content");
                        foreach (XmlNode nodeContent in nodeListContent)
                        {
                            var language = nodeContent.GetAttribute("lang") ?? "unkown";
                            var nodeContentInformation = xmlHierarchyOverview.CreateElement("content");
                            nodeContentInformation.SetAttribute("lang", language);

                            // Get the SDE information
                            var xmlContent = new XmlDocument();
                            var nodeContentImported = xmlContent.ImportNode(nodeContent, true);
                            xmlContent.AppendChild(nodeContentImported);
                            var xmlSdeInformation = RenderStructuredDataElementOverview(xmlContent);

                            // Append the resulting information into the content node
                            var nodeImported = xmlHierarchyOverview.ImportNode(xmlSdeInformation.DocumentElement, true);
                            nodeContentInformation.AppendChild(nodeImported);

                            nodeDataReference.AppendChild(nodeContentInformation);
                        }


                    }

                    nodeDataReferences.AppendChild(nodeDataReference);
                }

                xmlHierarchyOverview.DocumentElement.AppendChild(nodeDataReferences);

                xmlHierarchyOverview.Save($"{logRootPathOs}/-sde-overview.xml");

                xmlSdeOverview = TransformXmlToDocument(xmlHierarchyOverview, "cms_sde-overview");

                var responseToClient = $@"
                <html>
                    <head>
                        <meta charset='utf-8'>
                        <title>SDE mapping overview</title>
                    </head>
                    <body>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlSdeOverview))}
                        </pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to render XBRL validation package", ex.ToString());
            }
        }

        public async static Task SandboxJsonToXml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            var debugRoutine = (siteType == "local" || siteType == "dev");

            var jsonData = await RetrieveTextFile($"{logRootPathOs}/_conversionservice-response.txt");
            appLogger.LogInformation($"Received JSON data: {jsonData.Length} characters");

            var xmlData = ConvertJsonToXml(jsonData, "root");

            appLogger.LogInformation($"XmlData data: {xmlData.OuterXml.Length} characters");

            try
            {
                var result = await RenderCollisionReport(projectVars, false);

                var responseToClient = $@"
                <html>
                    <head>
                        <meta charset='utf-8'>
                        <title>SDE mapping overview</title>
                    </head>
                    <body>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlData))}
                        </pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to render XBRL validation package", ex.ToString());
            }
            finally
            {
                jsonData = null;
                xmlData = null;

                Console.WriteLine("Garbage collection start.");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine("Garbage collection done.");
            }
        }




        public async static Task SandboxAcl(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            try
            {
                var permissions = await Taxxor.ConnectedServices.AccessControlService.RetrievePermissionsForResource("get__taxxoreditor__cms_project-details__6k_2022-02-22", true, true);
                // permissions = await Taxxor.ConnectedServices.AccessControlService.RetrievePermissionsForResource("get__taxxoreditor__cms_project-details__q319", true);

                var responseToClient = $@"
                <html>
                    <head>
                        <meta charset='utf-8'>
                        <title>Permissions</title>
                    </head>
                    <body>
                        <h1>Permissions test</h1>
                        <pre>
                        {permissions.ToString()}
                        </pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed", ex.ToString());
            }
        }


        public async static Task SandboxGetSectionDataGrpc(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);


            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar22-2", true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");



            //pid=ar22-2&vid=latest&type=text&did=company-profile-j0aexbl10n6i5v22&ctype=regular&rtype=annual-report&octype=pdf&ocvariantid=arpdfen&oclang=en

            SetProjectVariables(context, projectVars);


            try
            {
                // Timers
                var elapsedTimeGrpc = 0;
                var elapsedTimeLegacy = 0;
                TaxxorGrpcResponseMessage? grpcResponse = null;
                var xmlFilingContentGrpc = new XmlDocument();
                var xmlFilingContentLegacy = new XmlDocument();

                // Create the request for the gRPC service
                var grpcRequest = new GetFilingComposerDataRequest
                {
                    DataType = "text",
                    Did = "company-profile-j0aexbl10n6i5v22"
                };
                grpcRequest.GrpcProjectVariables = new GrpcProjectVariables();
                grpcRequest.GrpcProjectVariables.ProjectId = projectVars.projectId;
                grpcRequest.GrpcProjectVariables.VersionId = "latest";
                grpcRequest.GrpcProjectVariables.EditorId = RetrieveEditorIdFromProjectId(projectVars.projectId);
                grpcRequest.GrpcProjectVariables.EditorContentType = "regular";
                grpcRequest.GrpcProjectVariables.ReportTypeId = "annual-report";
                grpcRequest.GrpcProjectVariables.OutputChannelType = "pdf";
                grpcRequest.GrpcProjectVariables.OutputChannelVariantId = "arpdfen";
                grpcRequest.GrpcProjectVariables.OutputChannelVariantLanguage = "en";

                // Start the stopwatch
                var stopwatch = Stopwatch.StartNew();

                FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient = context.RequestServices.GetRequiredService<FilingComposerDataService.FilingComposerDataServiceClient>();

                // Call the gRPC service
                grpcResponse = await filingComposerDataClient.GetFilingComposerDataAsync(grpcRequest);
                xmlFilingContentGrpc.LoadXml(grpcResponse.Data);
                var nodeArticle = xmlFilingContentGrpc.SelectSingleNode($"//content[@lang={GenerateEscapedXPathString(grpcRequest.GrpcProjectVariables.OutputChannelVariantLanguage)}]/article");
                if (nodeArticle != null)
                {
                    xmlFilingContentGrpc.RemoveAll();
                    xmlFilingContentGrpc.AppendChild(xmlFilingContentGrpc.ImportNode(nodeArticle, true));
                }
                else
                {
                    xmlFilingContentGrpc.RemoveAll();
                    xmlFilingContentGrpc.AppendChild(xmlFilingContentGrpc.CreateElement("article"));
                }


                // Stop the stopwatch
                stopwatch.Stop();

                // Get the elapsed time
                elapsedTimeGrpc = stopwatch.Elapsed.Milliseconds;

                stopwatch = Stopwatch.StartNew();

                xmlFilingContentLegacy = await RetrieveFilingComposerXmlData(grpcRequest.GrpcProjectVariables.ProjectId, grpcRequest.GrpcProjectVariables.EditorId, grpcRequest.GrpcProjectVariables.VersionId, grpcRequest.Did, false);

                // Stop the stopwatch
                stopwatch.Stop();

                // Get the elapsed time
                elapsedTimeLegacy = stopwatch.Elapsed.Milliseconds;

                var responseToClient = $@"
                <html>
                    <head>
                        <title>gRPC Response</title>
                    </head>
                    <body>
                        <h1>gRPC Response (took: {elapsedTimeGrpc} ms)</h1>
                        <pre>{HtmlEncodeForDisplay(xmlFilingContentGrpc?.OuterXml ?? "")}</pre>
                        <h1>REST Response (took: {elapsedTimeLegacy} ms)</h1>
                        <pre>{HtmlEncodeForDisplay(xmlFilingContentLegacy?.OuterXml ?? "")}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to execute gRPC call", ex.ToString());
            }
        }


        public async static Task SandboxGitCompareFullHtml(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            SetupPdfStylesheetCache(reqVars);


            // Create a project variables instance on the context and fill it
            var renderProjectVarsResult = await ProjectVariablesFromProjectId("ar22-2", true);
            if (!renderProjectVarsResult) throw new Exception("Unable to retrieve project variables");



            //pid=ar22-2&vid=latest&type=text&did=company-profile-j0aexbl10n6i5v22&ctype=regular&rtype=annual-report&octype=pdf&ocvariantid=arpdfen&oclang=en

            SetProjectVariables(context, projectVars);


            try
            {
                /*

                RenderScope [string] =
                "single-section"
                Sections [string] =
                "all"
                Secure [bool] =
                false
                SignatureMarks [bool] =
                false
                TablesOnly [bool] =
                false
                TrackChanges [bool] =
                false
                UseContentStatus [bool] =
                false
                UseLoremIpsum [bool] =
                false
                Watermark [bool] =
                false

                */


                //
                // => Setup PDF properties object
                //
                PdfProperties pdfProperties = new()
                {
                    Sections = "all",
                    Mode = "diff",
                    Base = "v0.1",
                    Latest = "current",
                    Html = null,
                    RenderScope = "single-section",
                    Secure = false,
                    PrintReady = false,
                    Layout = "regular",
                    PostProcess = "none",
                    SignatureMarks = false,
                    TablesOnly = false,
                    UseLoremIpsum = false,
                    UseContentStatus = false,
                    HideCurrentPeriodDatapoints = false,
                    HideErrors = true,
                    Comments = false,
                    DisableExternalLinks = false,
                    EcoVersion = false,
                    FileName = null,
                    TrackChanges = false,
                    Watermark = false
                };

                pdfProperties.PdfGeneratorStringSet.Add("booktitle", "Lorem");







                // //
                // // => Render HTML type output
                // //
                // var compareSectionResult = await ProjectLogic.CompareSectionContent(
                //                                                                        projectVars.projectId,
                //                                                                        "project-data",
                //                                                                        projectVars.outputChannelVariantLanguage,
                //                                                                        "all",
                //                                                                        "current",
                //                                                                        "v0.1",
                //                                                                        "raw",
                //                                                                        "hierarchy-ar-pdf-en",
                //                                                                        "Full PDF"
                //                                                                    );

                XmlDocument pdfRenderResult = await PdfService.RenderRawFullDiffHtml<XmlDocument>(pdfProperties, projectVars, null, true);


                var responseToClient = $@"
                <html>
                    <head>
                        <title>Raw compare result</title>
                    </head>
                    <body>
                        <h1>Raw full PDF compare</h1>
                        <pre>{HtmlEncodeForDisplay(PrettyPrintXml(pdfRenderResult))}</pre>
                    </body>
                </html>";

                await response.OK(responseToClient, ReturnTypeEnum.Html, true);
            }
            catch (Exception ex)
            {
                HandleError("Failed to execute gRPC call", ex.ToString());
            }
        }



        public async static Task SandboxExperiment(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            // user id: "johan@taxxor.com"
            // pid: "ar22-2"
            // editor id: "annual-report"
            // version: "1"
            // did: "message-from-the-ceo"

            /*
                var dataToPost = new Dictionary<string, string>
                        {
                            { "userid", projectVars.userId },
                            { "pid", projectVars.projectId },
                            { "vid", versionId },
                            // Data types supported are "text", "config" - but since we need the content for the editor we will fix it to "text"
                            { "type", "text" },
                            { "did", did },
                            { "ctype", projectVars.editorContentType },
                            { "rtype", projectVars.reportTypeId },
                            { "octype", projectVars.outputChannelType },
                            { "ocvariantid", projectVars.outputChannelVariantId },
                            { "oclang", projectVars.outputChannelVariantLanguage }
                        };
            */



            // var formattedDate = "--";
            // var projectId = "ar18";
            // var nodeProjectConfig = xmlApplicationConfiguration.SelectSingleNode($"//cms_project[@id='{projectId}']");
            // var publicationDate = nodeProjectConfig.GetAttribute("date-publication");
            // if (!string.IsNullOrEmpty(publicationDate))
            // {
            //     var parsedDate = DateTime.Parse(publicationDate);
            //     formattedDate = parsedDate.ToString("MMMM dd, yyyy");
            // }

            // const int _max = 1000;

            // var xmlSource = new XmlDocument();
            // xmlSource.LoadXml(xmlApplicationConfiguration.OuterXml);
            // var xmlClone = new XmlDocument();


            // var s1 = Stopwatch.StartNew();
            // for (int i = 0; i < _max; i++)
            // {
            //     xmlClone.LoadXml("<foo/>");
            //     xmlClone.LoadXml(xmlSource.OuterXml);
            // }
            // s1.Stop();
            // Console.WriteLine("XML Clone using String:");
            // Console.WriteLine(((double) (s1.Elapsed.TotalMilliseconds * 1000000) / _max).ToString("0.00 ns"));


            // var s2 = Stopwatch.StartNew();
            // for (int i = 0; i < _max; i++)
            // {
            //     xmlClone.LoadXml("<foo/>");
            //     xmlClone.ReplaceContent(xmlSource);

            //     // xmlClone = new XmlDocument();
            //     // xmlClone.AppendChild(xmlClone.ImportNode(xmlSource.DocumentElement, true));
            // }
            // s2.Stop();
            // Console.WriteLine("XML Clone using ImportNode:");
            // Console.WriteLine(((double) (s2.Elapsed.TotalMilliseconds * 1000000) / _max).ToString("0.00 ns"));

            var nodeFootnoteRepository = xmlApplicationConfiguration.SelectSingleNode($"/configuration/locations/location[@id='footnote_repository']");

            try
            {
                // In a docker container the main (dotnet) process always starts with pid 1
                var listOpenFileHandlesCommand = $"ls /proc/1/fd | wc -l";

                // Execute the command on the commandline
                var commandlist = new List<string>();
                commandlist.Add(listOpenFileHandlesCommand);
                var cmdResultRaw = ExecuteCommandLine<string>(commandlist, false, true, true, "cmd.exe", applicationRootPathOs);

                Console.WriteLine("********");
                Console.WriteLine(cmdResultRaw);
                Console.WriteLine("********");


                // Retrieve the open file handles using a regular expression as there seems to be an issue with the XML that is returned from the framework method
                var openFileHandles = RegExpReplace(@"^.*(\d+).*$", cmdResultRaw, "$1", true);

                // Dump the result
                if (openFileHandles.ToLower().Contains("error"))
                {
                    Console.WriteLine($"ERROR: cmdResultRaw: {cmdResultRaw}, listOpenFileHandlesCommand: {listOpenFileHandlesCommand}");
                }
                else
                {
                    Console.WriteLine($"=> Open file handles result: {openFileHandles}");
                    appLogger.LogInformation($"- openFileHandles: {openFileHandles}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Could not check the open file handles. error: {ex}");
            }


            var xmlResult = await AccessControlService.ListUsersInGroup("administrators");

            double offset = -1.25;

            var projectPeriodMetadata = new ProjectPeriodProperties("ar19");
            var reportingPeriod = projectPeriodMetadata.ReportingPeriod;
            var projectType = projectPeriodMetadata.ProjectType;
            int currentProjectYear = projectPeriodMetadata.CurrentProjectYear;
            int currentProjectQuarter = projectPeriodMetadata.CurrentProjectQuarter;



            DateTime dateProject;
            switch (currentProjectQuarter)
            {
                case 0:
                    // This is an Annual Report, so the date to compare with is dec 31
                    dateProject = new DateTime(currentProjectYear, 12, 31, 23, 59, 59);
                    break;

                case 1:
                    // First quarter March 31
                    dateProject = new DateTime(currentProjectYear, 3, 31, 23, 59, 59);
                    break;

                case 2:
                    // Second quarter - June 30
                    dateProject = new DateTime(currentProjectYear, 6, 30, 23, 59, 59);
                    break;

                case 3:
                    // Third quarter - September 30
                    dateProject = new DateTime(currentProjectYear, 9, 31, 23, 59, 59);
                    break;

                case 4:
                    // Fourth quarter - Dec 31
                    dateProject = new DateTime(currentProjectYear, 12, 31, 23, 59, 59);
                    break;
                default:
                    dateProject = new DateTime();
                    appLogger.LogError($"There was an error determining the date offset - unknown currentProjectQuarter: {currentProjectQuarter}");
                    break;
            }

            DateTime newDate = dateProject.AddDays(offset * 364);


            var responseToClient = $@"
                <html>
                    <head>
                        <title>Done with test</title>
                    </head>
                    <body>
                        <h1>Dates, etc.</h1>
                        - reportingPeriod: {reportingPeriod}
                        <br/>
                        - dateProject: {dateProject.ToString()}
                        <br/>
                        - offset: {offset}
                        <br/>
                        - newDate: {newDate.ToString()}
                        <br/>
                        - newDateQuarter: Q{newDate.GetQuarter()} {newDate.Year}
                        <br/>
                        <br/>
                        
                    </body>
                </html>";

            await response.OK(responseToClient, ReturnTypeEnum.Html, true);
        }




        public static T Test<T>()
        {
            Console.WriteLine($"- typeof(T).ToString(): {typeof(T).ToString()}");

            return (T)Convert.ChangeType("foobar", typeof(T));
        }

    }
}