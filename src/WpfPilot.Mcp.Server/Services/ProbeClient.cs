using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Client for communicating with the in-process probe via named pipe.
/// Performs a protocol handshake on connect: hello {nonce, allowedMethods}
/// from the probe, then the client echoes nonce+version on every call.
/// </summary>
public sealed class ProbeClient : IProbeTransport
{
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private string? _pipeName;
    private string? _nonce;
    private HashSet<string>? _allowedMethods;
    private readonly object _lock = new();

    public bool IsConnected
    {
        get
        {
            lock (_lock)
                return _pipe?.IsConnected == true;
        }
    }
    public string? PipeName { get { lock (_lock) return _pipeName; } }
    public ProbeContract? Contract { get { lock (_lock) return BuildContract(); } }

    public async Task<Result<bool>> ConnectAsync(int processId, int timeoutMs = 3000, CancellationToken ct = default)
        => await ConnectInternalAsync($"wpfpilot-mcp-probe-{processId}", timeoutMs, ct);

    public async Task<Result<bool>> ConnectAsync(string pipeName, int timeoutMs = 3000, CancellationToken ct = default)
        => await ConnectInternalAsync(pipeName, timeoutMs, ct);

    private async Task<Result<bool>> ConnectInternalAsync(string pipeName, int timeoutMs, CancellationToken ct)
    {
        try
        {
            Disconnect();
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutMs, ct);

            using var helloCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            helloCts.CancelAfter(timeoutMs);

            var reader = new StreamReader(pipe, Encoding.UTF8);
            var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };

            // Read probe's hello
            var helloLine = await reader.ReadLineAsync(helloCts.Token);
            if (helloLine is null)
            {
                DisposeConnectResources(pipe, reader, writer);
                return Result<bool>.Fail(ErrorCodes.HandshakeFailed, "Probe closed before sending hello");
            }

            using var helloDoc = JsonDocument.Parse(helloLine);
            var root = helloDoc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "hello")
            {
                DisposeConnectResources(pipe, reader, writer);
                return Result<bool>.Fail(ErrorCodes.HandshakeFailed, "First message was not a hello");
            }
            if (!root.TryGetProperty("nonce", out var nonceEl) ||
                nonceEl.ValueKind != JsonValueKind.String)
            {
                DisposeConnectResources(pipe, reader, writer);
                return Result<bool>.Fail(ErrorCodes.HandshakeFailed, "Hello missing nonce");
            }
            var nonce = nonceEl.GetString();
            if (string.IsNullOrEmpty(nonce))
            {
                DisposeConnectResources(pipe, reader, writer);
                return Result<bool>.Fail(ErrorCodes.HandshakeFailed, "Hello nonce must be a non-empty string");
            }
            if (!root.TryGetProperty("protocolVersion", out var protocolEl) ||
                protocolEl.GetString() != Versions.ProtocolVersion)
            {
                DisposeConnectResources(pipe, reader, writer);
                return Result<bool>.Fail(ErrorCodes.HandshakeFailed,
                    $"Protocol mismatch: client={Versions.ProtocolVersion}, probe={protocolEl.GetString()}");
            }
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("allowedMethods", out var methodsEl))
            {
                foreach (var m in methodsEl.EnumerateArray())
                {
                    var s = m.GetString();
                    if (s is not null) allowed.Add(s);
                }
            }

            lock (_lock)
            {
                _pipe = pipe;
                _reader = reader;
                _writer = writer;
                _pipeName = pipeName;
                _nonce = nonce;
                _allowedMethods = allowed;
            }
            return Result<bool>.Ok(true);
        }
        catch (OperationCanceledException)
        {
            Disconnect();
            return Result<bool>.Fail(ErrorCodes.Timeout, "Probe handshake timed out");
        }
        catch (Exception ex)
        {
            Disconnect();
            return Result<bool>.Fail(ErrorCodes.Internal, $"Probe connect failed: {ex.Message}");
        }
    }

    public async Task<Result<ProbeResponse>> TrySendAsync(string method, IReadOnlyDictionary<string, object?>? parameters = null, int timeoutMs = 10000, CancellationToken ct = default) => await SendAsyncInternal(method, parameters, timeoutMs, ct);

    async Task<Result<ProbeResponse>> IProbeTransport.SendAsync(string method, IReadOnlyDictionary<string, object?>? parameters, int timeoutMs, CancellationToken ct)
        => await SendAsyncInternal(method, parameters, timeoutMs, ct);

    private async Task<Result<ProbeResponse>> SendAsyncInternal(string method, IReadOnlyDictionary<string, object?>? parameters, int timeoutMs, CancellationToken ct)
    {
        StreamWriter? writer;
        StreamReader? reader;
        string nonce;
        HashSet<string> allowed;
        lock (_lock)
        {
            if (_pipe?.IsConnected != true || _writer is null || _reader is null || _nonce is null || _allowedMethods is null)
                return Result<ProbeResponse>.Fail(ErrorCodes.ProbeNotConnected, "Not connected to probe.");
            if (!_allowedMethods.Contains(method))
                return Result<ProbeResponse>.Fail(ErrorCodes.ProbeMethodNotAllowed,
                    $"Method '{method}' is not in the probe's allow-list. Allowed: {string.Join(", ", _allowedMethods)}");
            writer = _writer;
            reader = _reader;
            nonce = _nonce;
            allowed = _allowedMethods;
        }

        try
        {
            var request = new ProbeRequest
            {
                Method = method,
                Nonce = nonce,
                ProtocolVersion = Versions.ProtocolVersion,
                ServerVersion = Versions.ServerVersion,
                Parameters = parameters?.ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value)) ?? new()
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonDefaults.Options));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            var responseLine = await reader.ReadLineAsync(cts.Token);
            if (responseLine is null)
                return Result<ProbeResponse>.Fail(ErrorCodes.Internal, "No response from probe.");
            var resp = JsonSerializer.Deserialize<ProbeResponse>(responseLine, JsonDefaults.Options);
            return resp is null
                ? Result<ProbeResponse>.Fail(ErrorCodes.Internal, "Probe returned an unparseable response")
                : Result<ProbeResponse>.Ok(resp);
        }
        catch (OperationCanceledException)
        {
            return Result<ProbeResponse>.Fail(ErrorCodes.Timeout, "Probe request timed out.");
        }
        catch (Exception ex)
        {
            return Result<ProbeResponse>.Fail(ErrorCodes.Internal, ex.Message);
        }
    }

    private ProbeContract? BuildContract()
    {
        if (_pipeName is null || _nonce is null) return null;
        return new ProbeContract
        {
            ProbeVersion = Versions.ProbeVersion,
            ProtocolVersion = Versions.ProtocolVersion,
            Nonce = _nonce,
            AllowedMethods = _allowedMethods?.ToList() ?? new List<string>(),
            ServerVersion = Versions.ServerVersion
        };
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            var pipe = _pipe;
            var reader = _reader;
            var writer = _writer;
            _reader = null;
            _writer = null;
            _pipe = null;
            _pipeName = null;
            _nonce = null;
            _allowedMethods = null;
            DisposeConnectResources(pipe, reader, writer);
        }
    }

    private static void DisposeConnectResources(NamedPipeClientStream? pipe, StreamReader? reader, StreamWriter? writer)
    {
        try { writer?.Dispose(); } catch { /* best-effort cleanup */ }
        try { reader?.Dispose(); } catch { /* best-effort cleanup */ }
        if (reader is null && writer is null)
        {
            try { pipe?.Dispose(); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Convenience wrapper: returns the unwrapped ProbeResponse or null on transport failure.
    /// Errors coming back from the probe itself (Ok=false) are returned as the response.
    /// Use the Result-returning TrySendAsync for explicit success/failure checks.
    /// </summary>
    public async Task<ProbeResponse?> SendAsync(string method, IReadOnlyDictionary<string, object?>? parameters = null, int timeoutMs = 10000)
    {
        var result = await TrySendAsync(method, parameters, timeoutMs, CancellationToken.None);
        if (!result.IsSuccess)
            return ProbeResponse.Failure(method, result.Error?.Code ?? "internal", result.Error?.Message ?? "Unknown error");
        return result.Value;
    }

    public void Dispose() => Disconnect();
}
