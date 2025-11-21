# Claude Code Configuration for gRPC Migration

This directory contains Claude Code configuration files for the gRPC migration project.

## Available Tools

### Skill: gRPC Migration Debugging

**File**: `skills/grpc-migration-debugging.md`

**Purpose**: Automated debugging workflow with test-fix-verify loop for gRPC migrations

**When to use**:
- After implementing a gRPC service/client
- When debugging compilation or runtime errors
- For iterative testing where multiple cycles are expected

**How to invoke**:
```
I'll use the grpc-migration-debugging skill to test this endpoint
```

Or Claude Code will automatically detect when it should be used based on context.

### Slash Command: /debug-grpc

**File**: `commands/debug-grpc.md`

**Purpose**: Quick launcher for the debugging workflow

**Usage**:

**With test URL** (full end-to-end testing):
```
/debug-grpc https://editor:8071/api/your-endpoint?params
```

**IMPORTANT**: For Editor endpoints, you can provide URLs with either `https://editor/` or `https://editor:8071/`. The agent will automatically transform `https://editor/` to `https://editor:8071/` (the authentication proxy) when testing.

**Without test URL** (compilation + basic health check):
```
/debug-grpc
```

**What it does**:
1. Launches a general-purpose agent configured for testing
2. Agent publishes changes via split-from-monorepo.sh
3. Agent monitors Docker compilation
4. Agent tests endpoint (if URL provided)
5. Agent checks logs for errors
6. Agent reports results
7. You fix any issues
8. Agent retests automatically
9. Repeats until success or max iterations

## Workflow Example

### Scenario: Testing a newly implemented gRPC endpoint

```
YOU: "I've implemented the CloneSectionLanguageData method. Let's test it."

CLAUDE: "I'll use the grpc-migration-debugging skill to test this endpoint with the automated agent."

[Claude verifies local compilation first]

CLAUDE: "Local compilation successful. Launching backend-developer agent..."

[Agent starts]

AGENT [Iteration 1]:
❌ Test failed

Curl response: HTTP 500
Docker logs show:
  NullReferenceException: Object reference not set to an instance of an object
  at FilingComposerDataService.CloneSectionLanguageData line 145

CLAUDE: "I see the issue - missing null check on projectVars.did. Let me fix that."

[Claude implements fix]

CLAUDE: "Fix applied. The agent will now retest automatically."

AGENT [Iteration 2]:
✅ Test succeeded

Curl: HTTP 200 OK
{"success": true, "message": "Section cloned successfully"}

No errors in Docker logs.

CLAUDE: "✅ The endpoint is working correctly. The fix resolved the issue."
```

## How It Works: Option A Enhanced

This setup implements the **Option A Enhanced (Semi-Autonomous)** debugging workflow:

**Division of Responsibilities**:
- **Claude** (you): Implement fixes with full codebase context
- **Agent**: Handle repetitive testing/verification automatically
- **Claude**: Fix issues based on agent reports
- **Agent**: Iterate until success

**Benefits**:
- ✅ Fast feedback (~30 seconds per iteration)
- ✅ Automatic iteration until success
- ✅ Human oversight on fixes (prevents incorrect assumptions)
- ✅ Works with or without test URLs
- ✅ Handles Docker hot-reload timing automatically

## Two Testing Modes

### Mode 1: With Test URL (Full Testing)

**Use when**: You have a REST endpoint or curl command to test

**URL Transformation**: The agent automatically transforms `https://editor/` to `https://editor:8071/` (authentication proxy) when testing Editor endpoints. You can provide URLs in either format.

**Agent verifies**:
1. ✅ Local compilation
2. ✅ Sync to Docker (split-from-monorepo.sh)
3. ✅ Docker hot-reload compilation
4. ✅ **Endpoint testing with curl** (with automatic URL transformation)
5. ✅ Runtime log analysis

**Example**:
```
/debug-grpc https://editor:8071/api/hierarchymanager/tools/sectionlanguageclone?did=123&sourcelang=en&targetlang=zh&pid=ar24&vid=1&type=json
```

Or with automatic transformation:
```
/debug-grpc https://editor/api/hierarchymanager/tools/sectionlanguageclone?did=123&sourcelang=en&targetlang=zh&pid=ar24&vid=1&type=json
```

### Mode 2: Without Test URL (Compilation Only)

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

**Example**:
```
/debug-grpc
```

## File Structure

```
.claude/
├── README.md                           # This file
├── commands/
│   └── debug-grpc.md                  # Slash command for quick access
└── skills/
    └── grpc-migration-debugging.md    # Full skill definition with workflow
```

## Requirements

For this workflow to function:

1. **Docker containers must be running**:
   ```bash
   docker ps | grep tdm
   ```

2. **Split script must be accessible**:
   ```bash
   /Users/jthijs/Documents/my_projects/taxxor/tdm/services/_grpc-migration-tools/split-from-monorepo.sh
   ```

3. **Local compilation must succeed** before testing:
   ```bash
   cd DocumentStore && dotnet build DocumentStore.sln
   cd ../Editor && dotnet build TaxxorEditor.sln
   ```

## Customization

### Adjust Max Iterations

Edit the skill file (`skills/grpc-migration-debugging.md`) to change:
- `MAX ITERATIONS: 5` (with test URL) - default for full testing
- `MAX ITERATIONS: 3` (without test URL) - default for compilation only

### Adjust Wait Times

If Docker hot-reload takes longer than 10 seconds on your system, adjust:
```
Wait: 10 seconds after seeing this message
```

### Add Custom Checks

Edit the skill to add additional verification steps, such as:
- Memory usage checks
- Custom log pattern searches
- Multiple endpoint tests in sequence

## Troubleshooting

**Skill not appearing**:
- Restart Claude Code
- Check file is in correct location: `.claude/skills/grpc-migration-debugging.md`
- Verify file has proper frontmatter with `name:` and `description:`

**Slash command not working**:
- Restart Claude Code
- Check file is in correct location: `.claude/commands/debug-grpc.md`
- Try invoking with `/` prefix

**Agent fails to run commands**:
- Check Docker containers are running: `docker ps`
- Verify paths in script match your system
- Check split script permissions: `chmod +x split-from-monorepo.sh`

**Agent reports false errors**:
- Check log patterns in skill definition
- Adjust grep patterns if too sensitive
- Consider Mode 2 (no URL) if endpoint isn't ready for testing

## Integration with Project Documentation

This setup is documented in the project's main `CLAUDE.md` under the section:
**"⚡ Automated Feedback Loop with Backend Developer Agent"**

The skill implements the specifications defined in that section.

---

**Created**: 2025-11-21
**Purpose**: Automate gRPC migration testing workflow
**Pattern**: Option A Enhanced (Semi-Autonomous)
