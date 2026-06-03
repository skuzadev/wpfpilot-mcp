using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.IntegrationTests;

public class DefaultMcpToolsTests
{
    [Fact]
    public void DefaultCatalog_HasExpectedToolCount()
    {
        Assert.InRange(DefaultMcpTools.Count, 35, 40);
        Assert.Equal(DefaultMcpTools.Count, DefaultMcpTools.Names.Count);
    }
}
