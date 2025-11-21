# gRPC Migration Project

## Overview

This is a temporary monorepo combining **Editor** and **DocumentStore** services from the Taxxor Document Management (TDM) stack to migrate REST API communications to gRPC.

## 🤖 Automatic Debugging

When you report issues, Claude Code automatically uses the `superpowers:systematic-debugging` skill. Just report the issue naturally (e.g., "I'm testing batch 3 and getting an error...") and the systematic debugging workflow will execute automatically.

## ⚠️ CRITICAL: Publishing Changes to Docker-Mounted Directories

**The Docker containers mount the SEPARATE Editor and DocumentStore directories, NOT this monorepo.**

After implementing changes, publish them using:
```bash
./split-from-monorepo.sh
```

**Quick Workflow:**
```bash
# 1. Make changes in monorepo
# 2. Verify compilation: dotnet build DocumentStore.sln && dotnet build Editor/TaxxorEditor.sln
# 3. Publish: ./split-from-monorepo.sh
# 4. Wait 5-10 seconds for Docker hot-reload
# 5. Test your changes
# 6. Check logs if needed: docker logs tdm-documentstore-1 | tail -50
```

**Key Points:**
- ✅ Always compile locally first (catch errors before Docker)
- ✅ Wait 5-10 seconds after sync before testing
- ✅ Docker auto-rebuilds with `dotnet watch run --no-hot-reload`
- ⚠️ Don't skip the split script - changes won't reach Docker otherwise
- ⚠️ Don't manually copy files - use the split script for consistency

## Project Structure

```
/
├── Editor/                         # Taxxor Editor service (Client)
│   ├── TaxxorEditor/
│   │   └── backend/
│   │       └── code/              # C# service client code
│   │           └── TaxxorServicesFilingData.cs  # Main migration target
│   ├── GrpcServices/              # Editor's gRPC client definitions
│   │   └── Protos/                # Proto files (synced from DocumentStore)
│   └── TaxxorEditor.sln           # Solution file
│
├── DocumentStore/                  # Taxxor DocumentStore service (Server)
│   ├── DocumentStore/
│   │   ├── backend/
│   │   │   └── controllers/
│   │   │       └── ApiDispatcher.cs  # REST endpoint routing (to be cleaned up)
│   │   └── hierarchies/
│   │   │   └── base_structure.xml    # REST endpoint definitions (to be cleaned up)
│   ├── GrpcServices/              # DocumentStore's gRPC server definitions
│   │   ├── Protos/
│   │   │   └── taxxor_service.proto  # Proto definitions (SOURCE OF TRUTH)
│   │   └── Services/              # gRPC service implementations
│   └── DocumentStore.sln          # Solution file
│
└── MIGRATION_PLAN.md              # Track batch progress (UPDATE THIS!)
```

## ⚠️ CRITICAL: Syncing Shared Code Between Services

**BOTH Editor and DocumentStore share identical copies of framework and shared code files. When you modify ANY shared file, you MUST sync the changes to BOTH services.**

### Shared Files That Require Syncing

**Backend Framework Files** (in `backend/code/shared/` or `backend/framework/`):
- `Git.cs` - Git operations and commit handling
- `TaxxorUser.cs` - User classes (AppUser, AppUserTaxxor)
- `Framework.cs` - Core framework classes
- `RequestVariables.cs` - Request context
- Any other files in `backend/code/shared/` or `backend/framework/`

**GrpcServices Proto Files**:
- **Source of Truth**: `DocumentStore/GrpcServices/Protos/*.proto`
- **Auto-synced to**: `Editor/GrpcServices/Protos/` (via Gulp watcher)
- **Rule**: Only edit proto files in DocumentStore; Editor's are gitignored and auto-synced

### Syncing Procedure

**When you modify a shared C# file:**

1. **Identify the file location** in both services:
   - DocumentStore: `DocumentStore/DocumentStore/backend/code/shared/FileName.cs`
   - Editor: `Editor/TaxxorEditor/backend/code/shared/FileName.cs`

2. **Apply the EXACT same changes** to both files

3. **Verify compilation** for BOTH services:
   ```bash
   dotnet build DocumentStore.sln
   dotnet build Editor/TaxxorEditor.sln
   ```

4. **Restart BOTH containers** if the changes affect runtime:
   ```bash
   docker restart tdm-documentstore-1
   docker restart tdm-editor-1
   ```

### Common Shared Files

| File | Location Pattern | Notes |
|------|------------------|-------|
| Git.cs | `backend/code/shared/Git.cs` | Git commit operations |
| TaxxorUser.cs | `backend/code/shared/TaxxorUser.cs` | User management |
| Framework.cs | `backend/framework/Framework.cs` | Core framework |
| RequestVariables.cs | `backend/framework/RequestVariables.cs` | Request context |
| User.cs | `backend/framework/User.cs` | AppUser base class |

### Quick Check Command

```bash
# Compare a shared file between services
diff DocumentStore/DocumentStore/backend/code/shared/Git.cs \
     Editor/TaxxorEditor/backend/code/shared/Git.cs

# No output = files are identical (good!)
# Output = files differ (need to sync!)
```

**Remember**: Failure to sync shared files will cause runtime errors in one service that are difficult to diagnose!

## Standard Response Format

**ALL** gRPC methods must return `TaxxorGrpcResponseMessage`:

```proto
message TaxxorGrpcResponseMessage {
  bool success = 1;        // Operation success status
  string message = 2;      // User-facing message
  string debuginfo = 3;    // Technical details for debugging
  string data = 4;         // JSON or XML string data
  bytes binary = 5;        // Binary data (files, etc.)
}
```

## Standard Project Variables

Use `GrpcProjectVariables` to pass context:

```proto
message GrpcProjectVariables {
  string userId = 1;
  string projectId = 2;
  string versionId = 3;
  string did = 5;
  string editorId = 6;
  string editorContentType = 7;
  string reportTypeId = 8;
  string outputChannelType = 9;
  string outputChannelVariantId = 10;
  string outputChannelVariantLanguage = 11;
}
```

## ⚠️ CRITICAL: ProjectVariables Initialization Pattern for gRPC

**ALL gRPC server handlers MUST use the centralized `InitializeProjectVariablesForGrpc` helper method** to initialize ProjectVariables. This ensures consistency with REST middleware behavior and prevents path calculation issues.

### The Centralized Helper Method

**Location**: `DocumentStore/DocumentStore/backend/code/_Project.cs:806`

**Signature**:
```csharp
public static ProjectVariables InitializeProjectVariablesForGrpc(IMapper mapper, object source)
```

**What it does**:
1. Uses AutoMapper to map from `GrpcProjectVariables` (or any request containing them) to `ProjectVariables`
2. Calls `FillCorePathsInProjectVariables()` to calculate all derived path properties:
   - `cmsDataRootPath` - Web path to project data root
   - `cmsDataRootBasePathOs` - OS path to project data root base
   - `cmsDataRootPathOs` - Full OS path including project ID and version (e.g., `/mnt/data/projects/project-name/ar24/version_1`)
   - `cmsContentRootPathOs` - OS path to content folder
   - `reportingPeriod` - Project reporting period
   - `outputChannelDefaultLanguage` - Default output channel language

### ✅ ALWAYS Use This Pattern in gRPC Handlers

**Correct pattern** (use in ALL gRPC service implementations):

```csharp
public override async Task<TaxxorGrpcResponseMessage> YourMethod(
    YourRequest request, ServerCallContext context)
{
    try
    {
        // ✅ CORRECT: Use centralized helper
        var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

        // Now projectVars has ALL paths calculated and is ready to use
        var xmlPath = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, request.Did, false);
        // ... rest of implementation
    }
    catch (Exception ex)
    {
        // Error handling
    }
}
```

### ❌ NEVER Use These Anti-Patterns

**Anti-pattern 1: Direct AutoMapper without path calculation**
```csharp
// ❌ WRONG: Missing path calculations
var projectVars = _mapper.Map<ProjectVariables>(request);
// projectVars.cmsDataRootPathOs is NULL or incomplete!
```

**Anti-pattern 2: Manual path initialization**
```csharp
// ❌ WRONG: Duplicating logic, incomplete initialization
var projectVars = _mapper.Map<ProjectVariables>(request);
if (string.IsNullOrEmpty(projectVars.cmsDataRootPathOs))
{
    projectVars.cmsDataRootPathOs = RetrieveFilingDataRootFolderPathOs(projectVars.projectId);
}
// Missing other path properties like cmsContentRootPathOs, reportingPeriod, etc.
```

**Anti-pattern 3: Using FillCorePathsInProjectVariables directly**
```csharp
// ❌ WRONG: Correct but not centralized - use the helper instead!
var projectVars = _mapper.Map<ProjectVariables>(request);
FillCorePathsInProjectVariables(ref projectVars);
// Works, but violates DRY principle - use InitializeProjectVariablesForGrpc instead
```

### Why This Pattern Matters

**Path calculation issues** are the most common cause of gRPC handler failures:

- Missing `cmsDataRootPathOs` → File operations fail with incomplete paths like `/textual/file.xml` instead of `/mnt/data/projects/ar24/version_1/textual/file.xml`
- Missing `cmsContentRootPathOs` → Content operations fail
- Missing `reportingPeriod` → Report generation fails
- Inconsistent behavior between REST and gRPC endpoints

**The centralized helper ensures**:
- ✅ Consistent initialization across all gRPC handlers
- ✅ Backward compatibility with REST middleware behavior
- ✅ All derived path properties are calculated
- ✅ Single source of truth for initialization logic
- ✅ DRY principle (Don't Repeat Yourself)

### Implementation Checklist

When implementing a new gRPC handler:

1. ✅ Add `private readonly IMapper _mapper;` field to service class (if not present)
2. ✅ Inject `IMapper mapper` in constructor (if not present)
3. ✅ Use `InitializeProjectVariablesForGrpc(_mapper, request)` to initialize ProjectVariables
4. ✅ NEVER use direct `_mapper.Map<ProjectVariables>(request)` without the helper
5. ✅ NEVER manually initialize path properties

### Example: Correct Implementation

See any method in `DocumentStore/GrpcServices/Services/FilingComposerDataService.cs`:

```csharp
public class FilingComposerDataService : Protos.FilingComposerDataService.FilingComposerDataServiceBase
{
    private readonly RequestContext _requestContext;
    private readonly IMapper _mapper;  // ← Required for helper method

    public FilingComposerDataService(RequestContext requestContext, IMapper mapper)
    {
        _requestContext = requestContext;
        _mapper = mapper;  // ← Injected via DI
    }

    public override async Task<TaxxorGrpcResponseMessage> SaveSourceData(
        SaveSourceDataRequest request, ServerCallContext context)
    {
        try
        {
            // ✅ CORRECT: One line initialization with all paths calculated
            var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

            // projectVars is now fully initialized and ready to use
            var xmlPath = RetrieveInlineFilingComposerXmlPathOs(reqVars, projectVars, request.Did, false);
            // ... rest of implementation
        }
        catch (Exception ex)
        {
            return new TaxxorGrpcResponseMessage
            {
                Success = false,
                Message = $"Error in SaveSourceData: {ex.Message}",
                Debuginfo = $"stack-trace: {GetStackTrace()}"
            };
        }
    }
}
```

### AutoMapper Configuration

The helper method works because AutoMapper is configured (in `DocumentStore/DocumentStore/Models/AutoMapper.cs`) to handle:

- `GetFilingComposerDataRequest` → `ProjectVariables`
- `SaveSourceDataRequest` → `ProjectVariables`
- `DeleteSourceDataRequest` → `ProjectVariables`
- `CreateSourceDataRequest` → `ProjectVariables`
- `GrpcProjectVariables` → `ProjectVariables` (direct mapping)
- `object` → `ProjectVariables` (generic fallback)

The helper method accepts `object source` so it works with **any** request type that contains `GrpcProjectVariables`.

## Migration Pattern: 4 Steps

### Step 1: Define Proto Service and Messages

In `DocumentStore/GrpcServices/Protos/taxxor_service.proto`:

```proto
service FilingDataService {
  rpc SaveSourceData (SaveSourceDataRequest) returns (TaxxorGrpcResponseMessage);
}

message SaveSourceDataRequest {
  GrpcProjectVariables grpcProjectVariables = 1;
  string data = 2;
  string did = 3;
  string contentLanguage = 4;
}
```

**Rules:**
- Use descriptive message names ending in `Request`
- Include `GrpcProjectVariables` as first field when needed
- Always return `TaxxorGrpcResponseMessage`

### Step 2: Implement Server-Side Handler

In `DocumentStore/GrpcServices/Services/`, create or update the service implementation.

**Rules:**
- Implement the gRPC service interface
- Return `TaxxorGrpcResponseMessage` with appropriate fields
- Set `success = true` for successful operations
- Set `success = false` and populate `message` and `debuginfo` for errors

#### ⚠️ CRITICAL: Register the gRPC Service in DocumentStore Startup.cs

**After implementing the service**, you MUST register it in the DocumentStore's endpoint configuration.

**Location**: `DocumentStore/DocumentStore/Startup.cs` in the `Configure` method (around line 179-181)

**Add this registration**:
```csharp
endpoints.MapGrpcService<YourService>();
```

**Example**:
```csharp
endpoints.MapGrpcService<FilingDataService>();
```

**If you forget this step**, you'll get a runtime error:
```
Grpc.Core.RpcException: Status(StatusCode="Unimplemented", Detail="Service is unimplemented.")
```

**After adding the registration**: Restart the DocumentStore Docker container:
```bash
docker restart tdm-documentstore-1
```

### Step 3: Update Client Code

In `Editor/TaxxorEditor/backend/code/`, update the method:

```csharp
public static async Task<XmlDocument> SaveSourceData(
    ProjectVariables projectVars,
    XmlDocument xmlDoc,
    string id,
    string contentLanguage,
    bool debugRoutine = false)
{
    try
    {
        // Get the gRPC client service via DI
        var client = System.Web.Context.Current.RequestServices
            .GetRequiredService<FilingDataService.FilingDataServiceClient>();
        
        // Create the request
        var grpcRequest = new SaveSourceDataRequest
        {
            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
            Data = xmlDoc.OuterXml,
            Did = id,
            ContentLanguage = contentLanguage
        };

        // Call the gRPC service
        var grpcResponse = await client.SaveSourceDataAsync(grpcRequest);

        if (grpcResponse.Success)
        {
            // Handle success - parse data as needed
            return ParseSuccessResponse(grpcResponse.Data);
        }
        else
        {
            // Handle error
            return GenerateErrorXml(grpcResponse.Message, 
                $"debuginfo: {grpcResponse.Debuginfo}");
        }
    }
    catch (Exception ex)
    {
        return GenerateErrorXml($"Error in SaveSourceData: {ex.Message}", 
            $"stack-trace: {GetStackTrace()}");
    }
}
```

**Rules:**
- Get gRPC client from DI (don't create new instances)
- Use helper methods: `ConvertToGrpcProjectVariables()`, `GenerateErrorXml()`
- Always wrap in try-catch
- Handle both success and error cases

#### ⚠️ CRITICAL: Register the gRPC Client in Startup.cs

**Before the client code will work**, you MUST register the gRPC client in the Editor's dependency injection container.

**Location**: `Editor/TaxxorEditor/Startup.cs` in the `ConfigureServices` method (around line 173-186)

**Add this registration**:
```csharp
services.AddGrpcClient<YourService.YourServiceClient>(o =>
{
    o.Address = new Uri("https://documentstore:4813");
});
```

**Example**:
```csharp
services.AddGrpcClient<FilingDataService.FilingDataServiceClient>(o =>
{
    o.Address = new Uri("https://documentstore:4813");
});
```

**If you forget this step**, you'll get a runtime error:
```
System.InvalidOperationException: No service for type 'DocumentStore.Protos.YourService+YourServiceClient' has been registered.
```

**After adding the registration**: Restart the Editor Docker container:
```bash
docker restart tdm-editor-1
```

### Step 4: Clean Up REST Code

**CRITICAL**: After migration, remove obsolete REST code:

1. **Remove XML endpoint definition** from `DocumentStore/DocumentStore/hierarchies/base_structure.xml`
2. **Remove C# routing** from `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs`

Look for REST calls like:
```csharp
// OLD - Remove this pattern:
await CallTaxxorConnectedService<XmlDocument>(
    ConnectedServiceEnum.DocumentStore, 
    RequestMethodEnum.Post, 
    "taxxoreditorcomposerdata", 
    dataToPost, 
    debugRoutine
);
```

## Code Quality Guidelines for Migration

### Eliminate Ambient Context Pattern

During migration, **actively refactor** methods to remove dependencies on `System.Web.Context.Current`. This modernizes the codebase and aligns with industry best practices.

**❌ Avoid (Service Locator / Ambient Context):**
```csharp
public static async Task<XmlDocument> MyMethod(string projectId, string versionId)
{
    // Hidden dependency on static context - implicit coupling
    var context = System.Web.Context.Current;
    RequestVariables reqVars = RetrieveRequestVariables(context);  // Often unused
    ProjectVariables projectVars = RetrieveProjectVariables(context);
    // ... uses projectVars but it's a hidden dependency
}
```

**✅ Prefer (Explicit Dependency Injection):**
```csharp
public static async Task<XmlDocument> MyMethod(ProjectVariables projectVars)
{
    // Explicit dependency - clear what the method needs
    // No hidden coupling to HTTP context
    var context = System.Web.Context.Current;  // Only if truly needed for DI
    // ...
}
```

**Benefits:**
- **Explicit contracts** - Method signatures reveal all dependencies
- **Improved testability** - Easy to test without mocking HTTP infrastructure
- **Loose coupling** - Business logic independent of ASP.NET framework
- **Better maintainability** - Dependencies are visible and traceable
- **Thread-safety** - No reliance on ambient static context in async scenarios

**Migration Checklist:**

When migrating a method:
1. ✅ Check if it retrieves `ProjectVariables` or `RequestVariables` from context
2. ✅ If it only uses `ProjectVariables`, change signature to accept it as a parameter
3. ✅ Remove unused `RequestVariables reqVars = RetrieveRequestVariables(context);` lines
4. ✅ Update all call sites to pass `projectVars` explicitly
5. ✅ Remove unused parameter variables (e.g., `projectId`, `versionId` if getting from context)

**Professional Context:**

This refactoring represents a shift from:
- **Service Locator anti-pattern** → **Dependency Injection pattern**
- **Ambient context** → **Explicit dependencies**
- **Implicit coupling** → **Dependency Inversion Principle (SOLID)**

Modern .NET development emphasizes Dependency Injection as a first-class pattern (built into ASP.NET Core), pure functions that depend only on their inputs, and framework-independent business logic.

## Example: Already Migrated Method

See `LoadSourceData` in `Editor/TaxxorEditor/backend/code/TaxxorServicesFilingData.cs`:

```csharp
public static async Task<XmlDocument> LoadSourceData(
    FilingComposerDataService.FilingComposerDataServiceClient filingComposerDataClient,
    ProjectVariables projectVars,
    string projectId,
    string did,
    string versionId = "latest",
    bool debugRoutine = false)
{
    if (ValidateCmsPostedParameters(projectId, versionId, "text") && did != null)
    {
        var grpcRequest = new GetFilingComposerDataRequest
        {
            DataType = "text",
            Did = did,
            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars)
        };

        var grpcResponse = await filingComposerDataClient.GetFilingComposerDataAsync(grpcRequest);

        if (grpcResponse.Success)
        {
            var xmlSourceData = new XmlDocument();
            xmlSourceData.LoadXml(grpcResponse.Data);
            return xmlSourceData;
        }
        else
        {
            return GenerateErrorXml("Could not load the source data", 
                $"message: {grpcResponse.Message}, debuginfo: {grpcResponse.Debuginfo}");
        }
    }
    else
    {
        return GenerateErrorXml("Did not supply enough input", 
            $"stack-trace: {GetStackTrace()}");
    }
}
```

## Critical Rules

### ✅ DO

1. **Always verify compilation** after each batch:
   ```bash
   cd Editor && dotnet build TaxxorEditor.sln
   cd ../DocumentStore && dotnet build DocumentStore.sln
   ```

2. **Remove obsolete REST code** after successful migration
3. **Update MIGRATION_PLAN.md** to mark batches complete
4. **Follow the established pattern** shown in `LoadSourceData`
5. **Use helper methods** provided in the codebase
6. **Handle errors gracefully** with meaningful messages

### ❌ DON'T

1. **Don't commit** if compilation fails
2. **Don't skip cleanup** of REST code
3. **Don't create new gRPC client instances** - always use DI
4. **Don't deviate** from `TaxxorGrpcResponseMessage` format
5. **Don't forget** to update proto files first (they're the source of truth)

## Proto File Location

**SOURCE OF TRUTH**: `DocumentStore/GrpcServices/Protos/taxxor_service.proto`

The proto files in Editor are synced from DocumentStore via a Gulp watcher. Always edit the DocumentStore version.

## Helper Methods Reference

Available in `Editor/TaxxorEditor/backend/code/`:

| Method | Purpose |
|--------|---------|
| `ConvertToGrpcProjectVariables(projectVars)` | Convert ProjectVariables to proto format |
| `GenerateErrorXml(message, debug)` | Generate standard error response |
| `ValidateCmsPostedParameters(...)` | Validate required parameters |
| `GetStackTrace()` | Get current stack trace for debugging |

## File Locations Quick Reference

| Purpose | Location |
|---------|----------|
| Proto definitions (edit here) | `DocumentStore/GrpcServices/Protos/taxxor_service.proto` |
| gRPC server implementations | `DocumentStore/GrpcServices/Services/` |
| Editor client code | `Editor/TaxxorEditor/backend/code/` |
| REST endpoint XML (cleanup) | `DocumentStore/DocumentStore/hierarchies/base_structure.xml` |
| REST routing (cleanup) | `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs` |
| Migration progress | `MIGRATION_PLAN.md` |

## Workflow Summary

For each batch:

1. **Read** the next batch from `MIGRATION_PLAN.md`
2. **Implement** using the 4-step pattern in the monorepo
3. **Compile** both solutions - must succeed!
4. **Publish** changes to Docker-mounted directories: `./split-from-monorepo.sh`
5. **Test** in Docker (wait ~10 seconds for auto-rebuild)
6. **Clean up** REST code after successful testing
7. **Commit** with clear message (from the monorepo)
8. **Update** `MIGRATION_PLAN.md` to mark batch complete

## Debugging with Docker Logs

**Containers**: `tdm-editor-1` (Client), `tdm-documentstore-1` (Server)

**Essential Commands:**
```bash
# Monitor errors in real-time
docker logs -f tdm-documentstore-1 | grep -E "(error|Error|Exception|fail)"
docker logs -f tdm-editor-1 | grep -E "(error|Error|Exception|fail)"

# Check recent errors
docker logs --tail 100 tdm-documentstore-1 | grep -A 5 -B 5 "Exception"

# Check logs from last 5 minutes
docker logs --since 5m tdm-documentstore-1
```

**Common Error Patterns & Fixes:**
- `Status(StatusCode="Unimplemented")` → Service not registered in DocumentStore Startup.cs
- `No service for type '...' has been registered` → Client not registered in Editor Startup.cs
- `NullReferenceException` → Missing ProjectVariables initialization or path calculation
- `Connection refused` → Container not running or wrong port

**After modifying Startup.cs or service implementations, restart containers:**
```bash
docker restart tdm-documentstore-1
docker restart tdm-editor-1
```

## Commit Convention

```
gRPC migration: [Batch X] - Method1, Method2, Method3

- Added [Service]Service to proto definitions
- Implemented gRPC server handlers
- Updated Editor clients to use gRPC
- Removed REST definitions and routing
- ✅ Both solutions compile successfully
```
