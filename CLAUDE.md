# gRPC Migration Project

## Overview

This is a temporary monorepo combining **Editor** and **DocumentStore** services from the Taxxor Document Management (TDM) stack to migrate REST API communications to gRPC.

## ⚠️ CRITICAL: Publishing Changes to Docker-Mounted Directories

**The Docker containers mount the SEPARATE Editor and DocumentStore directories, NOT this monorepo.**

After implementing ANY changes in the monorepo, you **MUST** publish them to the actual target directories using the split script:

```bash
./split-from-monorepo.sh
```

### When to Run the Split Script

**Run the split script immediately after:**

1. ✅ Making any C# code changes in the monorepo
2. ✅ Modifying proto files
3. ✅ Updating service implementations
4. ✅ Changing client code
5. ✅ Modifying Startup.cs files
6. ✅ Any change that needs to be tested in Docker

### Workflow for Each Change

```bash
# 1. Make changes in the monorepo
vim DocumentStore/GrpcServices/Services/FilingDataService.cs

# 2. Verify compilation
dotnet build DocumentStore.sln

# 3. Publish changes to target directories
./split-from-monorepo.sh

# 4. Docker will auto-rebuild (wait ~10 seconds)
# No manual restart needed - dotnet watch handles it

# 5. Test your changes
# Open browser and test the functionality
```

### What the Split Script Does

The `split-from-monorepo.sh` script:

- ✅ Syncs **Editor/** from monorepo to `../Editor/`
- ✅ Syncs **DocumentStore/** from monorepo to `../DocumentStore/`
- ✅ Excludes monorepo-specific files (MIGRATION_PLAN.md, CLAUDE.md, etc.)
- ✅ Uses checksums to only copy actually changed files
- ✅ Shows which files were changed

### Docker Auto-Rebuild

The Docker containers run with `dotnet watch run --no-hot-reload`, which means:

- ✅ **Automatic recompilation** when C# files change
- ✅ **Automatic restart** after recompilation completes
- ⏱️ **Wait 5-15 seconds** for rebuild to complete before testing
- 📺 **Monitor logs** to see when rebuild finishes:
  ```bash
  docker logs -f tdm-documentstore-1 | grep "watch :"
  docker logs -f tdm-editor-1 | grep "watch :"
  ```

### Important Notes

⚠️ **The monorepo is ONLY for development** - Docker containers don't see monorepo changes until you run the split script

⚠️ **Always run split script before testing** - Otherwise your changes won't be visible to Docker

⚠️ **Don't manually copy files** - Use the split script to ensure consistency

✅ **Commit from the monorepo** - Keep all your work in the monorepo, split script is just for publishing

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

When testing gRPC migrations, **ALWAYS monitor both container logs** to get a complete picture of the communication flow.

### Container Names

- **Editor (Client)**: `tdm-editor-1`
- **DocumentStore (Server)**: `tdm-documentstore-1`

### Essential Debugging Commands

**Monitor both containers simultaneously during testing:**

```bash
# In separate terminal windows/panes:
docker logs -f tdm-editor-1 | grep -E "(fail|error|Error|Exception|info)"
docker logs -f tdm-documentstore-1 | grep -E "(fail|error|Error|Exception|info)"
```

**Check recent errors:**

```bash
# Editor errors
docker logs --tail 100 tdm-editor-1 | grep -A 10 -B 5 -E "(fail|error|Error|Exception)"

# DocumentStore errors
docker logs --tail 100 tdm-documentstore-1 | grep -A 10 -B 5 -E "(fail|error|Error|Exception)"
```

**Check logs from specific time:**

```bash
# Last 5 minutes
docker logs --since 5m tdm-editor-1
docker logs --since 5m tdm-documentstore-1

# Last hour
docker logs --since 1h tdm-documentstore-1
```

### Debugging Workflow

When encountering runtime errors:

1. **Reproduce the error** - Perform the action that causes the issue
2. **Check DocumentStore logs first** - Server-side errors show up here
3. **Check Editor logs** - Client-side perspective and gRPC call details
4. **Look for the error stack trace** - Note the file and line numbers
5. **Add debug logging if needed** - Insert `appLogger.LogInformation()` statements
6. **Restart container** - `docker restart tdm-documentstore-1` or `tdm-editor-1`
7. **Test again** - Reproduce and check logs for debug output

### Common Error Patterns

**NullReferenceException in DocumentStore:**
- Check if user context is populated: `reqVars.currentUser`
- Check if project variables are mapped correctly
- Verify middleware is running and populating context

**gRPC Communication Errors:**
- `Status(StatusCode="Unimplemented")` → Service not registered in DocumentStore Startup.cs
- `No service for type '...' has been registered` → Client not registered in Editor Startup.cs
- Connection refused → Container not running or wrong port

**Container Restart Required:**

After modifying these files, you **must** restart the container:

```bash
# Restart DocumentStore after changes to:
# - Startup.cs (service registration)
# - GrpcServices/Services/*.cs (service implementation)
docker restart tdm-documentstore-1

# Restart Editor after changes to:
# - Startup.cs (client registration)
# - backend/code/*.cs (client code)
docker restart tdm-editor-1
```

Wait 5-10 seconds after restart before testing to ensure the service is fully initialized.

### Log Analysis Tips

**Look for these patterns:**

- `[fail]` or `[error]` - Failed operations
- `[warn]` - Warnings that might indicate issues
- `[info]` - Informational messages (useful for tracing flow)
- `Exception:` - Stack traces with file:line information
- `DEBUG` - Custom debug logging you've added

**Trace a gRPC call through both services:**

1. Editor log: Request initiated → gRPC client call
2. DocumentStore log: gRPC request received → processing → response
3. Editor log: Response received → result processing

## Commit Convention

```
gRPC migration: [Batch X] - Method1, Method2, Method3

- Added [Service]Service to proto definitions
- Implemented gRPC server handlers
- Updated Editor clients to use gRPC
- Removed REST definitions and routing
- ✅ Both solutions compile successfully
```
