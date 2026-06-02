using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Core.Abstractions;

public interface IErrorMapper
{
    ErrorInfo FromUiaException(string operation, Exception ex);
    ErrorInfo FromTimeout(string operation, TimeSpan elapsed);
    ErrorInfo FromProbe(string operation, string? code, string message);
    ErrorInfo FromJson(string operation, Exception ex);
    ErrorInfo FromInvalidArgs(string what);
}
