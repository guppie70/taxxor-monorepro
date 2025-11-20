# gRPC Migration Project

## Overview

This is a temporary monorepo combining **Editor** and **DocumentStore** services from the Taxxor Document Management (TDM) stack to migrate REST API communications to gRPC.

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
2. **Implement** using the 4-step pattern
3. **Compile** both solutions - must succeed!
4. **Clean up** REST code
5. **Commit** with clear message
6. **Update** `MIGRATION_PLAN.md` to mark batch complete

## Commit Convention

```
gRPC migration: [Batch X] - Method1, Method2, Method3

- Added [Service]Service to proto definitions
- Implemented gRPC server handlers
- Updated Editor clients to use gRPC
- Removed REST definitions and routing
- ✅ Both solutions compile successfully
```
