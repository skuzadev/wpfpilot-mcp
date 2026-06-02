# Architecture

Detailed architecture and design decisions for the WpfPilot MCP Server.

---

## System Overview

The WpfPilot MCP Server is a .NET 8 console application that communicates via **stdio** (JSON-RPC) with MCP clients. It provides semantic UI automation for WPF applications through two complementary approaches:

1. **Black-box automation** — via Microsoft UI Automation / FlaUI (external process inspection)
2. **White-box diagnostics** — via an optional in-process probe (named pipe IPC)

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MCP Client Layer                             │
│  (VS Code Copilot, Claude Desktop, custom MCP clients)              │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ stdin/stdout (JSON-RPC)
┌────────────────────────────────▼────────────────────────────────────┐
│                    WpfPilot MCP Server                              │
│                                                                     │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────────┐     │
│  │   Tool    │  │   Tool    │  │   Tool    │  │   Tool        │     │
│  │  Classes  │  │  Classes  │  │  Classes  │  │   Classes     │     │
│  └─────┬─────┘  └──────┬────┘  └──────┬────┘  └─────────┬─────┘     │
│        │               │              │                 │           │
│  ┌─────▼───────────────▼──────────────▼─────────────────▼────────┐  │
│  │                    Service Layer                              │  │
│  │                                                               │  │
│  │  SessionManager │ UiaAdapter │ SelectorBuilder │ AuditLog     │  │
│  │  RecordingService │ ScreenshotService │ ProbeClient           │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                     │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            │               │               │
            ▼               ▼               ▼
    ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
    │ UI Automation│ │  GDI+ Screen │ │  Named Pipe  │
    │    (FlaUI)   │ │   Capture    │ │   (Probe)    │
    └──────┬───────┘ └──────────────┘ └───────┬──────┘
           │                                  │
           ▼                                  ▼
    ┌──────────────────────────────────────────────────┐
    │              Target WPF Application              │
    │                                                  │
    │  ┌────────────────────────────────────────────┐  │
    │  │  AutomationPeers (UIA bridge)              │  │
    │  └────────────────────────────────────────────┘  │
    │  ┌────────────────────────────────────────────┐  │
    │  │  ProbeHost (optional, in-process)          │  │
    │  └────────────────────────────────────────────┘  │
    └──────────────────────────────────────────────────┘
```

---

## Design Principles

### 1. Semantic Over Pixel-Based

Every action targets elements by **meaning** (AutomationId, Name, ControlType), never by screen coordinates. This makes automation resistant to:
- Window resizing/repositioning
- DPI changes
- Theme changes
- Minor layout adjustments

### 2. Selector Stability Hierarchy

```
AutomationId  →  Most stable (survives refactoring if developers maintain it)
     ↓
Name          →  Stable but may break with localization
     ↓
ControlType + ClassName  →  Structural, less specific
     ↓
Index path    →  Brittle, last resort (flagged as warning)
```

### 3. Black-Box First, White-Box Optional

The server is fully functional with **zero changes** to the target application. The probe is an enhancement for teams that want deeper diagnostics.

### 4. Safety by Default

- No OS-level access (file system, registry, shell)
- All actions scoped to the attached application
- Audit log for all mutating operations
- Dry-run mode available
- Configurable redaction for sensitive data

---

## Service Layer

### SessionManager

Manages the lifecycle of app attachment:
- Launches new processes or attaches to existing ones
- Holds the FlaUI `Application` and `UIA3Automation` instances
- Tracks the active window and session metadata
- Thread-safe session state

### UiaAdapter

Wraps FlaUI to provide:
- `CaptureSnapshot(maxDepth)` — tree traversal with configurable depth
- `FindElement(criteria)` — single element resolution
- `FindElements(criteria, root)` — multi-element queries
- `QueryElements(...)` — flexible search with multiple criteria
- `ResolveSelector(selector)` — full selector resolution

### SelectorBuilder

Generates and validates element selectors:
- `BuildSelector(element)` — creates best selector for given element
- `ValidateSelector(selector)` — checks uniqueness
- `ValidateSelectorUniqueness(selector)` — detailed uniqueness report
- `RankSelectors(element)` — produces ranked list of possible selectors

### RecordingService

Workflow recording engine:
- Records actions as semantic steps (not coordinates)
- Supports pause/resume
- Generates xUnit + FlaUI test code from recordings
- Schema-versioned workflow JSON format

### ScreenshotService

Screen capture using GDI+:
- Window capture (full window)
- Element capture (cropped to bounds)
- Window bounds helper for annotation overlays

### ProbeClient

Named pipe IPC client:
- Connects to in-process probe
- Sends JSON requests, receives JSON responses
- Auto-discovery by process ID (`wpfpilot-mcp-probe-{PID}`)

### AuditLog

Thread-safe action logging:
- Records tool name, parameters, timestamp, result
- Queryable log for diagnostics
- Clearable (with audit of the clear itself)

---

## Tool Registration

Tools are discovered automatically via `WithToolsFromAssembly()`. Each tool class:

1. Has `[McpServerToolType]` attribute
2. Receives services via constructor injection
3. Each method has `[McpServerTool(Name = "tool_name")]` and `[Description("...")]`
4. Returns JSON-serialized results

```csharp
[McpServerToolType]
public sealed class SessionTools
{
    private readonly SessionManager _session;
    private readonly AuditLog _audit;

    public SessionTools(SessionManager session, AuditLog audit) { ... }

    [McpServerTool(Name = "wpf_attach"), Description("Attach to a running process.")]
    public string Attach(string? processName = null, int? processId = null) { ... }
}
```

---

## Data Flow

### Typical Tool Invocation

```
1. MCP Client sends JSON-RPC request → stdin
2. MCP SDK deserializes → routes to tool method
3. Tool method:
   a. Records to AuditLog
   b. Calls service layer (UiaAdapter, SessionManager, etc.)
   c. Service interacts with UIA/Probe/GDI+
   d. Returns JSON response
4. MCP SDK serializes → writes to stdout
5. MCP Client receives response
```

### Probe Communication

```
1. Tool calls ProbeClient.SendAsync("method", params)
2. ProbeClient writes JSON line to named pipe
3. ProbeHost (in target app) reads line
4. ProbeHost dispatches to UI thread via Dispatcher.InvokeAsync
5. Handler inspects ViewModel/bindings/commands
6. ProbeHost writes JSON response line
7. ProbeClient reads and returns to tool
```

---

## Threading Model

| Component | Thread |
|-----------|--------|
| MCP server (stdio) | Main thread |
| FlaUI/UIA calls | Main thread (COM apartment) |
| ProbeClient pipe I/O | Async (Task-based) |
| ProbeHost listener | Background Task |
| ProbeHost handlers | WPF Dispatcher thread |
| Screenshot capture | Main thread (GDI+) |

---

## Verb DSL (protocol 3.0)

Primary client surface:

- `wpf_act` — `ActionRequest` with `verb` + `selector` / `path`
- `wpf_query` — `QueryRequest` with `kind` + `selector` / `path`
- `wpf_wait` — `WaitRequest` with `condition` + `selector` / `path`
- `wpf_assert` — `AssertRequest` with `condition` + `selector` / `path`
- `wpf_capabilities` — discovery document (verbs, kinds, conditions)

Shared implementation lives in `WpfPilot.Mcp.Core` (DSL catalogs, models) and server services (`UiaAdapter`, `UiActionEngine`, `ErrorMapper`).

---

## Error Handling Strategy

- **Tool-level**: Structured errors via `ToolJson` — `{ error: { code, message, detail? } }`
- **Service-level**: Services throw on invalid state (e.g., "not attached")
- **Probe-level**: Pipe disconnection handled gracefully with reconnect
- **No global exception swallowing** — errors propagate to MCP response

---

## Extensibility Points

1. **New tools**: Add a class with `[McpServerToolType]` — auto-discovered
2. **New probe methods**: Add handler in `ProbeHost.ProcessRequest` switch
3. **New services**: Register in `Program.cs` as singleton
4. **Custom policies**: Extend `RecordingPolicy` model
5. **Selector strategies**: Extend `SelectorBuilder` ranking logic
