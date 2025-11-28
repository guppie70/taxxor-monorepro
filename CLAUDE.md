# gRPC Migration Monorepo

## Goal

Replace REST API interfaces between Editor (client) and DocumentStore (server) with gRPC using a **conservative, traffic-verified approach**.

**Current Progress**: Starting fresh with traffic recording approach. See `grpc-migration-status.md` for details.

---

## Lessons Learned (First Attempt)

The initial migration attempt revealed critical issues:

1. **Phantom Migrations** - gRPC code existed but REST was still being called. Only discovered when REST was removed and functionality broke.

2. **False Test Confidence** - Manual testing passed because REST was still active as a fallback, not because gRPC worked.

3. **Missing Verification Step** - No way to confirm gRPC was actually being used before cleanup.

**Key Insight**: Without recorded baseline traffic, you cannot verify that gRPC responses match REST responses.

---

## Conservative Migration Approach

### Overview

```
Phase 1: Setup Traffic Recording
    Editor → Recording Proxy → DocumentStore
                   ↓
            Capture all REST traffic

Phase 2: For Each Migration
    1. Implement gRPC (server + client)
    2. DISABLE REST endpoint (comment out, don't delete)
    3. Test - verify gRPC path is actually used
    4. Replay recorded traffic against gRPC
    5. Compare responses - must match REST baseline
    6. Only delete REST after verification passes
```

### Critical Rule: Disable Before Delete

```
WRONG (causes phantom migrations):
1. Implement gRPC
2. Test (unknowingly still hitting REST)
3. Delete REST → everything breaks

CORRECT:
1. Implement gRPC
2. DISABLE REST (comment out)
3. Test → proves gRPC is called
4. Replay recorded traffic → proves responses match
5. Delete REST → safe because already verified
```

---

## Development Workflow

### Standard Pattern

```bash
# 1. Make changes in monorepo
# 2. Verify compilation
dotnet build DocumentStore.sln && dotnet build Editor/TaxxorEditor.sln

# 3. Publish to Docker-mounted directories
bash ./split-from-monorepo.sh

# 4. Wait for Docker hot-reload (5-10 seconds)
# 5. Test your changes
# 6. Check logs if needed
docker logs tdm-documentstore-1 | tail -50
docker logs tdm-editor-1 | tail -50
```

### Automatic Feedback Loop

When a curl command is provided for testing, use this loop:
1. Implement the change
2. Build both solutions
3. Run `bash ./split-from-monorepo.sh`
4. Wait 5-10 seconds
5. Execute curl command (replace `https://editor/` with `https://editor:8071/`)
6. Check response - if failed, analyze and fix
7. Repeat until success

### Session Start

Start log monitoring in background:
```bash
docker logs -f --tail=100 tdm-documentstore-1 2>&1
docker logs -f --tail=100 tdm-editor-1 2>&1
```

---

## Folder Structure

```
/Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-monorepo/
├── Editor/                              # Editor service (gRPC client)
│   ├── TaxxorEditor/
│   │   └── backend/code/                # C# service code
│   │       └── TaxxorServicesFilingData.cs  # Main gRPC client methods
│   └── GrpcServices/Protos/             # Proto files (synced from DocumentStore)
│
├── DocumentStore/                       # DocumentStore service (gRPC server)
│   ├── DocumentStore/
│   │   ├── backend/
│   │   │   └── controllers/ApiDispatcher.cs  # REST routing (cleanup target)
│   │   └── hierarchies/base_structure.xml    # REST endpoint defs (cleanup target)
│   └── GrpcServices/
│       ├── Protos/taxxor_service.proto       # Proto definitions (SOURCE OF TRUTH)
│       └── Services/                         # gRPC service implementations
│
├── grpc-migration-status.md             # Migration progress tracker
├── grpc-migration-planning.md           # Remaining work and procedures
└── grpc-spec.md                         # gRPC specification reference
```

---

## Shared Code (MUST sync both locations)

These folders exist in **both** Editor and DocumentStore. Changes must be applied to **both**:

| Folder | Editor Path | DocumentStore Path |
|--------|-------------|-------------------|
| Framework | `Editor/TaxxorEditor/backend/framework` | `DocumentStore/DocumentStore/backend/framework` |
| Shared | `Editor/TaxxorEditor/backend/code/shared` | `DocumentStore/DocumentStore/backend/code/shared` |

### Proto Files (MUST sync manually)

| Source | Destination |
|--------|-------------|
| `DocumentStore/GrpcServices/Protos/taxxor_service.proto` | `Editor/GrpcServices/Protos/DocumentStore/taxxor_service.proto` |

**Edit in DocumentStore first**, then copy to Editor. Proto files are NOT auto-synced.

---

## Key Locations

| Purpose | Location |
|---------|----------|
| Proto definitions | `DocumentStore/GrpcServices/Protos/taxxor_service.proto` |
| gRPC server services | `DocumentStore/GrpcServices/Services/` |
| gRPC client code | `Editor/TaxxorEditor/backend/code/TaxxorServicesFilingData.cs` |
| REST cleanup (endpoints) | `DocumentStore/DocumentStore/hierarchies/base_structure.xml` |
| REST cleanup (routing) | `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs` |
| Editor Startup | `Editor/TaxxorEditor/Startup.cs` |
| DocumentStore Startup | `DocumentStore/DocumentStore/Startup.cs` |
| ConvertToGrpcProjectVariables | `Editor/TaxxorEditor/backend/code/_Project.cs:2232` |
| InitializeProjectVariablesForGrpc | `DocumentStore/DocumentStore/backend/code/_Project.cs:808` |

---

## gRPC Patterns

### All Requests Use GrpcProjectVariables

```proto
message YourRequest {
  GrpcProjectVariables grpcProjectVariables = 1;  // ALWAYS first
  string yourField = 2;
}
```

### All Responses Use TaxxorGrpcResponseMessage

```proto
message TaxxorGrpcResponseMessage {
  bool success = 1;
  string message = 2;      // User-facing
  string debuginfo = 3;    // Technical details
  string data = 4;         // XML/JSON payload
  bytes binary = 5;        // Binary data
}
```

### Server Pattern

```csharp
public override async Task<TaxxorGrpcResponseMessage> YourMethod(
    YourRequest request, ServerCallContext context)
{
    try
    {
        var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);
        // Business logic...
        return new TaxxorGrpcResponseMessage { Success = true, Data = result };
    }
    catch (Exception ex)
    {
        appLogger.LogError(ex, $"Error in YourMethod");
        return new TaxxorGrpcResponseMessage
        {
            Success = false,
            Message = ex.Message,
            Debuginfo = ex.ToString()
        };
    }
}
```

### Client Pattern

```csharp
var client = context.RequestServices.GetRequiredService<YourService.YourServiceClient>();
var grpcRequest = new YourRequest
{
    GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
    // Other fields...
};
var response = await client.YourMethodAsync(grpcRequest);

if (response.Success)
{
    // Parse response.Data
}
else
{
    appLogger.LogError($"Could not [operation] (message: {response.Message}, debuginfo: {response.Debuginfo})");
}
```

---

## REST Cleanup Checklist

After migrating a method, cleanup must occur in **both** Editor and DocumentStore. See `rest-architecture.md` for complete details.

### Editor Cleanup
1. **Find all callers** of the REST endpoint:
   ```bash
   grep -rn '"endpoint-id"' Editor/TaxxorEditor/backend/code/
   ```
2. **Update each caller** to use gRPC (wrapper method or direct client call)

### DocumentStore Cleanup
1. **Remove from base_structure.xml** - Delete the `<item id="endpoint-id">` block
2. **Remove from ApiDispatcher.cs** - Delete the `case "endpoint-id":` block
3. **Evaluate handler method** - Check if it's ONLY called from ApiDispatcher:
   ```bash
   grep -rn "HandlerMethodName" DocumentStore/DocumentStore/backend/
   ```
   - If only ApiDispatcher calls it → safe to remove
   - If other code calls it → keep it (utility function)
4. **NEVER remove utility methods** in `backend/framework/` - these are shared utilities

### Verification
```bash
grep -rn "endpoint-id" DocumentStore/
grep -rn "endpoint-id" Editor/
# Both should return empty after cleanup
```

---

## Known Issues

### Proto Changes Not Regenerating C#

If proto file changes don't trigger proper C# regeneration:
```bash
# Restart containers (in this order)
docker restart tdm-documentstore-1
docker restart tdm-editor-1
```
The entrypoint contains `dotnet clean` for full rebuild.

### URL Replacement

When testing with provided curl commands:
- Replace `https://editor/` with `https://editor:8071/`

---

## Session End

Update these files before ending session:
- `grpc-migration-status.md` - Update progress and completed items
- `grpc-migration-planning.md` - Update remaining work and notes

---

## Quick Commands

```bash
# Build both solutions
dotnet build DocumentStore.sln && dotnet build Editor/TaxxorEditor.sln

# Publish changes
bash ./split-from-monorepo.sh

# Check container logs
docker logs tdm-documentstore-1 --tail 50
docker logs tdm-editor-1 --tail 50

# Restart containers if needed
docker restart tdm-documentstore-1 tdm-editor-1

# Find all REST calls to DocumentStore (Editor side)
grep -rn 'CallTaxxorDataService\|CallTaxxorConnectedService.*DocumentStore' Editor/TaxxorEditor/backend/code/

# Find specific endpoint usage
grep -rn '"endpoint-id"' Editor/TaxxorEditor/backend/code/

# List all REST endpoints in DocumentStore
grep -n 'item id=' DocumentStore/DocumentStore/hierarchies/base_structure.xml
grep -n 'case "' DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs
```

---

## Reference Documents

- **REST architecture**: `rest-architecture.md` - How REST works between Editor and DocumentStore (essential for cleanup)
- **Full gRPC spec**: `grpc-spec.md`
- **Migration status**: `grpc-migration-status.md`
- **Planning/procedures**: `grpc-migration-planning.md`
