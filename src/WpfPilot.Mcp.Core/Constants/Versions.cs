namespace WpfPilot.Mcp.Core.Constants;

public static class Versions
{
    public const int ProtocolMajor = 3;
    public const int ProtocolMinor = 0;
    public const string ProtocolVersion = "3.0";
    public const string ServerVersion = "3.0.0";
    public const string ProbeVersion = "3.0.0";

    public static readonly IReadOnlyList<string> DefaultProbeMethods = new[]
    {
        "ping",
        "get_datacontext",
        "get_viewmodel_properties",
        "get_binding_errors",
        "get_bindings",
        "get_command_state",
        "get_validation_state",
        "get_dispatcher_status",
        "execute_command"
    };
}
