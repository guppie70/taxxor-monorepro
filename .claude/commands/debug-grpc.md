# Debug gRPC Migration

Use the gRPC migration debugging workflow with automated testing agent.

## Usage

This command launches the automated debugging workflow for gRPC migrations. You have two options:

### Option 1: With Test URL (Full Testing)

Provide a curl command or endpoint URL to test:

```
/debug-grpc https://editor:8071/api/your-endpoint?params
```

**IMPORTANT**: For Editor endpoints, you can use either `https://editor/` or `https://editor:8071/`. The agent will automatically transform `https://editor/` to `https://editor:8071/` (authentication proxy) when testing.

The agent will:
- Publish changes via split-from-monorepo.sh
- Monitor Docker compilation
- Test the endpoint with curl (with automatic URL transformation for Editor)
- Check logs for errors
- Report results and iterate

### Option 2: Without Test URL (Compilation Only)

Just check compilation and runtime health:

```
/debug-grpc
```

The agent will:
- Publish changes via split-from-monorepo.sh
- Monitor Docker compilation
- Check logs for startup errors
- Report results

## What Happens

1. **You implement** the fix or feature
2. **Agent launches** and handles all testing automatically
3. **Agent reports** success or errors
4. **You fix** any reported issues
5. **Agent retests** automatically
6. Repeat until success

## Parameters

- `testUrl` (optional): Full curl command or endpoint URL
- Defaults to Mode 2 (compilation only) if no URL provided

## Example

```bash
# Test a specific endpoint
/debug-grpc https://editor:8071/api/hierarchymanager/tools/sectionlanguageclone?did=1153794-message-from-the-ceo1&sourcelang=en&targetlang=zh&includechildren=false&ocvariantid=arpdfen&octype=pdf&oclang=en&pid=ar24&vid=1&ctype=regular&token=TOKEN&type=json

# Or just verify compilation
/debug-grpc
```

## Notes

- Always verify local compilation succeeds before using this command
- Agent will iterate up to 5 times (with URL) or 3 times (without URL)
- You can stop the agent at any time if you need to investigate manually
