using WpfPilot.Mcp.Core.Dsl;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Curated default MCP tool catalog (~39 tools) for client compatibility.
/// </summary>
public static class DefaultMcpTools
{
    public static IReadOnlyList<ToolDescriptor> Descriptors { get; } =
    [
        // Meta / verb DSL
        new() { Name = "wpf_capabilities", Description = "Discover server version, verb DSL, and registered tool catalog.", Category = "meta" },
        new() { Name = "wpf_act", Description = "Verb-driven UI actions (click, set_value, select, etc.).", Category = "verb-dsl" },
        new() { Name = "wpf_query", Description = "Verb-driven UI reads (text, bounds, state, children, find).", Category = "verb-dsl" },
        new() { Name = "wpf_wait", Description = "Wait until a UI condition is met.", Category = "verb-dsl" },
        new() { Name = "wpf_assert", Description = "Assert a UI condition; returns pass/fail.", Category = "verb-dsl" },

        // Session
        new() { Name = "wpf_list_apps", Description = "List candidate WPF/desktop processes and top-level windows.", Category = "session" },
        new() { Name = "wpf_launch_app", Description = "Launch app from executable path with optional args.", Category = "session" },
        new() { Name = "wpf_attach", Description = "Attach to a running process/window by PID, name, or title.", Category = "session" },
        new() { Name = "wpf_detach", Description = "Detach from current app session.", Category = "session" },
        new() { Name = "wpf_session_status", Description = "Current attachment, window handle, PID, and app state.", Category = "session" },
        new() { Name = "wpf_focus_window", Description = "Bring attached app/window to foreground.", Category = "session" },
        new() { Name = "wpf_list_windows", Description = "List top-level, modal, popup, and child windows.", Category = "session" },
        new() { Name = "wpf_select_window", Description = "Switch active target window by title or automation id.", Category = "session" },
        new() { Name = "wpf_close_window", Description = "Close a window through normal close command.", Category = "session" },
        new() { Name = "wpf_get_window_state", Description = "Get minimized/maximized/normal/focused/modal state.", Category = "session" },
        new() { Name = "wpf_set_window_state", Description = "Minimize, maximize, or restore a window.", Category = "session" },
        new() { Name = "wpf_get_app_metadata", Description = "App version, executable path, bitness, framework if detectable.", Category = "session" },

        // Snapshot
        new() { Name = "wpf_snapshot", Description = "Compact UI tree for the active window.", Category = "discovery" },
        new() { Name = "wpf_snapshot_element", Description = "UI subtree rooted at an element.", Category = "discovery" },
        new() { Name = "wpf_diff_snapshot", Description = "Compare two snapshots; report added/removed/changed.", Category = "discovery" },
        new() { Name = "wpf_watch_ui_changes", Description = "Poll the UI tree for changes over durationMs.", Category = "discovery" },

        // Screenshot
        new() { Name = "wpf_screenshot", Description = "Capture current window screenshot (base64 PNG).", Category = "capture" },
        new() { Name = "wpf_screenshot_element", Description = "Capture screenshot of selected element.", Category = "capture" },
        new() { Name = "wpf_highlight_element", Description = "Flash-highlight an element for visual debugging.", Category = "capture" },
        new() { Name = "wpf_capture_failure_artifacts", Description = "Screenshot, snapshot, and diagnostics after a failure.", Category = "capture" },

        // Selectors
        new() { Name = "wpf_build_selector", Description = "Generate stable selector for an element.", Category = "selectors" },
        new() { Name = "wpf_validate_selector", Description = "Test whether a selector resolves uniquely.", Category = "selectors" },
        new() { Name = "wpf_heal_selector", Description = "Find replacement when a selector no longer resolves.", Category = "selectors" },
        new() { Name = "wpf_explain_selector", Description = "Explain how a selector resolves and brittleness risks.", Category = "selectors" },

        // Probe
        new() { Name = "wpf_probe_status", Description = "Check if in-process probe is connected.", Category = "probe" },
        new() { Name = "wpf_probe_connect", Description = "Connect to in-process probe via named pipe.", Category = "probe" },
        new() { Name = "wpf_probe_install_instructions", Description = "Instructions for installing probe NuGet in a WPF app.", Category = "probe" },

        // Diagnostics
        new() { Name = "wpf_why_disabled", Description = "Explain why an element is disabled.", Category = "diagnostics" },
        new() { Name = "wpf_explain_screen", Description = "AI-friendly summary of the current screen.", Category = "diagnostics" },

        // Recording
        new() { Name = "wpf_record_start", Description = "Start recording UI actions.", Category = "recording" },
        new() { Name = "wpf_record_stop", Description = "Stop recording and return workflow JSON.", Category = "recording" },
        new() { Name = "wpf_export_recording", Description = "Export workflow as JSON.", Category = "recording" },
        new() { Name = "wpf_replay", Description = "Replay recorded workflow JSON.", Category = "recording" },
        new() { Name = "wpf_export_test", Description = "Generate xUnit + FlaUI test code from recording JSON.", Category = "recording" },
    ];

    public static IReadOnlySet<string> Names { get; } = Descriptors.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

    public static int Count => Descriptors.Count;
}
