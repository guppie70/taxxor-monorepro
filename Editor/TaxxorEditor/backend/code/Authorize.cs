using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        /// <summary>
        /// Authorizes an HTTP request
        /// </summary>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="pageId"></param>
        /// <param name="originatingRoutine"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> AuthorizeRequest(RequestVariables reqVars, ProjectVariables projectVars, string pageId, string originatingRoutine)
        {
            //
            // => Test if the page that the user tries to access, has been defined in the hierarchy
            //
            if (string.IsNullOrEmpty(pageId))
            {
                return new TaxxorReturnMessage(false, "Not found", $"Did not receive a valid page id, so the system cannot check if this user has access to the web page or not. originatingRoutine: {originatingRoutine}");
            }

            //
            // => Retrieve permissions from the RBAC hierarchy cache or by requesting the access control service
            //
            var xPathPermissions = "/user/root/permissions/permission";
            var rbacBreadCrumbtrail = "get__taxxoreditor__cms-overview,root";

            if (!string.IsNullOrEmpty(projectVars.projectId))
            {
                // Fills projectVars.cmsMetaData with information about the hierarchies and the permissions of the user for the (outputchannel) hierarchies
                var hierarchyRetrieveResult = await RetrieveOutputChannelHierarchiesMetaData(projectVars, reqVars);
                if (!hierarchyRetrieveResult) return new TaxxorReturnMessage(false, "Unable to retrieve hierarchy metadata", $"pageId: {pageId}, originatingRoutine: {originatingRoutine}");

                xPathPermissions = $"/user/projects/project[@id='{projectVars.projectId}']/permissions/permission";
                rbacBreadCrumbtrail = $"get__taxxoreditor__cms_project-details__{projectVars.projectId},{rbacBreadCrumbtrail}";
            }

            var permissionCount = 0;
            var foundProjectPermissions = false;
            XmlDocument? xmlProjectPermissions = null;
            XmlNodeList? nodeListPermissions = null;
            if (RbacCacheData.TryGetValue(projectVars.currentUser.Id, out xmlProjectPermissions))
            {
                foundProjectPermissions = true;
                nodeListPermissions = xmlProjectPermissions.SelectNodes(xPathPermissions);
                permissionCount = nodeListPermissions.Count;
            }

            if (foundProjectPermissions && permissionCount > 0)
            {
                projectVars.currentUser.Permissions.SetPermissions(nodeListPermissions);
            }
            else
            {
                // if (siteType == "local") appLogger.LogInformation($"Requesting new permissions. user-ID: {projectVars.currentUser.Id}, project-ID: {projectVars.projectId??"unknown"}");

                XmlDocument xmlPermissions = await AccessControlService.RetrievePermissionsForResources(rbacBreadCrumbtrail);
                if (XmlContainsError(xmlPermissions))
                {
                    appLogger.LogError($"There was a problem requesting new permissions for {rbacBreadCrumbtrail}: {ConvertErrorXml(xmlPermissions)}");
                }
                else
                {
                    nodeListPermissions = xmlPermissions.SelectNodes($"/items/item/permissions/permission");
                    if (nodeListPermissions.Count > 0)
                    {
                        projectVars.currentUser.Permissions.SetPermissions(nodeListPermissions);

                        // Store the permissions in the RBAC cache so that we only need to request it once
                        try
                        {
                            projectVars.rbacCache.StorePermissions(xPathPermissions.Replace("/permissions/permission", "/permissions"), nodeListPermissions);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogWarning($"There was a problem adding the permissions to the rbac cache data in the project variables. user-ID: {projectVars.currentUser.Id}, project-ID: {projectVars.projectId ?? "unknown"}, error: {ex}");
                        }
                    }
                    else
                    {
                        appLogger.LogWarning("Unable to find any permissions");
                    }
                }
            }

            // Properly handle the state if we were unable to retrieve permissions
            if (projectVars.currentUser.Permissions == null)
            {
                return new TaxxorReturnMessage(false, "Unable to retrieve permissions for user", $"userId: {projectVars.currentUser.Id}, pageId: {pageId}, originatingRoutine: {originatingRoutine}");
            }

            //
            // => Stop processing the page further if the user does not have view rights
            //
            if (!projectVars.currentUser.Permissions.View)
            {
                return new TaxxorReturnMessage(false, "Unauthorized access.<br/><br/>You do not have the enough permissions to view this page.<br/>Please contact your system administrator if you need access.", $"userId: {projectVars.currentUser.Id}, pageId: {pageId}, originatingRoutine: {originatingRoutine}, permissions: {GenerateDebugObjectString(projectVars.currentUser.Permissions, ReturnTypeEnum.Txt)}");
            }

            // Return success message
            return new TaxxorReturnMessage(true, "Successfully authorized this request");
        }

        /// <summary>
        /// Handles the security for a method called in the websockets HUB or gRPC Web using the attribute values used to decorate the functions
        /// </summary>
        /// <param name="attr"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static async Task<TaxxorReturnMessage> HandleMethodSecurity(AuthorizeAttribute attr, RequestVariables reqVars, ProjectVariables projectVars)
        {
            var debugRoutine = false;
            try
            {
                var virtualPath = attr.VirtualPath;
                if (debugRoutine) Console.WriteLine($"- virtualPath: {virtualPath}");

                var pageIdHub = RetrievePageId(reqVars, virtualPath, reqVars.xmlHierarchyStripped);
                if (debugRoutine) Console.WriteLine($"- pageIdHub: {pageIdHub}");

                // Authenticate this request
                var authenticateRequestResult = AuthenticateRequest(reqVars, projectVars, pageIdHub, "WebSocketHub.cs");
                if (debugRoutine) Console.WriteLine($"- authenticateRequestResult: {authenticateRequestResult.ToString()}");
                if (!authenticateRequestResult.Success) return authenticateRequestResult;

                // Authorize this request
                var authorizeRequestResult = await AuthorizeRequest(reqVars, projectVars, pageIdHub, "WebSocketHub.cs");

                return authorizeRequestResult;
            }
            catch (Exception ex)
            {
                return new TaxxorReturnMessage(false, "There was a problem finding access control rights for this websocket method", $"error: {ex}");
            }
        }
    }

    /// <summary>
    /// Custom attribute that we can use to decorate data WebSocketHub and gRPC web methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute
    {
        public string VirtualPath { get; set; }

        public AuthorizeAttribute() { }
    }
}