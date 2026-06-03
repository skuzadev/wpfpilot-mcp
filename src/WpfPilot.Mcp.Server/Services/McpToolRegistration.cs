using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace WpfPilot.Mcp.Server.Services;

public static class McpToolRegistration
{
    public static IMcpServerBuilder WithDefaultWpfPilotTools(
        this IMcpServerBuilder builder,
        Assembly? assembly = null)
    {
        assembly ??= typeof(DefaultMcpTools).Assembly;
        var toolTypes = new HashSet<Type>();
        var tools = new List<McpServerTool>();
        var registeredNames = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
                continue;
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is not { } toolAttr)
                    continue;

                var toolName = toolAttr.Name ?? method.Name;
                if (!DefaultMcpTools.Names.Contains(toolName))
                    continue;

                toolTypes.Add(type);
                registeredNames.Add(toolName);
                tools.Add(McpServerTool.Create(
                    method,
                    (RequestContext<CallToolRequestParams> ctx) => ctx.Services!.GetRequiredService(type),
                    null));
            }
        }

        if (registeredNames.Count != DefaultMcpTools.Count)
        {
            var missing = DefaultMcpTools.Names.Except(registeredNames).OrderBy(n => n).ToList();
            var extra = registeredNames.Except(DefaultMcpTools.Names).OrderBy(n => n).ToList();
            throw new InvalidOperationException(
                $"Default MCP tool registration mismatch: expected {DefaultMcpTools.Count}, registered {registeredNames.Count}. " +
                $"Missing: [{string.Join(", ", missing)}]. Extra: [{string.Join(", ", extra)}].");
        }

        foreach (var type in toolTypes)
            builder.Services.AddSingleton(type);

        return builder.WithTools(tools);
    }
}
