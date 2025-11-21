# gRPC Migration Project

## Overview

This is a temporary monorepo combining **Editor** and **DocumentStore** services from the Taxxor Document Management (TDM) stack to migrate REST API communications to gRPC.

## 🤖 Automatic Debugging Skill Invocation

**CRITICAL: When you report issues, errors, or bugs, Claude Code MUST automatically use the `superpowers:systematic-debugging` skill.**

### Automatic Trigger Conditions

Claude Code will automatically invoke the systematic-debugging skill when you report ANY of these patterns:

- **"I'm testing batch X and getting an error..."**
- **"SaveHierarchy is causing issues..."**
- **"There's a problem with..."**
- **"Error in [feature]..."**
- **"Docker logs show..."**
- **"Test failed because..."**
- **Any report of unexpected behavior during development**

### Expected Workflow

When you report an issue:

1. **Recognition** - Claude Code recognizes the issue report
2. **Skill Invocation** - Announces: "I'm using the systematic-debugging skill to investigate [issue]"
3. **Skill Execution** - Loads and runs `superpowers:systematic-debugging`
4. **Investigation** - Follows the skill's 5-phase debugging process:
   - Phase 1: Root cause investigation
   - Phase 2: Pattern analysis
   - Phase 3: Hypothesis testing
   - Phase 4: Implementation of fix
   - Phase 5: Verification of solution
5. **Reporting** - Returns findings and fixed code

### Example

```
YOU: "I'm testing batch 3 and SaveHierarchy is causing issues. Can you investigate?"

CLAUDE (immediately):
"I'm using the systematic-debugging skill to investigate the SaveHierarchy issue."

[Skill runs investigation process]

CLAUDE (after investigation):
"Found the issue: [root cause]. Applied fix: [solution]. Verification: [test results]"
```

### Why This Matters

- ✅ Prevents manual trial-and-error investigation
- ✅ Ensures systematic problem-solving with proven methodology
- ✅ Catches environment issues (like "Docker didn't rebuild") automatically
- ✅ Includes verification steps to confirm fixes work
- ✅ Saves time by following a structured process

### Note

You should NOT need to ask "use the debugging skill" - just report the issue naturally and Claude Code will handle skill invocation automatically.

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

### Complete Development & Test Feedback Loop

**Use this complete feedback loop for rapid development and testing:**

```bash
# STEP 1: Implement changes in the monorepo
# Edit files in the monorepo (e.g., DocumentStore/GrpcServices/Services/FilingComposerDataService.cs)

# STEP 2: Verify local compilation
cd /path/to/monorepo/DocumentStore
dotnet build DocumentStore.sln

# Expected output: "0 Error(s)" (warnings are OK)
# If errors → fix them before proceeding

# STEP 3: Publish changes to Docker-mounted directories
cd /path/to/_grpc-migration-tools
bash ./split-from-monorepo.sh

# This syncs only *.cs and *.proto files to the actual service directories
# Shows which files were changed

# STEP 4: Monitor Docker logs for compilation
# Wait 5-10 seconds for hot reload to detect changes and rebuild
docker logs -f tdm-documentstore-1 2>&1 | tail -50

# Look for:
# - Compilation warnings (expected, safe to ignore most)
# - Compilation errors (must fix)
# - "watch : Shutdown requested. Press Ctrl+C again to force exit" (compilation complete)

# STEP 5: Test the endpoint with curl
# Example for testing a gRPC-backed REST endpoint:
curl -k "https://editor:8071/api/hierarchymanager/tools/sectionlanguageclone?did=1153794-message-from-the-ceo1&sourcelang=en&targetlang=zh&includechildren=false&ocvariantid=arpdfen&octype=pdf&oclang=en&pid=ar24&vid=1&ctype=regular&token=TOKEN&type=json"

# Expected response for success: HTTP 200 OK with JSON result
# If not 200 OK → proceed to Step 6

# STEP 6: Check Docker logs for runtime errors
docker logs tdm-documentstore-1 2>&1 | tail -100 | grep -E "(fail|error|Error|Exception)" -A 10 -B 5

# Look for:
# - NullReferenceException → Check path initialization
# - gRPC errors → Check service/client registration
# - Business logic errors → Check implementation

# STEP 7: If errors found, fix and repeat from Step 1
# This creates a tight feedback loop: Edit → Compile → Sync → Test → Fix → Repeat
```

### Feedback Loop Best Practices

**✅ DO:**

1. **Always compile locally first** (Step 2) - Catch syntax errors before Docker
2. **Wait for Docker rebuild** (Step 4) - Don't test immediately after sync
3. **Check both compile-time and runtime logs** - Compilation might succeed but runtime might fail
4. **Use curl for quick API testing** - Faster than browser for repeated tests
5. **Monitor logs in real-time** during testing - See errors as they happen

**❌ DON'T:**

1. **Don't skip local compilation** - Docker rebuild takes longer; find errors locally first
2. **Don't test immediately after split** - Wait 5-10 seconds for hot reload
3. **Don't ignore compilation warnings** - Some warnings indicate real issues
4. **Don't assume success without testing** - Always verify with curl/browser

### Automated Testing Example

**For rapid iteration, run all steps in sequence:**

```bash
# Complete feedback loop in one command block
cd /path/to/monorepo/DocumentStore && \
dotnet build DocumentStore.sln && \
cd /path/to/_grpc-migration-tools && \
bash ./split-from-monorepo.sh && \
sleep 10 && \
curl -k "https://editor:8071/api/your-endpoint?params" && \
docker logs tdm-documentstore-1 2>&1 | tail -50
```

This provides immediate feedback:
- ✅ Compilation status
- ✅ Files synced
- ✅ API response
- ✅ Runtime logs

### Workflow for Each Change

**Quick reference workflow:**

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
curl -k "https://editor:8071/api/your-endpoint?params"

# 6. Check logs if issues
docker logs tdm-documentstore-1 | tail -50
```

**Time savings**: This feedback loop takes ~20-30 seconds per iteration vs. several minutes with manual Docker restarts

## ⚡ Automated Feedback Loop with Backend Developer Agent

**For maximum productivity**: Use the automated testing workflow that provides automatic testing, monitoring, and iterative fixing until features are fully working.

### Quick Start

**NEW**: This workflow is now available as a **skill** and **slash command**!

**Use the skill**: Claude Code will automatically detect when to use the debugging workflow
**Use the slash command**: Type `/debug-grpc` to launch the automated testing agent

**With test URL** (full end-to-end testing):
```
/debug-grpc https://editor:8071/api/your-endpoint?params
```

**Without test URL** (compilation + health check only):
```
/debug-grpc
```

See `.claude/README.md` for complete documentation on the skill and command.

---

### How It Works: Option A Enhanced (Semi-Autonomous)

This workflow combines **your implementation expertise** with an **autonomous testing agent** that handles the repetitive test-fix-verify cycle.

```
┌────────────────────────────────────────────────────────────────┐
│ WORKFLOW: Automated Feedback Loop                             │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ 1. YOU say: "Implement Batch 3"                              │
│                                                                │
│ 2. CLAUDE implements in monorepo:                            │
│    ✅ Update proto definitions                               │
│    ✅ Implement gRPC server handlers                         │
│    ✅ Update Editor client code                              │
│    ✅ Verify local compilation                               │
│                                                                │
│ 3. CLAUDE launches backend-developer agent:                  │
│    Parameters:                                                │
│    - testUrl: (optional) "https://editor:8071/api/..."      │
│    - maxIterations: 5                                         │
│    - autoFix: false (reports issues, you fix)                │
│                                                                │
│ 4. AGENT executes test loop:                                 │
│    ┌──────────────────────────────────────────┐             │
│    │ LOOP until success or max iterations:    │             │
│    │                                           │             │
│    │ a. Run split-from-monorepo.sh           │             │
│    │ b. Monitor Docker logs for compilation   │             │
│    │ c. Test endpoint (if URL provided)       │             │
│    │ d. Check logs for runtime errors         │             │
│    │                                           │             │
│    │ IF errors found:                         │             │
│    │   → Extract error details                │             │
│    │   → Report back to Claude                │             │
│    │   → WAIT for fix                         │             │
│    │   → Continue loop after fix              │             │
│    │                                           │             │
│    │ IF no errors:                            │             │
│    │   → Report success                       │             │
│    │   → EXIT loop                            │             │
│    └──────────────────────────────────────────┘             │
│                                                                │
│ 5. CLAUDE receives agent report:                             │
│    - Success: Mark batch complete                            │
│    - Errors: Analyze, fix code, agent re-tests              │
│                                                                │
│ 6. Repeat steps 4-5 automatically until success             │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

### Two Testing Modes

#### Mode 1: With Test URL (Full End-to-End Testing)

**Use when**: You have a REST endpoint or curl command to test the feature

**Agent tests**:
1. ✅ Local compilation
2. ✅ Sync to Docker
3. ✅ Docker hot-reload compilation
4. ✅ **Endpoint testing with curl**
5. ✅ Runtime log analysis

**Example**:
```
Agent parameters:
- testUrl: "https://editor:8071/api/hierarchymanager/tools/sectionlanguageclone?did=123..."
- maxIterations: 5

Agent report (Iteration 1):
❌ Test failed

Curl response: HTTP 500 Internal Server Error
<error>Could not load source data</error>

Docker logs show:
System.ArgumentNullException: Value cannot be null. (Parameter 'xmlPathOs')
  at ProjectLogic.LoadAndResolveInlineFilingComposerData(xmlPathOs)
  at FilingComposerDataService.CloneSectionLanguageData(request)

Waiting for fix...
```

#### Mode 2: Without Test URL (Compilation + Log Monitoring)

**Use when**:
- No simple test endpoint available
- Internal/helper methods
- Methods requiring complex setup
- Early stage testing before integration

**Agent tests**:
1. ✅ Local compilation
2. ✅ Sync to Docker
3. ✅ Docker hot-reload compilation
4. ✅ Runtime log analysis (checks for errors on startup)
5. ⏭️ **Skips endpoint testing**

**Example**:
```
Agent parameters:
- testUrl: null (or omitted)
- maxIterations: 3

Agent report (Iteration 1):
❌ Compilation failed

Docker logs show:
/app/GrpcServices/Services/FilingDataService.cs(45,25): error CS0103:
The name 'InitializeProjectVariablesForGrpc' does not exist

Waiting for fix...

---

Agent report (Iteration 2):
✅ Success

Local compilation: ✅ 0 errors
Docker compilation: ✅ Hot reload complete
Runtime logs: ✅ No errors detected

All checks passed!
```

### Agent Behavior Details

**What the agent does automatically**:
1. **Publishes changes** - Runs `split-from-monorepo.sh`
2. **Waits for Docker rebuild** - Monitors for "watch : Shutdown requested"
3. **Tests endpoint** (if URL provided) - Executes curl and checks response
4. **Scans logs for errors** - Searches for Exception, Error, fail patterns
5. **Reports findings** - Extracts relevant error messages and stack traces
6. **Iterates automatically** - Continues testing after each fix
7. **Declares success** - Reports when all checks pass

**What you still do**:
1. **Implement the feature** - Write proto, server, and client code
2. **Fix issues** - Analyze errors and apply corrections
3. **Make decisions** - Determine when to stop or change approach

### Why Option A Enhanced is Best

| Feature | Manual Testing | **Option A Enhanced** | Full Auto (Option C) |
|---------|---------------|---------------------|---------------------|
| Implementation | You do | **You do (better)** | Agent does |
| Testing | Manual | **Agent does** | Agent does |
| Error Detection | Manual | **Agent does** | Agent does |
| Fixing | You do | **You do (better)** | Agent does |
| Iteration | Manual | **Automatic** | Automatic |
| Context Awareness | Full | **Strategic** | Limited |
| Time per iteration | 3-5 min | **30 sec** | 30 sec |

**Advantages of Option A Enhanced**:
- ✅ **You have full codebase context** for fixing complex issues
- ✅ **Agent handles repetitive testing** (the tedious part)
- ✅ **Automatic iteration** until success
- ✅ **Human oversight** on fixes (prevents incorrect assumptions)
- ✅ **Fast feedback** (agent tests in seconds, reports immediately)
- ✅ **Works with or without test URLs**
- ✅ **Can evolve** to fully autonomous (Option C) later

### Practical Example: With Test URL

**Scenario**: Implementing Batch 3 (CloneSectionLanguageData)

```
YOU: "Implement Batch 3"

CLAUDE:
- Implements proto definitions
- Implements server handler
- Updates client code
- Verifies compilation
- Launches agent with testUrl

AGENT [Iteration 1]:
❌ Test failed

Curl: HTTP 500
Error in logs:
  NullReferenceException at line 808
  xmlSectionFolderPathOs is null

CLAUDE:
Analyzing... I see the issue - forgot to calculate folder path.
*Fixes code*

AGENT [Iteration 2]:
✅ Test succeeded

Curl: HTTP 200
{
  "success": true,
  "message": "Section language cloned successfully"
}

No errors in logs.

CLAUDE:
✅ Batch 3 complete! Marking MIGRATION_PLAN.md...
```

### Practical Example: Without Test URL

**Scenario**: Implementing internal helper method

```
YOU: "Refactor InitializeProjectVariables pattern"

CLAUDE:
- Creates centralized helper method
- Updates all 6 methods to use it
- Verifies compilation
- Launches agent without testUrl (just compilation check)

AGENT [Iteration 1]:
✅ Success

Local compilation: ✅ DocumentStore.sln - 0 errors
Docker compilation: ✅ Hot reload complete at 14:23:45
Runtime logs: ✅ No exceptions found

CLAUDE:
✅ Refactoring complete! All services using centralized helper.
```

### When to Use Each Mode

**Use Mode 1 (with test URL) when**:
- ✅ Migrating a REST endpoint to gRPC
- ✅ Have a curl command or browser URL to test
- ✅ Want full end-to-end verification
- ✅ Testing external API behavior

**Use Mode 2 (without test URL) when**:
- ✅ Refactoring internal code
- ✅ No simple test endpoint exists
- ✅ Early stage development
- ✅ Just need compilation + basic runtime verification
- ✅ Will test manually after agent confirms compilation

### Invoking the Agent

**From CLAUDE.md, Claude will automatically**:
1. Implement the batch using the 4-step pattern
2. Verify local compilation
3. Launch the backend-developer agent
4. Monitor agent progress
5. Fix any reported issues
6. Iterate until agent reports success

**You just need to say**:
- "Implement Batch 3"
- "Implement the next batch"
- "Migrate SaveSourceData method"

**Optionally provide test URL**:
- "Implement Batch 3 and test with https://editor:8071/api/..."

**Claude will handle the rest**, including launching the agent and managing the feedback loop.

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
