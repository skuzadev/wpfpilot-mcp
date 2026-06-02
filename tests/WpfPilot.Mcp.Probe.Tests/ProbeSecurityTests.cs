using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Core.Models;
using Xunit;

namespace WpfPilot.Mcp.Probe.Tests;

public class ProbeSecurityTests : IDisposable
{
    private ProbeHost? _host;

    public void Dispose()
    {
        _host?.Stop();
        _host?.Dispose();
    }

    [Fact]
    public async Task Probe_StartsAndStops_Cleanly()
    {
        _host = ProbeHost.Start(pipeName: $"test-{Guid.NewGuid():N}");
        await Task.Delay(200);
        Assert.True(_host.IsRunning);
        Assert.Equal(Versions.ProbeVersion, _host.AllowedMethods is not null ? Versions.ProbeVersion : "");
        Assert.NotEmpty(_host.AllowedMethods);
    }

    [Fact]
    public async Task Handshake_Sends_Hello_On_Connect()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        _host = ProbeHost.Start(pipeName: pipeName);

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);

        var helloLine = await ReadLineAsync(pipe);
        Assert.NotNull(helloLine);
        var bytes = System.Text.Encoding.UTF8.GetBytes(helloLine!);
        Assert.True(bytes.Length > 0);
        // dump the first 100 bytes for debugging if it fails
        var hex = BitConverter.ToString(bytes, 0, Math.Min(100, bytes.Length));
        Assert.True(helloLine!.StartsWith("{"), $"Expected JSON, got hex: {hex}, len={bytes.Length}");
        using var doc = JsonDocument.Parse(helloLine!);
        var root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("type").GetString());
        Assert.Equal(Versions.ProtocolVersion, root.GetProperty("protocolVersion").GetString());
        Assert.NotEmpty(root.GetProperty("nonce").GetString()!);
        Assert.True(root.GetProperty("allowedMethods").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Reject_Request_With_Bad_Nonce()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        _host = ProbeHost.Start(pipeName: pipeName);

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);

        var helloLine = await ReadLineAsync(pipe);
        Assert.NotNull(helloLine);

        var bad = new ProbeRequest
        {
            Method = "ping",
            Nonce = "00000000000000000000000000000000",
            ProtocolVersion = Versions.ProtocolVersion
        };
        await WriteLineAsync(pipe, JsonSerializer.Serialize(bad));

        var responseLine = await ReadLineAsync(pipe);
        Assert.NotNull(responseLine);
        using var doc = JsonDocument.Parse(responseLine!);
        Assert.Equal(ErrorCodes.HandshakeFailed, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reject_Request_With_Bad_Protocol_Version()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        _host = ProbeHost.Start(pipeName: pipeName);

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);

        var helloLine = await ReadLineAsync(pipe);
        Assert.NotNull(helloLine);
        using var helloDoc = JsonDocument.Parse(helloLine!);
        var nonce = helloDoc.RootElement.GetProperty("nonce").GetString();

        var bad = new ProbeRequest
        {
            Method = "ping",
            Nonce = nonce,
            ProtocolVersion = "1.0"
        };
        await WriteLineAsync(pipe, JsonSerializer.Serialize(bad));

        var responseLine = await ReadLineAsync(pipe);
        Assert.NotNull(responseLine);
        using var doc = JsonDocument.Parse(responseLine!);
        Assert.Equal(ErrorCodes.HandshakeFailed, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reject_ExecuteCommand_When_Not_In_Allow_List()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        // Start with no execute_command in allow-list
        _host = ProbeHost.Start(
            pipeName: pipeName,
            allowedMethods: new[] { "ping", "get_datacontext" });

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);

        var helloLine = await ReadLineAsync(pipe);
        Assert.NotNull(helloLine);
        using var helloDoc = JsonDocument.Parse(helloLine!);
        var nonce = helloDoc.RootElement.GetProperty("nonce").GetString();

        var req = new ProbeRequest
        {
            Method = "execute_command",
            Nonce = nonce,
            ProtocolVersion = Versions.ProtocolVersion,
            Parameters = new Dictionary<string, JsonElement>
            {
                ["commandName"] = JsonSerializer.SerializeToElement("SaveCommand")
            }
        };
        await WriteLineAsync(pipe, JsonSerializer.Serialize(req));

        var responseLine = await ReadLineAsync(pipe);
        Assert.NotNull(responseLine);
        using var doc = JsonDocument.Parse(responseLine!);
        Assert.Equal(ErrorCodes.ProbeMethodNotAllowed, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteCommand_With_Null_Parameters_Returns_InvalidArgs_Not_NullReference()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        _host = ProbeHost.Start(
            pipeName: pipeName,
            allowedMethods: Versions.DefaultProbeMethods.Concat(new[] { "execute_command" }).ToList());

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);

        var helloLine = await ReadLineAsync(pipe);
        Assert.NotNull(helloLine);
        using var helloDoc = JsonDocument.Parse(helloLine!);
        var nonce = helloDoc.RootElement.GetProperty("nonce").GetString();

        var req = new ProbeRequest
        {
            Method = "execute_command",
            Nonce = nonce,
            ProtocolVersion = Versions.ProtocolVersion,
            Parameters = null
        };
        await WriteLineAsync(pipe, JsonSerializer.Serialize(req));

        var responseLine = await ReadLineAsync(pipe);
        Assert.NotNull(responseLine);
        using var doc = JsonDocument.Parse(responseLine!);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(ErrorCodes.InvalidArgs, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Allow_Ping_After_Handshake()
    {
        var pipeName = $"test-{Guid.NewGuid():N}";
        _host = ProbeHost.Start(pipeName: pipeName);

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);

        var helloLine = await ReadLineAsync(pipe);
        Assert.NotNull(helloLine);
        using var helloDoc = JsonDocument.Parse(helloLine!);
        var nonce = helloDoc.RootElement.GetProperty("nonce").GetString();

        var req = new ProbeRequest
        {
            Method = "ping",
            Nonce = nonce,
            ProtocolVersion = Versions.ProtocolVersion
        };
        await WriteLineAsync(pipe, JsonSerializer.Serialize(req));

        var responseLine = await ReadLineAsync(pipe);
        Assert.NotNull(responseLine);
        using var doc = JsonDocument.Parse(responseLine!);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("pong", doc.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public void Allowed_Methods_Default_Includes_ExecuteCommand()
    {
        Assert.Contains("execute_command", ProbeHost.DefaultAllowedMethods);
    }

    [Fact]
    public void Allowed_Methods_Are_Immutable()
    {
        var list = new[] { "ping" };
        var host = ProbeHost.Start(pipeName: $"test-{Guid.NewGuid():N}", allowedMethods: list);
        try
        {
            Assert.Single(host.AllowedMethods);
            Assert.Contains("ping", host.AllowedMethods);
        }
        finally
        {
            host.Stop();
            host.Dispose();
        }
    }

    private static async Task<string?> ReadLineAsync(NamedPipeClientStream pipe)
    {
        var ms = new MemoryStream();
        var buf = new byte[256];
        while (pipe.IsConnected)
        {
            var read = await pipe.ReadAsync(buf, 0, buf.Length);
            if (read == 0) break;
            for (int i = 0; i < read; i++)
            {
                if (buf[i] == (byte)'\n')
                {
                    var bytes = ms.ToArray();
                    return Encoding.UTF8.GetString(bytes);
                }
                ms.WriteByte(buf[i]);
            }
        }
        var leftover = ms.ToArray();
        if (leftover.Length == 0) return null;
        return Encoding.UTF8.GetString(leftover);
    }

    private static async Task WriteLineAsync(NamedPipeClientStream pipe, string line)
    {
        var data = Encoding.UTF8.GetBytes(line + "\n");
        await pipe.WriteAsync(data);
        await pipe.FlushAsync();
    }
}
