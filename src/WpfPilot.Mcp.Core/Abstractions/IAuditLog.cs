using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Core.Abstractions;

public interface IAuditLog
{
    void Record(AuditEntry entry);
    void Record(string tool, ElementCriteria? selector = null, string? path = null,
                Dictionary<string, object?>? parameters = null,
                string result = "success", string? error = null, long durationMs = 0);
    IReadOnlyList<AuditEntry> GetEntries(int? limit = null);
    void Clear();
    void Flush();
}
