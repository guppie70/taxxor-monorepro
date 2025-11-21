---
name: grpc-migration-debugging
description: Use when gRPC methods fail with HTTP 500, "Service unimplemented", or NullReferenceException - quick checklist for service registration, proto definitions, and ProjectVariables initialization before proposing code fixes
---

# gRPC Migration Debugging

## Overview

**Core principle:** gRPC failures have THREE common sources: service registration, proto definition mismatches, or path initialization. Check configuration FIRST, code SECOND.

**Red flag:** If you're proposing code fixes before reading Docker logs, you're guessing, not debugging.

## When to Use

Use as a **quick configuration checklist** BEFORE starting full systematic debugging.

Apply when any gRPC method fails with:
- HTTP 500 / gRPC errors in runtime testing
- "Service is unimplemented" (service not registered)
- NullReferenceException in gRPC handler
- gRPC call fails silently
- Proto mapping errors
- Path calculation issues in gRPC context

**Workflow:**
1. See gRPC error → Use THIS skill (quick checklist)
2. If checklist passes but issue persists → Use `superpowers:systematic-debugging` for deeper investigation

**NOT when:** Testing locally hasn't been run yet—run the test first, THEN use this skill when it fails.

## The Iron Law for gRPC Debugging

```
NO CODE CHANGES WITHOUT DOCKER LOGS AND SERVICE REGISTRATION VERIFICATION
```

If you haven't read Docker logs, you cannot propose code fixes. Logs contain the actual error message—everything else is guessing.

## Quick Checklist (In This Order)

**This is a manual 5-minute checklist YOU follow—not an automated agent workflow.**

**STOP here if any step fails—don't proceed to code fixes:**

1. ✅ **Gather Evidence from Docker** (You read logs manually)
   ```bash
   docker logs --tail 200 tdm-documentstore-1 | grep -E "(fail|error|Error|Exception)" -A 10 -B 5
   docker logs --tail 200 tdm-editor-1 | grep -E "(fail|error|Error|Exception)" -A 10 -B 5
   ```
   - Read the FULL error message (not automated—you interpret it)
   - Note the file and line number
   - Identify which service failed (Editor or DocumentStore)

2. ✅ **Verify Service Registration** (You inspect config files)
   - DocumentStore: Check `Startup.cs` line ~179-181 for `endpoints.MapGrpcService<YourService>();`
   - Editor: Check `Startup.cs` line ~173-186 for `services.AddGrpcClient<YourService.YourServiceClient>();`
   - If missing → You add it, you restart container, you test again

3. ✅ **Check Proto Definitions** (You review proto file)
   - Verify message names match exactly in `taxxor_service.proto`
   - Verify request/response types are correct
   - Run: `dotnet build` locally to catch proto errors

4. ✅ **Verify ProjectVariables Initialization** (You audit the code)
   - Check if handler uses `InitializeProjectVariablesForGrpc(_mapper, request)`
   - If using direct `_mapper.Map<ProjectVariables>(request)` → Missing path calculations
   - ProjectVariables without paths cause NullReferenceException

5. **ONLY THEN:** Examine code logic if all above checks pass

## Common gRPC Errors and Root Causes

| Error | Most Likely Cause | Check First |
|-------|------------------|------------|
| `Status(StatusCode="Unimplemented")` | Service not registered in Startup.cs | Service registration |
| `No service for type '...' registered` | gRPC client not registered in DI | Client registration |
| `NullReferenceException: cmsDataRootPathOs` | ProjectVariables not initialized | InitializeProjectVariablesForGrpc usage |
| `System.Reflection.TargetInvocationException` | Missing proto field or mapping | Proto definitions |
| HTTP 500 with no clear error | Check BOTH container logs | Docker logs |

## Evidence-First Workflow

**When someone reports a gRPC failure:**

1. **Don't look at code.** Look at Docker logs.
   ```bash
   # Get the actual error message
   docker logs tdm-documentstore-1 2>&1 | tail -100 | grep -i exception
   ```

2. **Categorize the error:**
   - Service registration error? → Check Startup.cs
   - Proto mapping error? → Check taxxor_service.proto
   - Path/context error? → Check ProjectVariables initialization
   - Code logic error? → THEN examine the handler code

3. **Fix the root cause**, not the symptom.

4. **Test immediately** - don't assume fix worked.

## Red Flags - STOP Before Code Changes

If you catch yourself:
- ✋ "I think the problem is in Git.cs" → How do you know? Show Docker logs first.
- ✋ "Let me just try changing this line" → What error justifies this change? Show evidence.
- ✋ "Compilation succeeded so the fix works" → Compilation ≠ runtime correctness. Test in Docker.
- ✋ "The fix should be in the handler" → Verify service registration first.
- ✋ Proposing Fix #2 without confirming Fix #1 failed → Test after EACH change.
- ✋ "I'll batch multiple fixes and test once" → Test after EACH change, one variable at a time.

**All of these mean: STOP. Read Docker logs. Verify registration. Check configuration. THEN propose fix.**

## Three Fix Failures = Architecture Question

If you've tried 3+ code changes and SaveHierarchy still fails:

**STOP. Don't attempt Fix #4.**

Instead, question the architecture:
- Is the gRPC definition correct?
- Is the request mapping working?
- Is the service initialization wrong?
- Should we refactor how ProjectVariables are passed?

Discuss with your human partner before attempting more fixes.

## ProjectVariables Initialization (Most Common Issue)

**✅ CORRECT:** In every gRPC handler
```csharp
public override async Task<TaxxorGrpcResponseMessage> SaveHierarchy(
    SaveHierarchyRequest request, ServerCallContext context)
{
    try
    {
        // ONE LINE: Initialize with ALL paths calculated
        var projectVars = InitializeProjectVariablesForGrpc(_mapper, request);

        // Now all paths are available: cmsDataRootPathOs, cmsContentRootPathOs, etc.
        var xmlPath = CalculateHierarchyPathOs(reqVars, projectVars);
```

**❌ WRONG:** Incomplete initialization
```csharp
// Missing path calculations - causes NullReferenceException
var projectVars = _mapper.Map<ProjectVariables>(request);
var xmlPath = CalculateHierarchyPathOs(reqVars, projectVars);  // projectVars.cmsDataRootPathOs is NULL
```

## Debugging Docker Logs Effectively

**Monitor BOTH containers during testing:**
```bash
# Terminal 1: DocumentStore logs
docker logs -f tdm-documentstore-1

# Terminal 2: Editor logs
docker logs -f tdm-editor-1

# Or combined with filtering:
docker logs --since 5m tdm-documentstore-1 | grep -E "(error|Error|Exception)" -A 5
```

**Trace the call chain:**
1. Editor initiates gRPC call → Check Editor logs
2. DocumentStore receives request → Check DocumentStore logs
3. DocumentStore processes → Check logs for business logic errors
4. DocumentStore returns response → Check response in Editor logs

## Common Mistakes

| Mistake | Why It Fails | Fix |
|---------|-------------|-----|
| Assuming code bug without Docker logs | Logs contain the actual error—assumptions are wrong 95% of the time | Always read logs first |
| Skipping service registration check | If service isn't registered, nothing will work | Verify Startup.cs BEFORE touching code |
| Using `_mapper.Map` without `InitializeProjectVariablesForGrpc` | Missing path initialization causes NullReferenceException | Use the centralized helper method |
| Testing immediately after split script | Docker hot-reload needs 5-10 seconds | Wait, then test, then check logs |
| Bundling multiple code fixes together | Can't isolate what fixed it; might break something else | Fix one thing at a time, test, check logs |
| "Compilation succeeded so it works" | Syntax valid ≠ logic correct ≠ service registered | Test in Docker, check logs |

## Workflow Summary

1. **Reproduce the failure** with specific steps
2. **Gather evidence** - Read Docker logs (DocumentStore AND Editor)
3. **Categorize** - Is it registration, proto, or code?
4. **Verify configuration** - Check Startup.cs and proto definitions
5. **Check initialization** - Verify ProjectVariables is properly initialized
6. **Examine code** - ONLY if 1-5 pass
7. **Test immediately** - After each change, check Docker logs
8. **Never batch fixes** - One change at a time

## Integration with Other Skills

**Complementary to `superpowers:systematic-debugging`:**

This skill is a **quick configuration checklist** specialized for gRPC issues in THIS project (Docker, service registration, proto definitions).

`superpowers:systematic-debugging` is a **deep investigation methodology** for complex issues.

**Workflow:**
1. **gRPC error occurs** → Use grpc-migration-debugging (this skill)
2. **Quick checklist** → Verifies service registration, proto definitions, ProjectVariables
3. **Issue resolved?** → Done
4. **Issue persists** → Use `superpowers:systematic-debugging` for deeper root cause analysis

**Per CLAUDE.md:** Error reports should automatically invoke `superpowers:systematic-debugging`. This skill accelerates that process with gRPC-specific checks FIRST.

