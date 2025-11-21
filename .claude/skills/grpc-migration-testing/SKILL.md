---
name: grpc-migration-testing
description: Use when reporting gRPC failures or errors during testing - automatically launches the test-fix-verify feedback loop with Docker, split script, curl testing, and log monitoring
---

# gRPC Migration Testing

## Overview

**Core principle:** When you report a failure, automatically start the feedback loop. You fix code, the workflow tests it, reports results, and iterates until success.

**Automatic trigger:** Error keywords like "failed", "not working", "error", "issue" + curl command or test description.

## When This Triggers

Claude Code **automatically invokes this workflow** when you say things like:

- "Saving the hierarchies failed. Can you check this?"
- "Storing the section failed. Can you help me debug?"
- "LoadHierarchy is returning an error"
- "gRPC call failing: [curl command]"
- "Getting HTTP 500 when testing SaveHierarchy"
- "Docker logs show exception in FilingComposerDataService"

**No need to ask** - just report the issue naturally, and the workflow starts automatically.

## The Automatic Workflow

```
┌─────────────────────────────────────────────────────┐
│ YOU: "Saving hierarchy failed. Can you debug this?" │
└─────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────┐
│ CLAUDE: Recognizes error report + automatic trigger │
│ "I'm using the grpc-migration-testing skill..."     │
└─────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────┐
│ PHASE 1: Gather Evidence                            │
│ - Read your error description                       │
│ - Extract test URL (if you provided curl)           │
│ - Note what you're trying to do                     │
└─────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────┐
│ PHASE 2: Launch Test Loop                           │
│                                                     │
│ ITERATION 1:                                        │
│ ✅ Run: ./split-from-monorepo.sh                   │
│ ✅ Wait: Monitor Docker for "watch : Shutdown"     │
│ ✅ Test: Execute your curl command                 │
│ ✅ Analyze: Check response + Docker logs           │
│                                                     │
│ IF ERROR FOUND:                                     │
│   → Extract error details                          │
│   → Report to you with:                            │
│     - HTTP status code                             │
│     - Error message                                │
│     - Stack trace (if available)                   │
│     - Which file/line failed                       │
│   → WAIT for you to fix code                       │
│                                                     │
│ IF NO ERRORS:                                       │
│   → Report SUCCESS ✅                              │
│   → Confirm feature working                        │
│   → EXIT loop                                      │
└─────────────────────────────────────────────────────┘
                           ↓
        YOU FIX THE CODE IN MONOREPO
                           ↓
┌─────────────────────────────────────────────────────┐
│ ITERATION 2+: Automatic Retry                       │
│                                                     │
│ You say: "Fixed the ProjectVariables initialization"
│                                                     │
│ Claude:                                             │
│ ✅ Run: ./split-from-monorepo.sh (again)           │
│ ✅ Wait: Monitor Docker rebuild                    │
│ ✅ Test: Run curl command (same as before)         │
│ ✅ Check: Response + logs                          │
│                                                     │
│ IF STILL FAILING:                                   │
│   → Report new error details                       │
│   → Wait for next fix                              │
│                                                     │
│ IF NOW WORKING:                                     │
│   → Report SUCCESS ✅✅✅                          │
│   → Feature fully working                          │
│   → EXIT loop                                      │
└─────────────────────────────────────────────────────┘
                           ↓
        REPEAT: Fix → Test → Report
     (Until success or max iterations reached)
```

## What You Provide

1. **Error description** - What's failing?
   - "Saving hierarchy returns HTTP 500"
   - "Getting NullReferenceException"
   - "Service not found error"

2. **Test command** (optional but recommended)
   ```bash
   curl -k "https://editor:8071/api/hierarchymanager/tools/savehierarchy?did=123&pid=456..."
   ```

3. **Your fixes** - When workflow reports an error, you fix the code in the monorepo

## What the Workflow Does

Each iteration automatically:

1. **Publish changes**
   ```bash
   ./split-from-monorepo.sh
   ```

2. **Monitor Docker rebuild**
   - Waits for `dotnet watch` to complete
   - Checks for compilation errors
   - Waits ~10 seconds for hot-reload

3. **Execute test**
   - Runs your curl command
   - Captures HTTP response code
   - Captures response body

4. **Analyze logs**
   ```bash
   docker logs tdm-documentstore-1 | tail -100
   docker logs tdm-editor-1 | tail -100
   ```
   - Searches for errors/exceptions
   - Extracts stack traces
   - Identifies failing component

5. **Report results**
   - ❌ **Error found?** Shows exact error with file:line
   - ✅ **Success?** Confirms working, shows response

## Examples

### Example 1: With Curl Command

```
YOU:
Storing the section failed. Can you help debug?

curl -k "https://editor:8071/api/hierarchymanager/tools/savesectiondata?\
did=section-123&projectid=ar24&versionid=v1&contenttype=text&language=en"

CLAUDE:
I'm using the grpc-migration-testing skill to debug this.

[Iteration 1]
Testing: POST https://editor:8071/api/hierarchymanager/tools/savesectiondata...

❌ FAILED: HTTP 500

Error from Docker logs:
  NullReferenceException at FilingComposerDataService.cs:45
  projectVars.cmsDataRootPathOs is null

  Stack trace shows missing InitializeProjectVariablesForGrpc() call

What to fix:
  - Handler uses direct _mapper.Map instead of helper method
  - Need: var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

Waiting for fix...

YOU:
[Fix the code]
Fixed - now using InitializeProjectVariablesForGrpc

CLAUDE:
[Iteration 2]
Re-testing...

✅ SUCCESS: HTTP 200

Response: {"success":true,"sectionId":"section-123","..."}
No errors in logs.

The issue is resolved! Section storage working correctly.
```

### Example 2: Without Curl (Just Description)

```
YOU:
LoadHierarchy is not working. Getting Service unimplemented error.

CLAUDE:
I'm using the grpc-migration-testing skill.

[Iteration 1]
Testing DocumentStore + Editor integration...

❌ FAILED: Service unimplemented

Error: gRPC service FilingHierarchyService is not registered

Root cause found:
  - Service registered in DocumentStore/Startup.cs? ❌ NO
  - Service registered in Editor/Startup.cs? ✅ YES (client)

Missing: endpoints.MapGrpcService<FilingHierarchyService>();
Location: DocumentStore/Startup.cs line ~180

What to fix:
  1. Add service registration in DocumentStore Startup.cs
  2. Restart DocumentStore container (happens automatically)

Waiting for fix...

YOU:
Added the MapGrpcService registration

CLAUDE:
[Iteration 2]
Re-testing...

✅ SUCCESS

LoadHierarchy working!
Service properly registered and responding.
```

## How Many Iterations?

The workflow runs up to **5 iterations** by default:

- **Iteration 1** - Initial test, identify error
- **Iteration 2** - After your first fix
- **Iteration 3** - After your second fix (if needed)
- **Iteration 4** - After your third fix
- **Iteration 5** - After your fourth fix

After **5 failures**, the workflow stops and recommends using `superpowers:systematic-debugging` for deeper investigation.

## What the Reports Show

Each iteration report includes:

```
[Iteration N]

Testing: [Your test URL or description]

STATUS: ✅ SUCCESS or ❌ FAILED

IF SUCCESS:
  ✅ HTTP [200/201/etc]
  ✅ Response shows: [actual data]
  ✅ No errors in Docker logs

IF FAILED:
  ❌ HTTP [500/400/etc]

  Error details:
  - Type: NullReferenceException
  - File: FilingComposerDataService.cs
  - Line: 45
  - Message: Object reference not set to an instance

  Stack trace (abbreviated):
    at SaveHierarchy() line 45
    at GrpcServices:Services

  Logs show (both services):
    DocumentStore: [relevant error lines]
    Editor: [relevant error lines]

  What to fix:
  [Specific guidance on what's wrong and where]

Waiting for fix...
```

## Red Flags - When to Abort

The workflow **stops automatically** if:

- ✋ **5 iterations completed without success** - Deeper investigation needed
- ✋ **Compilation error on split** - Check your monorepo code
- ✋ **Docker not responding** - Check container status
- ✋ **Same error repeats 3+ times** - Pattern indicates architectural issue

If any of these occur, use `superpowers:systematic-debugging` for root cause analysis.

## Integration with Other Skills

**grpc-migration-testing** (this skill):
- Automates the test-fix-verify loop
- Reports concrete errors
- Handles repetitive testing

**superpowers:systematic-debugging**:
- For when you're stuck after 5 iterations
- For unclear/intermittent errors
- For architectural questions

**grpc-migration-debugging**:
- For configuration checklist (service registration, proto, ProjectVariables)
- Use this FIRST if you're not sure what's wrong

**Workflow:**
1. Report error → grpc-migration-testing launches automatically
2. Workflow tests and reports specific error
3. You fix code based on the error
4. Workflow retests automatically
5. If 5 iterations don't work → Use systematic-debugging for deeper analysis

## Pro Tips

**Provide curl commands when possible:**
```
❌ NOT HELPFUL: "It's not working"
✅ HELPFUL: "Getting error with this curl:
curl -k 'https://editor:8071/api/...' -d '{json}'"
```

**The workflow is fastest when you:**
1. Provide a testable endpoint/curl command
2. Fix code quickly between iterations
3. Give specific error descriptions
4. Let the workflow handle testing/retesting

**Time expectations:**
- Iteration with test: ~30-60 seconds
- You fixing code: ~5-10 minutes (depends on complexity)
- 2-3 iteration cycle: ~15-30 minutes total

## Don't Use If...

**Don't invoke this workflow if:**
- ❌ You're still implementing the feature (use after implementation is done)
- ❌ You don't have a way to test yet (implement test first)
- ❌ The issue is local compilation error (fix locally first)
- ❌ Docker containers aren't running (start Docker, then use skill)

**DO use if:**
- ✅ Implementation is done, testing in Docker
- ✅ You have a curl command or clear test steps
- ✅ You're seeing HTTP/gRPC errors in runtime
- ✅ Docker containers are running and accessible

