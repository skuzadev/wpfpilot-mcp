using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Core.Models;
using Xunit;

namespace WpfPilot.Mcp.Core.Tests;

public class SessionInfoTests
{
    [Fact]
    public void ProtocolVersion_Default_Matches_Versions()
    {
        Assert.Equal(Versions.ProtocolVersion, new SessionInfo().ProtocolVersion);
    }
}
