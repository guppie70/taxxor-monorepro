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

### Step 4: Clean Up REST Code (MANDATORY - NO EXCEPTIONS)

**⚠️ CRITICAL**: REST cleanup is NOT optional—it is a mandatory part of every batch completion. Leaving orphaned REST code undermines the migration effort and perpetuates legacy patterns.

**Why cleanup is mandatory:**
- Prevents technical debt accumulation from old bespoke code
- Eliminates confusing duplicate implementations
- Maintains clear architecture boundaries
- Ensures the gRPC migration is actually complete
- Makes future maintenance easier and cheaper
- Prevents developers from accidentally using old REST code

#### Cleanup Checklist (MUST complete all items)

**1. Remove XML Endpoint Definitions** from `DocumentStore/DocumentStore/hierarchies/base_structure.xml`

For each migrated method, find and delete the corresponding `<item>` entry:

```xml
<!-- REMOVE THIS ENTIRE BLOCK -->
<item id="endpoint-identifier">
    <web_page>
        <path>/api/taxxoreditor/filing/data/generic</path>
        <linkname>Loads or saves filing assets such as metadata, images, etc.</linkname>
    </web_page>
</item>
```

**Verification**: Search for the endpoint ID in base_structure.xml - it should NOT exist after cleanup.

**2. Remove C# Routing Cases** from `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs`

Delete the entire `case` statement(s) for migrated endpoints:

```csharp
// REMOVE THIS ENTIRE BLOCK
case "endpointname":
    switch (reqVars.method)
    {
        case RequestMethodEnum.Get:
            await RetrieveData(request, response, routeData);
            break;
        case RequestMethodEnum.Put:
            await StoreData(request, response, routeData);
            break;
        default:
            _handleMethodNotSupported(reqVars);
            break;
    }
    break;
```

**Verification**: The case statement should be completely gone. Search for the endpoint name in ApiDispatcher.cs - it should NOT exist.

**3. Remove Handler Methods** from `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs`

Find and delete the actual handler methods that were invoked by the removed case statements:

```csharp
// REMOVE THIS METHOD ENTIRELY
private async Task RetrieveFilingData(HttpRequest request, HttpResponse response, RouteData routeData)
{
    // ... old implementation ...
}
```

**Verification**: Search for the method name in ApiDispatcher.cs - it should NOT exist. If it's used anywhere else, keep it; if not, delete it.

**4. Remove REST Service Connectors** from `DocumentStore/DocumentStore/backend/code/shared/TaxxorServiceConnectors.cs`

If the old endpoint had a connector method, remove it:

```csharp
// REMOVE THIS METHOD
public static async Task<XmlDocument> CallOldRestEndpoint(...)
{
    // Old REST API call
}
```

**Same for Editor**: `Editor/TaxxorEditor/backend/code/shared/TaxxorServiceConnectors.cs`

**Verification**: The old connector method should NOT exist in either service. The Editor should use the new gRPC client instead.

**5. Search and Verify Cleanup**

After making changes, verify NO traces remain:

```bash
# Search in ApiDispatcher.cs
grep -n "taxxoreditorfilingdata\|RetrieveFilingData\|StoreFilingData" \
    DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs

# Search in base_structure.xml
grep -n "taxxoreditorfilingdata" \
    DocumentStore/DocumentStore/hierarchies/base_structure.xml

# Search in both TaxxorServiceConnectors.cs files
grep -n "OldEndpointName" \
    DocumentStore/DocumentStore/backend/code/shared/TaxxorServiceConnectors.cs
grep -n "OldEndpointName" \
    Editor/TaxxorEditor/backend/code/shared/TaxxorServiceConnectors.cs

# If no output = cleanup successful!
# If output exists = cleanup incomplete, continue removing!
```

#### Cleanup Validation Workflow

**Before committing:**
1. ✅ Run all grep searches above - zero results required
2. ✅ Compile both solutions successfully:
   ```bash
   dotnet build DocumentStore.sln
   dotnet build Editor/TaxxorEditor.sln
   ```
3. ✅ Test in Docker to verify gRPC replacement works
4. ✅ Verify no compilation warnings about unused methods

**If validation fails:**
- ❌ Do NOT commit
- ❌ Do NOT mark batch as complete
- ❌ Do NOT move to next batch
- Find and remove remaining orphaned code before proceeding

#### Common Cleanup Mistakes (Avoid These!)

**❌ Mistake 1: Partial Cleanup**
- Removing the case statement but leaving the handler method
- Removing XML but leaving C# routing
- **Fix**: Complete the entire checklist, not just part of it

**❌ Mistake 2: Searching for Just Method Names**
- Misses XML endpoint definitions
- Misses service connector methods
- **Fix**: Use the full verification grep commands above

**❌ Mistake 3: "I'll Clean Up Later"**
- Never happened in the history of software projects
- Creates technical debt that gets worse
- **Fix**: Clean up in the SAME batch, before moving to next batch

**❌ Mistake 4: Keeping "Just in Case" Code**
- "Someone might still need the old REST code"
- gRPC is already in place and tested
- **Fix**: DELETE it. If we need it back, git has the history.

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

### ✅ DO (MANDATORY)

1. **ALWAYS verify compilation** after each batch:
   ```bash
   cd Editor && dotnet build TaxxorEditor.sln
   cd ../DocumentStore && dotnet build DocumentStore.sln
   ```

2. **MUST REMOVE obsolete REST code** - this is not optional:
   - ✅ Remove XML endpoint definitions from `base_structure.xml`
   - ✅ Remove C# case statements from `ApiDispatcher.cs`
   - ✅ Remove handler methods from `ApiDispatcher.cs`
   - ✅ Remove REST service connectors from `TaxxorServiceConnectors.cs` (both services)
   - ✅ Run cleanup verification grep commands
   - ✅ See detailed cleanup checklist in Step 4 above

3. **MUST UPDATE MIGRATION_PLAN.md** to mark batches complete with commit hash

4. **ALWAYS follow the established pattern** shown in `LoadSourceData`

5. **ALWAYS use helper methods** provided in the codebase

6. **ALWAYS handle errors gracefully** with meaningful messages

### ❌ DON'T (THESE ARE DEAL-BREAKERS)

1. **Don't commit** if compilation fails
2. **Don't skip or defer cleanup** of REST code - cleanup is part of the same batch, not "later"
3. **Don't create new gRPC client instances** - always use DI
4. **Don't deviate** from `TaxxorGrpcResponseMessage` format
5. **Don't forget** to update proto files first (they're the source of truth)
6. **Don't commit** with orphaned REST code still in the repository
7. **Don't mark batches as "complete"** without full cleanup verification

### Why This Matters

**Legacy code never cleans itself up.** The bespoke REST implementations were written years ago and created the very technical debt that made this migration necessary. If we don't actively remove them during migration, we create a codebase with:

- Two implementations of each method (REST + gRPC)
- Developer confusion about which to use
- Hidden bugs from maintaining duplicate code
- Years of technical debt accumulation
- Higher costs for future development

**This migration is only complete when the old code is gone.** Leaving REST code behind means the migration isn't actually finished—it's just added gRPC alongside the old patterns.

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

**For each batch: Implementation → Testing → Cleanup → Commit (NO SKIPPING)**

1. **Read** the next batch from `MIGRATION_PLAN.md`
2. **Implement** using the 4-step pattern in the monorepo
3. **Compile** both solutions - must succeed!
4. **Publish** changes to Docker-mounted directories: `./split-from-monorepo.sh`
5. **Test** in Docker (wait ~10 seconds for auto-rebuild)
6. **Clean up REST code** (MANDATORY - see Step 4 Cleanup Checklist):
   - Remove XML endpoint definitions from `base_structure.xml`
   - Remove C# case statements from `ApiDispatcher.cs`
   - Remove handler methods from `ApiDispatcher.cs`
   - Remove REST service connectors from `TaxxorServiceConnectors.cs` (both services)
   - Run verification grep commands - zero results required
   - Recompile both solutions
7. **Commit** with clear message (from the monorepo)
8. **Update** `MIGRATION_PLAN.md` to mark batch complete with commit hash

**CRITICAL**: Do NOT skip step 6. Cleanup is part of the batch, not "something to do later."

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
