using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Taxxor.Project
{
    /// <summary>
    /// Logic to deal with user preferences and data
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        public class TaxxorPermissions
        {
            private List<string> _jsPermission = new List<string>();

            public bool All { get; set; } = false;

            public bool View { get; set; } = false;
            public bool Dummy { get; set; } = false;

            public bool EditSection { get; set; } = false;
            public bool CreateSection { get; set; } = false;
            public bool DeleteSection { get; set; } = false;
            public bool RestoreSection { get; set; } = false;


            public bool CreateFilingDocument { get; set; } = false;
            public bool DeleteFilingDocument { get; set; } = false;
            public bool CloneFilingDocument { get; set; } = false;

            public bool EditContent { get; set; } = false;
            public bool EditTables { get; set; } = false;
            public bool EditFootnotes { get; set; } = false;
            public bool EditFilingHierarchy { get; set; } = false;
            public bool ViewAuditorInformation { get; set; } = false;
            public bool ViewFilingVersions { get; set; } = false;
            public bool RestoreFilingVersion { get; set; } = false;
            public bool ViewDeveloperTools { get; set; } = false;
            public bool ManageAccessControl { get; set; } = false;
            public bool ManageExternalDataSets { get; set; } = false;
            public bool ManageStructuredDataSets { get; set; } = false;
            public bool GenerateOutputChannels { get; set; } = false;
            public bool UseExtraTools { get; set; } = false;
            public bool ManageXbrl { get; set; } = false;
            public bool AdminXbrl { get; set; } = false;
            public bool EditReportDesignPackage { get; set; } = false;

            public bool EditSourceMappings { get; set; } = false;
            public bool EditTargetMappings { get; set; } = false;

            public bool GenerateTrackChanges { get; set; } = false;
            public bool EditWhitespace { get; set; } = false;
            public bool LoginAs { get; set; } = false;
            public bool PublishWebsite { get; set; } = false;

            public bool CreateNote { get; set; } = false;
            public bool EditProjectProperties { get; set; } = false;



            public List<string> Permissions { get; set; } = new List<string>();

            public string JavaScriptPermissions { get; set; } = "prms = []";

            public TaxxorPermissions()
            {

            }

            public TaxxorPermissions(XmlDocument xmlFromRbacService, bool generateJavaScripVerion = true)
            {
                SetPermissions(xmlFromRbacService, generateJavaScripVerion);
            }

            public TaxxorPermissions(XmlNodeList nodeListPermissions, bool generateJavaScripVerion = true)
            {
                SetPermissions(nodeListPermissions, generateJavaScripVerion);
            }

            /// <summary>
            /// Helper function to add a permission ID to the lists in use
            /// </summary>
            /// <param name="permissionId"></param>
            /// <param name="generateJavaScripVersion"></param>
            private void _addPermissionToLists(string permissionId, bool generateJavaScripVersion)
            {
                if (generateJavaScripVersion && _jsPermission.IndexOf($"'{permissionId}'") == -1) _jsPermission.Add($"'{permissionId}'");
                if (Permissions.IndexOf(permissionId) == -1) Permissions.Add(permissionId);
            }

            /// <summary>
            /// Returns if the current user has a specific permission on this resource
            /// </summary>
            /// <param name="permissionId"></param>
            /// <returns></returns>
            public bool HasPermission(string permissionId)
            {
                return (Permissions.IndexOf(permissionId) > -1);
            }


            /// <summary>
            /// Converts the effective permissions from a nodelist into C# properties and a JavaScript array
            /// </summary>
            /// <param name="nodeListPermissions"></param>
            /// <param name="generateJavaScripVersion"></param>
            public void SetPermissions(XmlNodeList nodeListPermissions, bool generateJavaScripVersion = true)
            {
                if (nodeListPermissions.Count == 0)
                {
                    appLogger.LogWarning($"No permissions were passed to this routine. stack-trace: {GetStackTrace()}");
                }
                else
                {
                    // Reset the lists
                    this.Permissions = new List<string>();
                    this._jsPermission = new List<string>();


                    StringBuilder jsPermissions = new StringBuilder();
                    if (generateJavaScripVersion) jsPermissions.Append("prms = [");
                    foreach (XmlNode nodePermission in nodeListPermissions)
                    {
                        var permissionId = GetAttribute(nodePermission, "id").ToLower();
                        if (!string.IsNullOrEmpty(permissionId))
                        {
                            switch (permissionId)
                            {
                                case "all":
                                    All = true;

                                    View = true;
                                    _addPermissionToLists("view", generateJavaScripVersion);

                                    Dummy = true;
                                    _addPermissionToLists("dummy", generateJavaScripVersion);

                                    EditSection = true;
                                    _addPermissionToLists("editsection", generateJavaScripVersion);

                                    CreateSection = true;
                                    _addPermissionToLists("createsection", generateJavaScripVersion);

                                    DeleteSection = true;
                                    _addPermissionToLists("deletesection", generateJavaScripVersion);

                                    RestoreSection = true;
                                    _addPermissionToLists("restoresection", generateJavaScripVersion);

                                    CreateFilingDocument = true;
                                    _addPermissionToLists("createfilingdocument", generateJavaScripVersion);

                                    DeleteFilingDocument = true;
                                    _addPermissionToLists("deletefilingdocument", generateJavaScripVersion);

                                    CloneFilingDocument = true;
                                    _addPermissionToLists("clonefilingdocument", generateJavaScripVersion);

                                    EditContent = true;
                                    _addPermissionToLists("editcontent", generateJavaScripVersion);

                                    EditTables = true;
                                    _addPermissionToLists("edittables", generateJavaScripVersion);

                                    EditFootnotes = true;
                                    _addPermissionToLists("editfootnotes", generateJavaScripVersion);

                                    EditFilingHierarchy = true;
                                    _addPermissionToLists("editfilinghierarchy", generateJavaScripVersion);

                                    ViewAuditorInformation = true;
                                    _addPermissionToLists("viewauditorinformation", generateJavaScripVersion);

                                    ViewFilingVersions = true;
                                    _addPermissionToLists("viewfilingversions", generateJavaScripVersion);

                                    RestoreFilingVersion = true;
                                    _addPermissionToLists("restorefilingversion", generateJavaScripVersion);

                                    ViewDeveloperTools = true;
                                    _addPermissionToLists("viewdevelopertools", generateJavaScripVersion);

                                    GenerateTrackChanges = true;
                                    _addPermissionToLists("generatetrackchanges", generateJavaScripVersion);

                                    ManageAccessControl = true;
                                    _addPermissionToLists("manageacl", generateJavaScripVersion);

                                    ManageExternalDataSets = true;
                                    _addPermissionToLists("manageexternaldata", generateJavaScripVersion);

                                    ManageStructuredDataSets = true;
                                    _addPermissionToLists("managestructureddata", generateJavaScripVersion);

                                    GenerateOutputChannels = true;
                                    _addPermissionToLists("generateoutputchannels", generateJavaScripVersion);

                                    UseExtraTools = true;
                                    _addPermissionToLists("useextratools", generateJavaScripVersion);

                                    ManageXbrl = true;
                                    _addPermissionToLists("managexbrl", generateJavaScripVersion);

                                    AdminXbrl = true;
                                    _addPermissionToLists("adminxbrl", generateJavaScripVersion);

                                    EditReportDesignPackage = true;
                                    _addPermissionToLists("editreportdesignpackage", generateJavaScripVersion);

                                    LoginAs = true;
                                    _addPermissionToLists("loginas", generateJavaScripVersion);

                                    EditWhitespace = true;
                                    _addPermissionToLists("editwhitespace", generateJavaScripVersion);

                                    PublishWebsite = true;
                                    _addPermissionToLists("publishwebsite", generateJavaScripVersion);

                                    CreateNote = true;
                                    _addPermissionToLists("createnote", generateJavaScripVersion);

                                    EditProjectProperties = true;
                                    _addPermissionToLists("editprojectproperties", generateJavaScripVersion);

                                    EditSourceMappings = true;
                                    _addPermissionToLists("editmappingsource", generateJavaScripVersion);

                                    EditTargetMappings = true;
                                    _addPermissionToLists("editmappingtarget", generateJavaScripVersion);

                                    break;

                                case "view":
                                    View = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "dummy":
                                    Dummy = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editsection":
                                    EditSection = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "createsection":
                                    CreateSection = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "deletesection":
                                    DeleteSection = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "restoresection":
                                    RestoreSection = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "createfilingdocument":
                                    CreateFilingDocument = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "deletefilingdocument":
                                    DeleteFilingDocument = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "clonefilingdocument":
                                    CloneFilingDocument = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editcontent":
                                    EditContent = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "edittables":
                                    EditTables = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editfootnotes":
                                    EditFootnotes = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editfilinghierarchy":
                                    EditFilingHierarchy = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "viewauditorinformation":
                                    ViewAuditorInformation = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "viewfilingversions":
                                    ViewFilingVersions = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "restorefilingversion":
                                    RestoreFilingVersion = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "viewdevelopertools":
                                    ViewDeveloperTools = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "generatetrackchanges":
                                    GenerateTrackChanges = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "manageacl":
                                    ManageAccessControl = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "manageexternaldata":
                                    ManageExternalDataSets = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "managestructureddata":
                                    ManageStructuredDataSets = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "generateoutputchannels":
                                    GenerateOutputChannels = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "useextratools":
                                    UseExtraTools = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "managexbrl":
                                    ManageXbrl = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "adminxbrl":
                                    AdminXbrl = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editreportdesignpackage":
                                    EditReportDesignPackage = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editwhitespace":
                                    EditWhitespace = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "loginas":
                                    LoginAs = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "publishwebsite":
                                    PublishWebsite = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "createnote":
                                    CreateNote = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editprojectproperties":
                                    EditProjectProperties = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editmappingsource":
                                    EditSourceMappings = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;

                                case "editmappingtarget":
                                    EditTargetMappings = true;
                                    _addPermissionToLists(permissionId, generateJavaScripVersion);
                                    break;


                                default:
                                    appLogger.LogError($"Could not map permission with id: {permissionId.ToLower()}");
                                    break;
                            }
                        }
                    }

                    // Finalize the JavaScript string
                    if (generateJavaScripVersion)
                    {
                        jsPermissions.Append(string.Join(", ", _jsPermission));
                        jsPermissions.Append(']');
                        JavaScriptPermissions = jsPermissions.ToString();
                    }

                }




            }

            /// <summary>
            /// Converts the effective permissions received from the RBAC service into C# properties and a JavaScript array
            /// </summary>
            /// <param name="xmlFromRbacService"></param>
            /// <param name="generateJavaScripVersion"></param>
            public void SetPermissions(XmlDocument xmlFromRbacService, bool generateJavaScripVersion = true)
            {
                XmlNodeList? nodeListPermissions = xmlFromRbacService.SelectNodes("/ArrayOfPermission/permission");

                this.SetPermissions(nodeListPermissions, generateJavaScripVersion);
            }

        }

    }
}