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
            Tools = DefaultMcpTools.Descriptors.ToList()
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
                registeredToolCount = DefaultMcpTools.Count,
                totalVerbs = cap.Verbs.Count,
                totalQueryKinds = cap.QueryKinds.Count,
                totalWaitConditions = cap.WaitConditions.Count,
                extendedTools = "Set WPFPILOT_MCP_TOOLS=full for all tools (maintainers only; may break some clients)."
            }
        });
    }
}
