using Microsoft.Extensions.Logging;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Centralized exception → ErrorInfo mapper. All UIA, JSON, and probe exceptions
/// should flow through here so the wire format stays consistent.
/// </summary>
public sealed class ErrorMapper : IErrorMapper
{
    private readonly ILogger<ErrorMapper>? _logger;

    public ErrorMapper(ILogger<ErrorMapper>? logger = null)
    {
        _logger = logger;
    }

    public ErrorInfo FromUiaException(string operation, Exception ex)
    {
        _logger?.LogWarning(ex, "UIA error during {Operation}", operation);
        return ex switch
        {
            FlaUI.Core.Exceptions.ElementNotAvailableException
                => new ErrorInfo(ErrorCodes.ElementNotFound, $"{operation}: element not available (window may have closed)."),
            FlaUI.Core.Exceptions.NoClickablePointException
                => new ErrorInfo(ErrorCodes.PatternNotSupported, $"{operation}: element has no clickable point."),
            FlaUI.Core.Exceptions.PropertyNotSupportedException
                => new ErrorInfo(ErrorCodes.PatternNotSupported, $"{operation}: property not supported on element."),
            FlaUI.Core.Exceptions.PatternNotSupportedException p
                => new ErrorInfo(ErrorCodes.PatternNotSupported, $"{operation}: pattern {p.Pattern?.Name ?? p.Message} not supported."),
            System.TimeoutException
                => FromTimeout(operation, TimeSpan.FromSeconds(30)),
            System.IO.IOException io
                => new ErrorInfo(ErrorCodes.Internal, $"{operation}: IO error: {io.Message}", io.ToString()),
            _ => new ErrorInfo(ErrorCodes.Internal, $"{operation}: {ex.Message}", ex.ToString())
        };
    }

    public ErrorInfo FromTimeout(string operation, TimeSpan elapsed)
    {
        _logger?.LogWarning("{Operation} timed out after {ElapsedMs}ms", operation, elapsed.TotalMilliseconds);
        return new ErrorInfo(ErrorCodes.Timeout, $"{operation} timed out after {(int)elapsed.TotalMilliseconds}ms.");
    }

    public ErrorInfo FromProbe(string operation, string? code, string message)
    {
        _logger?.LogWarning("Probe error during {Operation}: {Code} {Message}", operation, code, message);
        return new ErrorInfo(code ?? ErrorCodes.Internal, $"{operation}: {message}");
    }

    public ErrorInfo FromJson(string operation, Exception ex)
    {
        _logger?.LogWarning(ex, "JSON error during {Operation}", operation);
        return new ErrorInfo(ErrorCodes.Internal, $"{operation}: invalid JSON - {ex.Message}", ex.ToString());
    }

    public ErrorInfo FromInvalidArgs(string what)
    {
        return new ErrorInfo(ErrorCodes.InvalidArgs, what);
    }
}
