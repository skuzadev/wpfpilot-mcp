namespace WpfPilot.Mcp.Core.Primitives;

public readonly record struct Result<T>(bool IsSuccess, T? Value, ErrorInfo? Error)
{
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string code, string message, Exception? cause = null)
        => new(false, default, new ErrorInfo(code, message, cause?.ToString()));
    public static Result<T> Fail(ErrorInfo error) => new(false, default, error);
}

public readonly record struct ErrorInfo(string Code, string Message, string? Detail = null)
{
    public static ErrorInfo NotAttached => new("not_attached", "No app attached. Use wpf_attach or wpf_launch_app first.");
    public static ErrorInfo ElementNotFound(string selector)
        => new("element_not_found", $"No element matched selector: {selector}");
    public static ErrorInfo PatternNotSupported(string pattern, string controlType)
        => new("pattern_not_supported", $"Element ({controlType}) does not support {pattern} pattern.");
    public static ErrorInfo Timeout(string op, int ms)
        => new("timeout", $"{op} timed out after {ms}ms.");
    public static ErrorInfo InvalidArgs(string what)
        => new("invalid_args", what);
    public static ErrorInfo Internal(string message, Exception? ex = null)
        => new("internal", message, ex?.ToString());
}

public static class ErrorCodes
{
    public const string NotAttached = "not_attached";
    public const string ElementNotFound = "element_not_found";
    public const string PatternNotSupported = "pattern_not_supported";
    public const string Timeout = "timeout";
    public const string InvalidArgs = "invalid_args";
    public const string Internal = "internal";
    public const string ProbeNotConnected = "probe_not_connected";
    public const string ProbeMethodNotAllowed = "probe_method_not_allowed";
    public const string HandshakeFailed = "handshake_failed";
}
