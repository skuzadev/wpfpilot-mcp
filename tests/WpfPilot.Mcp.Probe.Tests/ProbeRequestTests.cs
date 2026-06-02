using System.Text.Json;
using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Core.Models;
using Xunit;

namespace WpfPilot.Mcp.Probe.Tests;

public class ProbeRequestTests
{
    [Fact]
    public void ProbeContract_Defaults_Match_Versions()
    {
        var contract = new ProbeContract();
        Assert.Equal(Versions.ProbeVersion, contract.ProbeVersion);
        Assert.Equal(Versions.ProtocolVersion, contract.ProtocolVersion);
    }

    [Fact]
    public void ProbeRequest_Defaults_Match_Versions()
    {
        var request = new ProbeRequest();
        Assert.Equal(Versions.ProtocolVersion, request.ProtocolVersion);
        Assert.Equal(Versions.ServerVersion, request.ServerVersion);
    }
}
