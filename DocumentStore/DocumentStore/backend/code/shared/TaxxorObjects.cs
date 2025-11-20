using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Framework;
using static Taxxor.Project.ProjectLogic;

public partial class FrameworkExtensions
{
    public virtual async Task UpdateSyncAndImportSystemStatusInClient(string clientEventName, bool isRunning, string projectId, double progress, List<string> runIds)
    {
        await Framework.DummyAwaiter();
    }

    public virtual async Task UpdateActiveUsersInClient(int count)
    {
        await Framework.DummyAwaiter();
    }

    public virtual void StoreErpImportLog(ProjectVariables projectVars) { }

    public virtual List<string> GetErpImportRunIds(List<string> runIds) { return runIds; }

    public virtual void RemoveErpImportObject(string runId) { }

    public virtual TaxxorReturnMessage ClearFsExistsCache()
    {
        return new TaxxorReturnMessage(true, "No need to clear the file system cache");
    }

    public virtual async Task<XmlDocument> RetrieveFilingComposerXmlData(ProjectVariables projectVarsForXhtmlCheck, string dataRef)
    {
        await Framework.DummyAwaiter();
        return new XmlDocument();
    }
}

namespace Taxxor.Project
{
    /// <summary>
    /// Objects used between Taxxor services
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {


        /// <summary>
        /// Holds statistics from a (bulk) Structured Data Elements synchronization
        /// Uses System.Runtime.Serialization.DataContractSerializer for (de-)serializing from and to XML
        /// </summary>
        [DataContract]
        public class SdeSyncStatictics
        {
            [DataMember]
            public bool Success { get; set; }

            [DataMember]
            public string Message { get; set; } = "";
            [DataMember]
            public string DebugInfo { get; set; } = "";

            /// <summary>
            /// Total number of structured data elements found
            /// </summary>
            /// <value></value>
            [DataMember]
            public int StructuredDataElementsFound { get; set; } = 0;

            /// <summary>
            /// Number of unique elements found and number of elements that will be queried
            /// </summary>
            /// <value></value>
            [DataMember]
            public int StructuredDataElementsUnique { get; set; } = 0;

            /// <summary>
            /// Number of structured data elements that were updated
            /// </summary>
            /// <value></value>
            [DataMember]
            public int StructuredDataElementsUpdated { get; set; } = 0;

            /// <summary>
            /// Number of structured data elements that did not need to be updated because the value from the structured data store was not changed
            /// </summary>
            /// <value></value>
            [DataMember]
            public int StructuredDataElementWithoutUpdate { get; set; } = 0;

            [DataMember]
            public StringBuilder LogSuccess { get; set; } = new StringBuilder();
            [DataMember]
            public StringBuilder LogWarning { get; set; } = new StringBuilder();
            [DataMember]
            public StringBuilder LogError { get; set; } = new StringBuilder();


            /// <summary>
            /// SDE which have synced without any problems
            /// </summary>
            /// <typeparam name="string"></typeparam>
            /// <typeparam name="string"></typeparam>
            /// <returns></returns>
            [DataMember]
            public List<SdeItem> SyncOk { get; set; } = [];

            /// <summary>
            /// SDE which did not sync due to a warning
            /// </summary>
            /// <typeparam name="string"></typeparam>
            /// <typeparam name="string"></typeparam>
            /// <returns></returns>
            [DataMember]
            public List<SdeItem> SyncWarning { get; set; } = [];

            /// <summary>
            /// SDE which did not sync due to a warning
            /// </summary>
            /// <typeparam name="string"></typeparam>
            /// <typeparam name="string"></typeparam>
            /// <returns></returns>
            [DataMember]
            public List<SdeItem> SyncError { get; set; } = [];

            /// <summary>
            /// Consolidated list of SDE which have synced without any problems
            /// </summary>
            /// <typeparam name="string"></typeparam>
            /// <typeparam name="string"></typeparam>
            /// <returns></returns>
            [DataMember]
            public Dictionary<string, List<SdeItem>> SyncOkConsolidated { get; set; } = [];

            /// <summary>
            /// Consolidated list of SDE which did not sync due to a warning
            /// </summary>
            /// <typeparam name="string"></typeparam>
            /// <typeparam name="string"></typeparam>
            /// <returns></returns>
            [DataMember]
            public Dictionary<string, List<SdeItem>> SyncWarningConsolidated { get; set; } = [];

            /// <summary>
            /// Consolidated list of SDE which did not sync due to a warning
            /// </summary>
            /// <typeparam name="string"></typeparam>
            /// <typeparam name="string"></typeparam>
            /// <returns></returns>
            [DataMember]
            public Dictionary<string, List<SdeItem>> SyncErrorConsolidated { get; set; } = [];


            public SdeSyncStatictics() { }

            public void ImportTaxxorResultMessage(TaxxorReturnMessage message)
            {
                this.Success = message.Success;
                this.Message = message.Message;
                this.DebugInfo = message.DebugInfo;
            }

            /// <summary>
            /// Adds the synchronization status of a fact ID into the correct internal dictionary
            /// </summary>
            /// <param name="factId"></param>
            /// <param name="syncStatus"></param>
            public void AddSdeSyncInfo(string factId, string syncStatus, string sdeValue)
            {

                var sdeItem = new SdeItem(factId, syncStatus, sdeValue);
                if (syncStatus.StartsWith("200") || syncStatus.StartsWith("201"))
                {
                    if (!this.ListContainsSdeItem(this.SyncOk, factId))
                    {
                        SyncOk.Add(sdeItem);
                    }
                    else
                    {
                        _ = this.LogWarning.AppendLine($"FactId: {factId} already exists in OK dictionary");
                    }
                }
                else if (syncStatus.StartsWith("500"))
                {
                    if (!this.ListContainsSdeItem(this.SyncError, factId))
                    {
                        SyncError.Add(sdeItem);
                    }
                    else
                    {
                        _ = this.LogWarning.AppendLine($"FactId: {factId} already exists in ERROR dictionary");
                    }
                }
                else
                {
                    if (!this.ListContainsSdeItem(this.SyncWarning, factId))
                    {
                        SyncWarning.Add(sdeItem);
                    }
                    else
                    {
                        _ = this.LogWarning.AppendLine($"FactId: {factId} already exists in WARNING dictionary");
                    }
                }
            }

            /// <summary>
            /// Used for consolidating multiple SdeSyncStatictics into one set of statistics
            /// </summary>
            /// <param name="dataReference"></param>
            /// <param name="syncStats"></param>
            public void ConsolidateSdeSyncInfo(string dataReference, SdeSyncStatictics syncStats)
            {

                // Consolidate base stats
                this.StructuredDataElementsFound = this.StructuredDataElementsFound + syncStats.StructuredDataElementsFound;
                this.StructuredDataElementsUnique = this.StructuredDataElementsUnique + syncStats.StructuredDataElementsUnique;
                this.StructuredDataElementsUpdated = this.StructuredDataElementsUpdated + syncStats.StructuredDataElementsUpdated;
                this.StructuredDataElementWithoutUpdate = this.StructuredDataElementWithoutUpdate + syncStats.StructuredDataElementWithoutUpdate;

                // Add the logs
                _ = this.LogSuccess.Append(syncStats.LogSuccess);
                _ = this.LogWarning.Append(syncStats.LogWarning);
                _ = this.LogError.Append(syncStats.LogError);

                // Add the individual stats of the sde item syncs
                this.SyncOk.AddRange(syncStats.SyncOk);
                this.SyncWarning.AddRange(syncStats.SyncWarning);
                this.SyncError.AddRange(syncStats.SyncError);

                // Add the individual sde stats
                this.SyncOkConsolidated.Add(dataReference, syncStats.SyncOk);
                this.SyncWarningConsolidated.Add(dataReference, syncStats.SyncWarning);
                this.SyncErrorConsolidated.Add(dataReference, syncStats.SyncError);
            }

            /// <summary>
            /// Tests if an SDE Item is already present in the list so that we can keep the list
            /// </summary>
            /// <param name="sdeItemList"></param>
            /// <param name="factId"></param>
            /// <returns></returns>
            private bool ListContainsSdeItem(List<SdeItem> sdeItemList, string factId)
            {
                for (int i = 0; i < sdeItemList.Count; i++)
                {
                    var sdeItem = sdeItemList[i];
                    if (sdeItem.Id == factId)
                    {
                        return true;
                    }
                }
                return false;
            }



        }

        /// <summary>
        /// Properties of a single Structured Data Element
        /// </summary>
        [DataContract]
        public class SdeItem
        {
            [DataMember]
            public string Id { get; set; } = "";

            [DataMember]
            public string SyncStatus { get; set; } = "";

            [DataMember]
            public string Value { get; set; } = "";

            public SdeItem(string id, string syncStatus, string value)
            {
                this.Id = id;
                this.SyncStatus = syncStatus;
                this.Value = value;
            }
        }

        /// <summary>
        /// Properties of a single Site Structure Item
        /// </summary>
        [DataContract]
        public class SiteStructureItem
        {
            [DataMember]
            public string Id { get; set; } = "";

            [DataMember]
            public string Linkname { get; set; } = "";

            [DataMember]
            public string Ref { get; set; } = "";

            public SiteStructureItem(string id, string linkname, string dataRef)
            {
                this.Id = id;
                this.Linkname = linkname;
                this.Ref = dataRef;
            }
        }

        // Create a thread-safe list to store ValidationErrorDetails
        public static ConcurrentBag<ValidationErrorDetails> validationErrorDetailsList = [];

        public class ValidationErrorDetails
        {
            public string DataReference { get; set; }

            public string Lang { get; set; }

            public string? ProjectId { get; set; }

            public string UserId { get; set; }
            public List<string> ErrorLog { get; set; }

            public ValidationErrorDetails(ProjectVariables projectVars, string dataReference, List<string> errorLog)
            {
                ProjectId = projectVars.projectId;
                DataReference = dataReference;
                Lang = projectVars.outputChannelVariantLanguage;
                UserId = projectVars.currentUser.Id;
                ErrorLog = errorLog;
            }

            public ValidationErrorDetails(string projectId, string dataReference, string language, string userId, List<string> errorLog)
            {
                ProjectId = projectId;
                DataReference = dataReference;
                Lang = language;
                UserId = userId;
                ErrorLog = errorLog;
            }
        }

        /// <summary>
        /// Base class for keeping track of the state of a background task
        /// </summary>
        public class BackgroundTaskRunDetails
        {
            public List<string> Log { get; set; } = [];
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public bool Success { get; set; } = true;
            public long EpochStartTime { get; set; }
            public long EpochEndTime { get; set; }

            public double Progress { get; set; } = 0;

            public string ProjectId { get; set; } = "";

            public string RunId { get; set; } = string.Empty;

            public BackgroundTaskRunDetails()
            {
                this.StartTime = DateTime.Now;
                this.EpochStartTime = ToUnixTime(this.StartTime);
            }

            public virtual async Task Done(ProjectVariables projectVars, bool success)
            {
                await DummyAwaiter();
                this.Success = success;
                this.EndTime = DateTime.Now;
                this.EpochEndTime = ToUnixTime(this.EndTime);
            }

            public virtual async Task AddLog(string log)
            {
                await DummyAwaiter();
                this.Log.Add(log);
            }
        }







        /// <summary>
        /// Collection of metrics regaring tha state of the Taxxor Editor and Document Store
        /// </summary>
        public static class SystemState
        {

            public static class ActiveUsers
            {
                public static int Count
                {
                    get => _count;
                    set
                    {
                        _count = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(value);
                    }
                }
                private static int _count = 0;

                public static async Task UpdateClient(int count)
                {
                    await Extensions.UpdateActiveUsersInClient(count);
                }
            }

            public static class SdsSync
            {
                public static bool IsRunning
                {
                    get => _isRunning;
                    set
                    {
                        _isRunning = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(value, ProjectId, Progress, RunIds);
                    }
                }
                private static bool _isRunning = false;

                public static string? ProjectId
                {
                    get => _projectId;
                    set
                    {
                        _projectId = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(IsRunning, value, Progress, RunIds);
                    }
                }
                private static string? _projectId = null;

                public static double Progress
                {
                    get => _progress;
                    set
                    {
                        _progress = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(IsRunning, ProjectId, value, RunIds);
                    }
                }
                private static double _progress = 0;

                public static List<string> RunIds
                {
                    get => _runIds;
                    set
                    {
                        _runIds = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(IsRunning, ProjectId, Progress, RunIds);
                    }
                }
                private static List<string> _runIds = [];

                public async static Task Reset(bool updateRemote = false)
                {
                    IsRunning = false;
                    ProjectId = null;
                    Progress = 0;
                    RunIds = [];

                    if (updateRemote) await UpdateRemote();
                }


                public static async Task UpdateClient(bool isRunning, string? projectId, double progress, List<string> runIds)
                {
                    await Extensions.UpdateSyncAndImportSystemStatusInClient("SystemStateUpdateSdsSync", isRunning, projectId, progress, runIds);
                }
            }

            public static class ErpImport
            {
                public static bool IsRunning
                {
                    get => _isRunning;
                    set
                    {
                        _isRunning = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(value, ProjectId, Progress, RunIds);
                    }
                }
                private static bool _isRunning = false;

                public static string? ProjectId
                {
                    get => _projectId;
                    set
                    {
                        _projectId = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(IsRunning, value, Progress, RunIds);
                    }
                }
                private static string? _projectId = null;

                public static double Progress
                {
                    get => _progress;
                    set
                    {
                        _progress = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(IsRunning, ProjectId, value, RunIds);
                    }
                }
                private static double _progress = 0;

                public static List<string> RunIds
                {
                    get
                    {
                        // Fill the runIds list with the keys of the ErpImportStatus dictionary
                        return Extensions.GetErpImportRunIds(_runIds);
                    }
                    set
                    {
                        _runIds = value;
                        // Call SignalR method to update clients
                        _ = UpdateClient(IsRunning, ProjectId, Progress, value);
                    }
                }
                private static List<string> _runIds = [];

                public async static Task Reset(bool updateRemote = false)
                {
                    IsRunning = false;
                    ProjectId = null;
                    Progress = 0;
                    RunIds = [];

                    if (updateRemote) await UpdateRemote();
                }

                public static async Task UpdateClient(bool isRunning, string projectId, double progress, List<string> runIds)
                {
                    await Extensions.UpdateSyncAndImportSystemStatusInClient("SystemStateUpdateErpImport", isRunning, projectId, progress, runIds);
                }
            }

            public static XmlDocument ToXml()
            {
                var xmlDoc = new XmlDocument();
                var nodeState = xmlDoc.CreateElement("state");
                var nodeActiveUsers = xmlDoc.CreateElementWithText("activeusers", ActiveUsers.Count.ToString());
                _ = nodeState.AppendChild(nodeActiveUsers);
                var nodeErpSync = xmlDoc.CreateElementWithText("erpimportrunning", ErpImport.IsRunning.ToString().ToLower());
                nodeErpSync.SetAttribute("projectid", ErpImport.ProjectId);
                nodeErpSync.SetAttribute("progress", ErpImport.Progress.ToString());
                nodeErpSync.SetAttribute("runids", string.Join(",", ErpImport.RunIds));
                _ = nodeState.AppendChild(nodeErpSync);
                var nodeSdsDataSyncRunning = xmlDoc.CreateElementWithText("sdsdatasyncrunning", SdsSync.IsRunning.ToString().ToLower());
                nodeSdsDataSyncRunning.SetAttribute("projectid", SdsSync.ProjectId);
                nodeSdsDataSyncRunning.SetAttribute("progress", SdsSync.Progress.ToString());
                nodeSdsDataSyncRunning.SetAttribute("runids", string.Join(",", SdsSync.RunIds));
                _ = nodeState.AppendChild(nodeSdsDataSyncRunning);
                _ = xmlDoc.AppendChild(nodeState);
                return xmlDoc;
            }

            public static void FromXml(XmlDocument xmlDoc)
            {
                ActiveUsers.Count = Convert.ToInt32(xmlDoc.SelectSingleNode("/state/activeusers")?.InnerText);
                ErpImport.IsRunning = xmlDoc.SelectSingleNode("/state/erpimportrunning")?.InnerText == "true";
                ErpImport.ProjectId = xmlDoc.SelectSingleNode("/state/erpimportrunning")?.GetAttribute("projectid");
                ErpImport.Progress = Convert.ToDouble(xmlDoc.SelectSingleNode("/state/erpimportrunning")?.GetAttribute("progress") ?? "0");
                ErpImport.RunIds = xmlDoc.SelectSingleNode("/state/erpimportrunning")?.GetAttribute("runids")?.Split(',')?.ToList() ?? [];
                SdsSync.IsRunning = xmlDoc.SelectSingleNode("/state/sdsdatasyncrunning")?.InnerText == "true";
                SdsSync.ProjectId = xmlDoc.SelectSingleNode("/state/sdsdatasyncrunning")?.GetAttribute("projectid");
                SdsSync.Progress = Convert.ToDouble(xmlDoc.SelectSingleNode("/state/sdsdatasyncrunning")?.GetAttribute("progress") ?? "0");
                SdsSync.RunIds = xmlDoc.SelectSingleNode("/state/sdsdatasyncrunning")?.GetAttribute("runids")?.Split(',')?.ToList() ?? [];
            }

            public static string ToJson()
            {
                return JsonConvert.SerializeObject(ToDynamic());
            }

            public static dynamic ToDynamic()
            {
                dynamic systemState = new ExpandoObject();
                systemState.systemstate = new ExpandoObject();
                systemState.systemstate.activeusers = new ExpandoObject();
                systemState.systemstate.activeusers.count = ActiveUsers.Count;
                systemState.systemstate.sdssync = new ExpandoObject();
                systemState.systemstate.sdssync.isrunning = SdsSync.IsRunning;
                systemState.systemstate.sdssync.projectid = SdsSync.ProjectId;
                systemState.systemstate.sdssync.progress = SdsSync.Progress;
                systemState.systemstate.sdssync.runids = SdsSync.RunIds;
                systemState.systemstate.erpimport = new ExpandoObject();
                systemState.systemstate.erpimport.isrunning = ErpImport.IsRunning;
                systemState.systemstate.erpimport.projectid = ErpImport.ProjectId;
                systemState.systemstate.erpimport.progress = ErpImport.Progress;
                systemState.systemstate.erpimport.runids = ErpImport.RunIds;
                return systemState;
            }

#pragma warning disable CS0114 // Member hides inherited member; missing override keyword
            public static string ToString()
#pragma warning restore CS0114 // Member hides inherited member; missing override keyword
            {
                return $"ActiveUsers.Count: {ActiveUsers.Count} ,ErpImport.IsRunning: {ErpImport.IsRunning} (projectId: {ErpImport.ProjectId}), SdsSync.IsRunning: {SdsSync.IsRunning} (projectId: {SdsSync.ProjectId})";
            }

            /// <summary>
            /// Updates the remote object system state
            /// </summary>
            /// <returns></returns>
            public async static Task UpdateRemote()
            {
                var remoteServer = applicationId == "taxxoreditor" ? ConnectedServiceEnum.DocumentStore : ConnectedServiceEnum.Editor;

                var dataToPost = new Dictionary<string, string>
                {
                    { "data", ToXml().OuterXml }
                };

                var response = await CallTaxxorConnectedService<TaxxorReturnMessage>(remoteServer, RequestMethodEnum.Put, "systemstate", dataToPost);
                if (!response.Success)
                {
                    appLogger.LogWarning("Failed to update the remote system with the system state");
                }
            }
        }

    }

    /// <summary>
    /// Inspects a binary file and returns the extension based on the https://en.wikipedia.org/wiki/List_of_file_signatures
    /// </summary>
    public static class FileInspector
    {
        // some magic bytes for the most important image formats, see Wikipedia for more
        static readonly List<byte> jpg = [0xFF, 0xD8];
        static readonly List<byte> bmp = [0x42, 0x4D];
        static readonly List<byte> gif = [0x47, 0x49, 0x46];
        static readonly List<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        static readonly List<byte> svg_xml_small = [0x3C, 0x3F, 0x78, 0x6D, 0x6C]; // "<?xml"
        static readonly List<byte> svg_xml_capital = [0x3C, 0x3F, 0x58, 0x4D, 0x4C]; // "<?XML"
        static readonly List<byte> svg_small = [0x3C, 0x73, 0x76, 0x67]; // "<svg"
        static readonly List<byte> svg_capital = [0x3C, 0x53, 0x56, 0x47]; // "<SVG"
        static readonly List<byte> intel_tiff = [0x49, 0x49, 0x2A, 0x00];
        static readonly List<byte> motorola_tiff = [0x4D, 0x4D, 0x00, 0x2A];
        static readonly List<byte> gimp_tiff = [0x49, 0x49, 0x2B, 0x00];

        static readonly List<(List<byte> magic, string extension)> imageFormats =
        [
            (jpg, "jpg"),
            (bmp, "bmp"),
            (gif, "gif"),
            (png, "png"),
            (svg_small, "svg"),
            (svg_capital, "svg"),
            (intel_tiff,"tif"),
            (motorola_tiff, "tif"),
            (gimp_tiff, "tif"),
            (svg_xml_small, "svg"),
            (svg_xml_capital, "svg")
        ];

        public static string TryGetExtension(string filepath)
        {
            return FileInspector.TryGetExtension(System.IO.File.ReadAllBytes(filepath));
        }

        public static string TryGetExtension(Byte[] array)
        {
            // check for simple formats first
            foreach (var (magic, extension) in imageFormats)
            {
                if (array.IsImage(magic))
                {
                    if (magic != svg_xml_small && magic != svg_xml_capital)
                        return extension;

                    // special handling for SVGs starting with XML tag
                    int readCount = magic.Count; // skip XML tag
                    int maxReadCount = 1024;

                    do
                    {
                        if (array.IsImage(svg_small, readCount) || array.IsImage(svg_capital, readCount))
                        {
                            return extension;
                        }
                        readCount++;
                    }
                    while (readCount < maxReadCount && readCount < array.Length - 1);

                    return null;
                }
            }
            string hexBytes = string.Join(", ", array.Take(10).Select(b => $"0x{b:X2}"));
            Console.WriteLine($"Unrecognized file format: {hexBytes}");
            return null;
        }

        private static bool IsImage(this Byte[] array, List<byte> comparer, int offset = 0)
        {
            int arrayIndex = offset;
            foreach (byte c in comparer)
            {
                if (arrayIndex > array.Length - 1 || array[arrayIndex] != c)
                    return false;
                ++arrayIndex;
            }
            return true;
        }
    }
}