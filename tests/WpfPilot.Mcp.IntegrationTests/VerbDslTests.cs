using System.Text.Json;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Server.Services;
using WpfPilot.Mcp.Server.Tools;

namespace WpfPilot.Mcp.IntegrationTests;

public class VerbDslTests
{
    private readonly SessionManager _session = new();
    private readonly AuditLog _audit = new();
    private readonly UiaAdapter _uia;
    private readonly RecordingService _recording;
    private readonly ErrorMapper _errors = new();
    private readonly UiActionEngine _actions;

    public VerbDslTests()
    {
        _uia = new UiaAdapter(_session);
        _recording = new RecordingService(_session);
        _actions = new UiActionEngine(_uia, _recording);
    }

    [Fact]
    public void Capabilities_Returns_Protocol3_And_VerbLists()
    {
        var tools = new CapabilitiesTools(_recording);
        var json = JsonDocument.Parse(tools.Capabilities());
        Assert.Equal("3.0", json.RootElement.GetProperty("protocolVersion").GetString());
        Assert.True(json.RootElement.GetProperty("verbs").GetArrayLength() > 0);
        Assert.True(json.RootElement.GetProperty("queryKinds").GetArrayLength() > 0);
        Assert.False(json.RootElement.TryGetProperty("deprecated", out _));
    }

    [Fact]
    public void Act_DryRun_DoesNotRequireAttachment()
    {
        var tools = new ActTools(_uia, _audit, _recording, _errors, _actions);
        var result = tools.Act(new ActionRequest { Verb = "click", DryRun = true, Selector = new() { AutomationId = "x" } });
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("dryRun").GetBoolean());
    }

    [Fact]
    public void Act_UnknownVerb_ReturnsStructuredError()
    {
        var tools = new ActTools(_uia, _audit, _recording, _errors, _actions);
        var result = tools.Act(new ActionRequest { Verb = "not_a_verb" });
        var json = JsonDocument.Parse(result);
        Assert.Equal("invalid_args", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Query_UnknownKind_ReturnsStructuredError()
    {
        var tools = new QueryTools(_uia, _audit);
        var result = tools.Query(new QueryRequest { Kind = "nope", Selector = new() { Name = "a" } });
        var json = JsonDocument.Parse(result);
        Assert.Equal("invalid_args", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Wait_UnknownCondition_ReturnsStructuredError()
    {
        var tools = new WaitTools(_uia, _session, _audit);
        var result = tools.Wait(new WaitRequest { Condition = "nope" });
        var json = JsonDocument.Parse(result);
        Assert.Equal("invalid_args", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Assert_UnknownCondition_ReturnsFail()
    {
        var tools = new AssertionTools(_uia, _audit);
        var result = tools.Assert(new AssertRequest { Condition = "nope" });
        var json = JsonDocument.Parse(result);
        Assert.Equal("fail", json.RootElement.GetProperty("assertion").GetString());
    }
}
