# REST Cleanup Checklist - Mandatory for All Batches

**⚠️ This is a MANDATORY requirement for every batch. Cleanup is not optional and must be completed before committing.**

## Quick Summary

After implementing gRPC migration for any batch, you MUST remove all orphaned REST code before committing:

1. ❌ Remove XML endpoint definitions from `base_structure.xml`
2. ❌ Remove C# routing cases from `ApiDispatcher.cs`
3. ❌ Remove handler methods from `ApiDispatcher.cs`
4. ❌ Remove REST service connectors from both `TaxxorServiceConnectors.cs` files
5. ✅ Verify cleanup with grep commands
6. ✅ Recompile and test

**DO NOT COMMIT without completing ALL steps.**

---

## Step-by-Step Cleanup Procedure

### Step 1: Identify Methods to Clean

For each method in your completed batch, note:
- The gRPC method name (e.g., `SaveSourceData`)
- The REST endpoint ID (e.g., `taxxoreditorfilingdata`)
- The REST handler methods (e.g., `RetrieveFilingData`, `StoreFilingData`)

Example from Batch 1:
- gRPC: `SaveSourceData` → REST endpoint: `taxxoreditorfilingdata` → handler: `StoreFilingData()`
- gRPC: `DeleteSourceData` → REST endpoint: `taxxoreditorcomposerdataextended` → handler: (needs research)
- gRPC: `CreateSourceData` → REST endpoint: `taxxoreditorfilingdata` → handler: `StoreFilingData()` (shared)

### Step 2: Remove XML Endpoint Definitions

**File**: `DocumentStore/DocumentStore/hierarchies/base_structure.xml`

For each endpoint, find and delete the entire `<item>` block:

```xml
<!-- Search for and DELETE this entire block -->
<item id="taxxoreditorfilingdata">
    <web_page>
        <path>/api/taxxoreditor/filing/data/generic</path>
        <linkname>Loads or saves filing assets such as metadata, images, etc.</linkname>
    </web_page>
</item>
```

**Verification**:
```bash
grep -n "taxxoreditorfilingdata" DocumentStore/DocumentStore/hierarchies/base_structure.xml
# Result: (no output = success)
```

### Step 3: Remove C# Routing Case Statements

**File**: `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs`

Find the case statement for your endpoint and DELETE the entire block:

```csharp
// DELETE THIS ENTIRE BLOCK
case "taxxoreditorfilingdata":
    switch (reqVars.method)
    {
        case RequestMethodEnum.Get:
            await RetrieveFilingData(request, response, routeData);
            break;

        case RequestMethodEnum.Put:
            await StoreFilingData(request, response, routeData);
            break;

        default:
            _handleMethodNotSupported(reqVars);
            break;
    }
    break;
```

**Verification**:
```bash
grep -n "case \"taxxoreditorfilingdata\"" \
    DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs
# Result: (no output = success)
```

### Step 4: Remove Handler Methods

**File**: `DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs`

Find each handler method invoked by the removed case statement and DELETE it:

```csharp
// DELETE THIS METHOD ENTIRELY
private async Task RetrieveFilingData(HttpRequest request, HttpResponse response, RouteData routeData)
{
    try
    {
        // ... old REST implementation ...
    }
    catch (Exception ex)
    {
        // error handling
    }
}

// DELETE THIS METHOD ENTIRELY
private async Task StoreFilingData(HttpRequest request, HttpResponse response, RouteData routeData)
{
    try
    {
        // ... old REST implementation ...
    }
    catch (Exception ex)
    {
        // error handling
    }
}
```

**Verification**:
```bash
grep -n "private async Task RetrieveFilingData\|private async Task StoreFilingData" \
    DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs
# Result: (no output = success)
```

**Important**: Only delete methods that are NOT used by other endpoints. If a handler is called by multiple case statements, leave it until ALL endpoints using it are migrated.

### Step 5: Remove REST Service Connectors

**Files**:
- `DocumentStore/DocumentStore/backend/code/shared/TaxxorServiceConnectors.cs`
- `Editor/TaxxorEditor/backend/code/shared/TaxxorServiceConnectors.cs`

For each old REST connector method used by your endpoints, DELETE it:

```csharp
// DELETE THIS METHOD FROM BOTH SERVICES
public static async Task<XmlDocument> RetrieveFilingDataRestConnector(ProjectVariables projectVars)
{
    // Old REST API call implementation
    return await CallTaxxorConnectedService<XmlDocument>(
        ConnectedServiceEnum.DocumentStore,
        RequestMethodEnum.Get,
        "taxxoreditorfilingdata",
        null,
        false
    );
}
```

**Verification** (DocumentStore version):
```bash
grep -n "RetrieveFilingDataRestConnector" \
    DocumentStore/DocumentStore/backend/code/shared/TaxxorServiceConnectors.cs
# Result: (no output = success)
```

**Verification** (Editor version):
```bash
grep -n "RetrieveFilingDataRestConnector" \
    Editor/TaxxorEditor/backend/code/shared/TaxxorServiceConnectors.cs
# Result: (no output = success)
```

**Important**: Verify the Editor is using the new gRPC client instead of the old REST connector.

### Step 6: Comprehensive Verification

Run all these grep commands. **EVERY ONE must return zero results:**

```bash
# Check ApiDispatcher.cs for all old endpoint names
grep -n "case \"taxxoreditorfilingdata\"\|case \"taxxoreditorcomposerdataextended\"\|case \"taxxoreditorcomposerdataoverview\"" \
    DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs

# Check ApiDispatcher.cs for all old handler methods
grep -n "RetrieveFilingData\|StoreFilingData\|DeleteFilingData" \
    DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs

# Check base_structure.xml for all old endpoints
grep -n "taxxoreditorfilingdata\|taxxoreditorcomposerdataextended\|taxxoreditorcomposerdataoverview" \
    DocumentStore/DocumentStore/hierarchies/base_structure.xml

# Check DocumentStore TaxxorServiceConnectors
grep -n "RetrieveFilingData\|StoreFilingData\|DeleteFilingData" \
    DocumentStore/DocumentStore/backend/code/shared/TaxxorServiceConnectors.cs

# Check Editor TaxxorServiceConnectors
grep -n "RetrieveFilingData\|StoreFilingData\|DeleteFilingData" \
    Editor/TaxxorEditor/backend/code/shared/TaxxorServiceConnectors.cs

# If ALL return zero output, cleanup is complete!
```

### Step 7: Compile and Test

```bash
# Recompile both solutions
dotnet build DocumentStore.sln
dotnet build Editor/TaxxorEditor.sln

# Both MUST succeed with no warnings about unused code
# Then test in Docker:
./split-from-monorepo.sh
# Wait 5-10 seconds for hot-reload
# Test gRPC calls in Docker

docker logs tdm-documentstore-1 | tail -50
docker logs tdm-editor-1 | tail -50
```

---

## Common Cleanup Mistakes & How to Avoid Them

### ❌ Mistake 1: Partial Cleanup

**Problem**: Removing case statement but leaving the handler method

```csharp
// BAD: Case removed but method still exists
// case "taxxoreditorfilingdata": // <- REMOVED
//    ...

private async Task RetrieveFilingData(...) // <- STILL HERE (WRONG!)
{
    // ...
}
```

**Fix**: Delete BOTH the case statement AND the handler method.

### ❌ Mistake 2: Missing XML Cleanup

**Problem**: Removing C# code but leaving XML endpoint definition

```xml
<!-- XML still exists -->
<item id="taxxoreditorfilingdata">
    <web_page>
        <path>/api/taxxoreditor/filing/data/generic</path>
        ...
    </web_page>
</item>
```

**Fix**: Remove the entire `<item>` block from base_structure.xml.

### ❌ Mistake 3: Forgetting Service Connectors

**Problem**: Old REST connectors still in TaxxorServiceConnectors.cs

```csharp
// OLD - Still exists in code
public static async Task<XmlDocument> RetrieveFilingDataOldRest(...)
{
    return await CallTaxxorConnectedService<XmlDocument>(
        ConnectedServiceEnum.DocumentStore,
        RequestMethodEnum.Get,
        "taxxoreditorfilingdata",
        ...
    );
}

// NEW - Editor should use gRPC instead
var gRpcClient = serviceProvider.GetService<FilingDataService.FilingDataServiceClient>();
var response = await gRpcClient.GetFilingComposerDataAsync(request);
```

**Fix**: Delete the old REST connector. Verify Editor is using gRPC clients from DI.

### ❌ Mistake 4: "I'll Clean Up Later"

**Problem**: Moving on without cleanup, promising to return

**Fix**: Clean up in the SAME batch. It will not happen "later." This is how technical debt accumulates.

### ❌ Mistake 5: Not Running Verification Grep Commands

**Problem**: Assuming cleanup is done without verifying

**Fix**: Actually run the grep commands. Verification takes 30 seconds and catches mistakes immediately.

### ❌ Mistake 6: Shared Handler Methods

**Problem**: Deleting a handler method that's still used by another endpoint

```csharp
case "endpoint1":
    await SharedHandler(request, response, routeData);  // Still needed!
    break;

case "endpoint2":  // Migrated to gRPC
    // Removed, but SharedHandler is gone too!
    break;
```

**Fix**: Before deleting a handler, search for ALL places it's called. Only delete when ALL calling endpoints are migrated.

---

## Before and After Examples

### Example: Batch 1 - SaveSourceData

**BEFORE (REST):**
```csharp
// ApiDispatcher.cs - case statement
case "taxxoreditorfilingdata":
    switch (reqVars.method)
    {
        case RequestMethodEnum.Get:
            await RetrieveFilingData(request, response, routeData);
            break;
        case RequestMethodEnum.Put:
            await StoreFilingData(request, response, routeData);
            break;
        default:
            _handleMethodNotSupported(reqVars);
            break;
    }
    break;

// ApiDispatcher.cs - handler method
private async Task StoreFilingData(HttpRequest request, HttpResponse response, RouteData routeData)
{
    try
    {
        // Old REST implementation for SaveSourceData
    }
    catch (Exception ex) { ... }
}

// base_structure.xml - endpoint definition
<item id="taxxoreditorfilingdata">
    <web_page>
        <path>/api/taxxoreditor/filing/data/generic</path>
        <linkname>Loads or saves filing assets</linkname>
    </web_page>
</item>

// TaxxorServiceConnectors.cs - REST connector
public static async Task<XmlDocument> SaveFilingDataRestConnector(...)
{
    return await CallTaxxorConnectedService<XmlDocument>(
        ConnectedServiceEnum.DocumentStore,
        RequestMethodEnum.Put,
        "taxxoreditorfilingdata",
        xmlDoc,
        false
    );
}
```

**AFTER (gRPC):**
```csharp
// No case statement in ApiDispatcher.cs
// No handler method in ApiDispatcher.cs
// No endpoint definition in base_structure.xml
// No REST connector in TaxxorServiceConnectors.cs

// Editor/TaxxorEditor/backend/code/TaxxorServicesFilingData.cs
public static async Task<XmlDocument> SaveSourceData(
    FilingDataService.FilingDataServiceClient filingDataClient,
    ProjectVariables projectVars,
    string did,
    string contentLanguage,
    bool debugRoutine = false)
{
    try
    {
        var request = new SaveSourceDataRequest
        {
            GrpcProjectVariables = ConvertToGrpcProjectVariables(projectVars),
            Data = xmlDoc.OuterXml,
            Did = did,
            ContentLanguage = contentLanguage
        };

        var response = await filingDataClient.SaveSourceDataAsync(request);

        if (response.Success)
        {
            // Handle success
        }
        else
        {
            // Handle error with gRPC response
        }
    }
    catch (Exception ex)
    {
        // Error handling
    }
}
```

---

## Checklist for Your Next Batch

When you start the next batch, use this checklist:

- [ ] Implement gRPC proto definitions
- [ ] Implement gRPC server handlers
- [ ] Register gRPC service in DocumentStore Startup.cs
- [ ] Update Editor client code
- [ ] Register gRPC client in Editor Startup.cs
- [ ] Compile both solutions (no errors)
- [ ] Publish to Docker: `./split-from-monorepo.sh`
- [ ] Wait 5-10 seconds for Docker hot-reload
- [ ] Test gRPC implementation in Docker
- [ ] **CLEANUP PHASE** (do NOT skip):
  - [ ] Remove XML endpoint definitions from `base_structure.xml`
  - [ ] Remove C# case statements from `ApiDispatcher.cs`
  - [ ] Remove handler methods from `ApiDispatcher.cs`
  - [ ] Remove REST connectors from `TaxxorServiceConnectors.cs` (both services)
  - [ ] Run verification grep commands (zero results)
  - [ ] Recompile both solutions
  - [ ] Test again in Docker to ensure gRPC still works
- [ ] Commit changes with clear message
- [ ] Update `MIGRATION_PLAN.md` with commit hash
- [ ] Move to next batch

---

## Questions & Answers

**Q: What if a handler method is used by multiple endpoints?**
A: Only delete it when ALL endpoints using it are migrated. Check by searching for the method name in all case statements.

**Q: Should I keep the old REST code "just in case"?**
A: No. Git has the full history. If you need it back, you can recover it from git. The purpose of this migration is to remove the old code.

**Q: Can I defer cleanup to the next batch?**
A: No. Cleanup is part of THIS batch. Deferring creates technical debt. Each batch must be complete before moving to the next.

**Q: What if I find the gRPC version doesn't work quite right?**
A: That's fine—fix it. But the cleanup still happens. You don't keep the old REST code "as a fallback." You fix the gRPC implementation.

**Q: How do I know if cleanup is really complete?**
A: When all 6 grep verification commands return zero output. No output = success.

---

## Reference: Grep Commands for Current Orphaned Code

Use these to track down any remaining old code:

```bash
# Find all case statements in ApiDispatcher
grep -n "case \"taxxor" DocumentStore/DocumentStore/backend/controllers/ApiDispatcher.cs

# Find RestConnector references
grep -n "RestConnector\|CallTaxxorConnectedService" \
    DocumentStore/DocumentStore/backend/code/shared/TaxxorServiceConnectors.cs
grep -n "RestConnector\|CallTaxxorConnectedService" \
    Editor/TaxxorEditor/backend/code/shared/TaxxorServiceConnectors.cs

# Find old REST endpoint references
grep -n "taxxoreditor\|taxxorfiling" DocumentStore/DocumentStore/hierarchies/base_structure.xml
```

---

**Remember**: This migration is only complete when the old code is removed. No exceptions.
