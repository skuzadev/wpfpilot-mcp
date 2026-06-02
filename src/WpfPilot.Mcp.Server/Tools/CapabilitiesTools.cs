using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class CapabilitiesTools
{
    private readonly RecordingService _recording;

    public CapabilitiesTools(RecordingService recording)
    {
        _recording = recording;
    }

    [McpServerTool(Name = "wpf_capabilities"),
     Description("Discover server version, verb DSL (act/query/wait/assert), and tool catalog.")]
    public string Capabilities()
    {
        var cap = new Capabilities
        {
            ServerVersion = Versions.ServerVersion,
            ProtocolVersion = Versions.ProtocolVersion,
            Verbs = ActionVerbCatalog.ByName.Keys.OrderBy(k => k).ToList(),
            QueryKinds = QueryKindCatalog.ByName.Keys.OrderBy(k => k).ToList(),
            WaitConditions = WaitConditionCatalog.ByName.Keys.OrderBy(k => k).ToList(),
            Tools =
            [
                new() { Name = "wpf_capabilities", Description = "This document.", Category = "meta" },
                new() { Name = "wpf_act", Description = "Verb-driven UI actions.", Category = "verb-dsl" },
                new() { Name = "wpf_query", Description = "Verb-driven UI reads.", Category = "verb-dsl" },
                new() { Name = "wpf_wait", Description = "Verb-driven waits.", Category = "verb-dsl" },
                new() { Name = "wpf_assert", Description = "Verb-driven assertions.", Category = "verb-dsl" },
                new() { Name = "wpf_attach", Description = "Attach to a process.", Category = "session" },
                new() { Name = "wpf_detach", Description = "Detach session.", Category = "session" },
                new() { Name = "wpf_launch_app", Description = "Launch and attach.", Category = "session" },
                new() { Name = "wpf_list_apps", Description = "List desktop apps with windows.", Category = "session" },
                new() { Name = "wpf_snapshot", Description = "UI tree snapshot.", Category = "discovery" },
                new() { Name = "wpf_build_selector", Description = "Build stable selector.", Category = "selectors" },
                new() { Name = "wpf_record_start", Description = "Start workflow recording.", Category = "recording" },
                new() { Name = "wpf_export_test", Description = "Export recording as test code.", Category = "recording" },
                new() { Name = "wpf_why_disabled", Description = "Explain disabled control (probe optional).", Category = "diagnostics" },
                new() { Name = "wpf_screenshot", Description = "Capture screenshot.", Category = "capture" }
            ]
        };

        return ToolJson.Ok(new
        {
            serverVersion = cap.ServerVersion,
            protocolVersion = cap.ProtocolVersion,
            verbs = cap.Verbs,
            queryKinds = cap.QueryKinds,
            waitConditions = cap.WaitConditions,
            tools = cap.Tools,
            meta = new
            {
                recording = _recording.IsRecording ? "recording" : "idle",
                totalVerbs = cap.Verbs.Count,
                totalQueryKinds = cap.QueryKinds.Count,
                totalWaitConditions = cap.WaitConditions.Count
            }
        });
    }
}
