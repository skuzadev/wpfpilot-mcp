using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Server.Services;
using WpfPilot.Mcp.Server.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Redirect logging to stderr so stdout is reserved for JSON-RPC messages
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<AuditLog>();
builder.Services.AddSingleton<UiaAdapter>();
builder.Services.AddSingleton<SelectorBuilder>();
builder.Services.AddSingleton<ScreenshotService>();
builder.Services.AddSingleton<RecordingService>();
builder.Services.AddSingleton<ProbeClient>();
builder.Services.AddSingleton<DevWatcherService>();
builder.Services.AddSingleton<ErrorMapper>();
builder.Services.AddSingleton<UiActionEngine>();

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "WpfPilot MCP",
        Version = Versions.ServerVersion
    };
})
.WithStdioServerTransport()
.WithToolsFromAssembly();

await builder.Build().RunAsync();
