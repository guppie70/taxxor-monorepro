---
name: grpc-migration-debugging
description: Automated debugging workflow for gRPC migrations with test-fix-verify loop using backend-developer agent
---

# gRPC Migration Debugging Workflow

**Use this skill when**: Debugging gRPC migration issues, testing endpoints after implementation, or verifying compilation after changes.

## Overview: Option A Enhanced (Semi-Autonomous)

This workflow combines **your implementation expertise** with an **autonomous testing agent** that handles the repetitive test-fix-verify cycle.

**Division of Responsibilities**:
- **YOU (Claude)**: Implement fixes with full codebase context
- **AGENT**: Handle repetitive testing/verification automatically
- **YOU**: Fix issues based on agent reports
- **AGENT**: Iterate until success

## When to Use This Skill

Use this skill for:
- ✅ Testing gRPC endpoints after implementation
- ✅ Debugging compilation or runtime errors
- ✅ Verifying changes work in Docker after publishing
- ✅ Iterative debugging where multiple test cycles are expected
- ✅ Any scenario where you need feedback loop automation

**DO NOT use for**:
- ❌ Initial implementation (implement first, then use this for testing)
- ❌ Simple syntax fixes that don't need Docker testing
- ❌ Changes that only need local compilation verification

## Two Testing Modes

### Mode 1: With Test URL (Full End-to-End Testing)

**Use when**: You have a REST endpoint or curl command to test the feature

**IMPORTANT**: For Editor endpoints, the agent will automatically transform `https://editor/` to `https://editor:8071/` (the authentication proxy). You can provide URLs in either format.

**Agent verifies**:
1. ✅ Local compilation
2. ✅ Sync to Docker (split-from-monorepo.sh)
3. ✅ Docker hot-reload compilation
4. ✅ **Endpoint testing with curl** (with automatic URL transformation)
5. ✅ Runtime log analysis

### Mode 2: Without Test URL (Compilation + Log Monitoring)

**Use when**:
- No simple test endpoint available
- Internal/helper methods
- Methods requiring complex setup
- Early stage testing before integration

**Agent verifies**:
1. ✅ Local compilation
2. ✅ Sync to Docker
3. ✅ Docker hot-reload compilation
4. ✅ Runtime log analysis (startup errors only)
5. ⏭️ **Skips endpoint testing**

## Workflow Steps

### 1. Make Your Implementation/Fix

Implement the feature or fix the reported issue using your full codebase context.

### 2. Verify Local Compilation

```bash
cd DocumentStore && dotnet build DocumentStore.sln
cd ../Editor && dotnet build TaxxorEditor.sln
```

Both must show `0 Error(s)` before proceeding.

### 3. Launch the Backend-Developer Agent

Use the Task tool with `subagent_type='general-purpose'` to launch an agent with this prompt:

**For Mode 1 (with test URL)**:
```
You are the backend-developer testing agent. Your job is to publish changes, monitor Docker compilation, test the endpoint, and report results.

TASK: Test the gRPC migration endpoint with automated feedback loop

TEST URL: [the curl command or endpoint to test]
MAX ITERATIONS: 5
PROJECT ROOT: /Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-monorepo

CRITICAL URL TRANSFORMATION:
- For Editor service endpoints, ALWAYS replace `https://editor/` with `https://editor:8071/`
- Port 8071 is the authentication proxy that handles user authentication
- Example: `https://editor/api/...` → `https://editor:8071/api/...`
- This transformation is REQUIRED for all Editor endpoint tests

INSTRUCTIONS:

For each iteration:

1. PUBLISH CHANGES
   - Navigate to /Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-tools
   - Run: bash ./split-from-monorepo.sh
   - Verify: Check output for changed files

2. WAIT FOR DOCKER REBUILD
   - Run: docker logs -f tdm-documentstore-1 2>&1 | grep "watch :" | head -5
   - Look for: "watch : Shutdown requested" (indicates rebuild complete)
   - Wait: 10 seconds after seeing this message

3. TEST ENDPOINT
   - TRANSFORM URL: If URL contains `https://editor/`, replace with `https://editor:8071/`
   - Run the curl command: [insert full curl command with transformed URL]
   - Check HTTP response code
   - Check response body for error messages

4. CHECK DOCKER LOGS FOR ERRORS
   - Run: docker logs tdm-documentstore-1 2>&1 | tail -100 | grep -E "(fail|error|Error|Exception)" -A 10 -B 5
   - Run: docker logs tdm-editor-1 2>&1 | tail -100 | grep -E "(fail|error|Error|Exception)" -A 10 -B 5
   - Extract any error messages, exceptions, or stack traces

5. REPORT RESULTS
   If errors found:
   - Report: ❌ Test failed (Iteration N)
   - Include: HTTP response code/body
   - Include: Full error messages from logs
   - Include: Stack traces if available
   - STOP and wait for fix from Claude

   If no errors:
   - Report: ✅ Success
   - Include: HTTP response
   - Include: Confirmation that logs are clean
   - EXIT

Continue iterations automatically after Claude applies fixes, up to MAX_ITERATIONS.
```

**For Mode 2 (without test URL)**:
```
You are the backend-developer testing agent. Your job is to publish changes, monitor Docker compilation, and verify no runtime errors.

TASK: Verify compilation and basic runtime health after changes

MAX ITERATIONS: 3
PROJECT ROOT: /Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-monorepo

INSTRUCTIONS:

For each iteration:

1. PUBLISH CHANGES
   - Navigate to /Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-tools
   - Run: bash ./split-from-monorepo.sh
   - Verify: Check output for changed files

2. WAIT FOR DOCKER REBUILD
   - Run: docker logs -f tdm-documentstore-1 2>&1 | grep "watch :" | head -5
   - Look for: "watch : Shutdown requested" (indicates rebuild complete)
   - Wait: 10 seconds after seeing this message

3. CHECK COMPILATION IN DOCKER
   - Run: docker logs tdm-documentstore-1 2>&1 | tail -100 | grep -E "(error CS|Error)" -A 5
   - Run: docker logs tdm-editor-1 2>&1 | tail -100 | grep -E "(error CS|Error)" -A 5
   - Check for: Compilation errors (error CS####)

4. CHECK RUNTIME LOGS
   - Run: docker logs tdm-documentstore-1 2>&1 | tail -50 | grep -E "(Exception|fail)" -A 10 -B 5
   - Run: docker logs tdm-editor-1 2>&1 | tail -50 | grep -E "(Exception|fail)" -A 10 -B 5
   - Look for: Runtime exceptions or failures on startup

5. REPORT RESULTS
   If errors found:
   - Report: ❌ Compilation or runtime errors detected (Iteration N)
   - Include: Full error messages
   - Include: Stack traces if available
   - STOP and wait for fix from Claude

   If no errors:
   - Report: ✅ Success
   - Include: Confirmation of clean compilation
   - Include: Confirmation of no runtime errors
   - EXIT

Continue iterations automatically after Claude applies fixes, up to MAX_ITERATIONS.
```

### 4. Monitor Agent Progress

The agent will:
- Automatically publish changes
- Wait for Docker rebuild
- Test endpoint (Mode 1) or check compilation (Mode 2)
- Scan logs for errors
- Report findings

### 5. Fix Reported Issues

When agent reports errors:
1. **Analyze the error** - Use full codebase context
2. **Implement the fix** - Make necessary code changes
3. **Verify local compilation** - Ensure it compiles locally
4. **Inform the agent** - Let the agent know the fix is ready
5. **Agent re-tests automatically** - Agent will repeat the verification cycle

### 6. Iterate Until Success

The agent continues testing automatically after each fix until:
- ✅ All checks pass (success)
- ⏱️ MAX_ITERATIONS reached (manual intervention needed)

## Example Usage

### Example 1: Testing a gRPC Endpoint Migration

```
USER: "Implement Batch 3"

CLAUDE:
1. Implements proto definitions
2. Implements server handler
3. Updates client code
4. Verifies local compilation: ✅
5. Launches agent with testUrl

AGENT [Iteration 1]:
❌ Test failed

Curl: HTTP 500
Error in logs:
  NullReferenceException at line 808
  xmlSectionFolderPathOs is null

CLAUDE:
I see the issue - missing folder path calculation.
[Implements fix]
Fix ready, please test again.

AGENT [Iteration 2]:
✅ Test succeeded

Curl: HTTP 200
{"success": true, "message": "Section language cloned successfully"}
No errors in logs.

CLAUDE:
✅ Batch 3 complete! The endpoint is working correctly.
```

### Example 2: Compilation Verification (No Test URL)

```
USER: "Refactor the InitializeProjectVariables pattern"

CLAUDE:
1. Creates centralized helper method
2. Updates all 6 methods to use it
3. Verifies local compilation: ✅
4. Launches agent (Mode 2 - no test URL)

AGENT [Iteration 1]:
✅ Success

Local compilation: ✅ DocumentStore.sln - 0 errors
Docker compilation: ✅ Hot reload complete at 14:23:45
Runtime logs: ✅ No exceptions found

CLAUDE:
✅ Refactoring complete! All services using centralized helper.
```

## Critical Rules

### ✅ DO

1. **Always verify local compilation first** before launching agent
2. **Use Mode 1 (with test URL)** when you have a testable endpoint
3. **Use Mode 2 (without test URL)** for internal changes or untestable methods
4. **Wait for agent to complete** before declaring success
5. **Fix issues based on agent reports** using full codebase context
6. **Let the agent iterate automatically** - don't manually re-test

### ❌ DON'T

1. **Don't skip local compilation** - Catch errors locally first
2. **Don't manually run split script** - Let the agent handle it
3. **Don't test immediately after changes** - Let agent manage timing
4. **Don't declare success without agent confirmation** - Wait for ✅ report
5. **Don't bypass the agent for "simple" fixes** - Even simple fixes benefit from verification

## Agent Parameters Reference

| Parameter | Mode 1 (Full Testing) | Mode 2 (Compilation Only) |
|-----------|----------------------|--------------------------|
| testUrl | **Required** - Full curl command | **Omit** or set to null |
| maxIterations | 5 (default) | 3 (default) |
| projectRoot | Auto-detected | Auto-detected |

## Troubleshooting

**If agent reports connection errors**:
- Check Docker containers are running: `docker ps`
- Restart if needed: `docker restart tdm-documentstore-1 tdm-editor-1`

**If agent can't find split script**:
- Verify path: `/Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-tools/split-from-monorepo.sh`
- Check script is executable: `chmod +x split-from-monorepo.sh`

**If iteration limit reached**:
- Analyze the pattern of errors across iterations
- May need a different approach or architectural change
- Consider asking user for guidance

## Benefits of This Workflow

- ✅ **Fast feedback** - Agent tests in ~30 seconds per iteration
- ✅ **Automatic iteration** - No manual test-fix cycles
- ✅ **Full context for fixing** - You maintain codebase knowledge
- ✅ **Consistent testing** - Agent follows same steps every time
- ✅ **Reduced manual work** - No more manual Docker commands
- ✅ **Clear success criteria** - Objective pass/fail reporting

## Integration with CLAUDE.md

This skill implements the "Option A Enhanced (Semi-Autonomous)" workflow described in the project's CLAUDE.md file. It provides the automation layer while keeping implementation decisions in your hands.
