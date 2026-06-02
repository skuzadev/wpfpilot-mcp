using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace WpfPilot.Mcp.Probe;

/// <summary>
/// In-process probe that runs inside the target WPF application.
/// Communicates with the MCP server via named pipe IPC.
/// Install by calling ProbeHost.Start() in the WPF app's startup.
/// </summary>
public sealed class ProbeHost : IDisposable
{
    private readonly string _pipeName;
    private readonly List<string> _allowedMethods;
    private readonly string _nonce;
    private readonly string _version = Versions.ProbeVersion;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private static ProbeHost? _instance;

    public static ProbeHost? Instance => _instance;
    public string PipeName => _pipeName;
    public string Nonce => _nonce;
    public bool IsRunning => _listenTask is not null && !_listenTask.IsCompleted;
    public IReadOnlyList<string> AllowedMethods => _allowedMethods;

    private ProbeHost(string pipeName, IReadOnlyList<string> allowedMethods, string nonce)
    {
        _pipeName = pipeName;
        _allowedMethods = allowedMethods.ToList();
        _nonce = nonce;
    }

    /// <summary>
    /// Start the probe in the current WPF application process.
    /// Call this from App.xaml.cs OnStartup or a similar entry point.
    /// </summary>
    public static ProbeHost Start(string? pipeName = null, IReadOnlyList<string>? allowedMethods = null)
    {
        pipeName ??= $"wpfpilot-mcp-probe-{Environment.ProcessId}";
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var methods = allowedMethods ?? DefaultAllowedMethods;
        var host = new ProbeHost(pipeName, methods, nonce);
        host.StartListening();
        _instance = host;
        return host;
    }

    private void StartListening()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    2,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(pipe, utf8NoBom);
                using var writer = new StreamWriter(pipe, utf8NoBom) { AutoFlush = true };

                // Send the contract on connect (handshake hello)
                var hello = new
                {
                    type = "hello",
                    probeVersion = _version,
                    protocolVersion = Versions.ProtocolVersion,
                    nonce = _nonce,
                    allowedMethods = _allowedMethods
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(hello));

                while (pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break;

                    var response = await ProcessRequest(line);
                    await writer.WriteLineAsync(response);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try { await Task.Delay(100, ct); } catch { break; }
            }
        }
    }

    private async Task<string> ProcessRequest(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ProbeRequest>(requestJson);
            if (request is null)
                return JsonSerializer.Serialize(ProbeResponse.Failure("?", ErrorCodes.InvalidArgs, "Invalid request"));

            // Handshake validation
            if (request.Nonce != _nonce)
                return JsonSerializer.Serialize(ProbeResponse.Failure(request.Method, ErrorCodes.HandshakeFailed, "Nonce mismatch"));

            if (request.ProtocolVersion != Versions.ProtocolVersion)
                return JsonSerializer.Serialize(ProbeResponse.Failure(request.Method, ErrorCodes.HandshakeFailed,
                    $"Protocol version mismatch: client={request.ProtocolVersion}, probe={Versions.ProtocolVersion}"));

            // Method allow-list
            if (!_allowedMethods.Contains(request.Method))
                return JsonSerializer.Serialize(ProbeResponse.Failure(request.Method, ErrorCodes.ProbeMethodNotAllowed,
                    $"Method '{request.Method}' is not in the probe's allow-list"));

            return request.Method switch
            {
                "ping" => JsonSerializer.Serialize(ProbeResponse.Success("ping", "pong", null)),
                "get_datacontext" => await RunOnDispatcher(() => GetDataContext(request)),
                "get_viewmodel_properties" => await RunOnDispatcher(() => GetViewModelProperties(request)),
                "get_binding_errors" => await RunOnDispatcher(() => GetBindingErrors()),
                "get_bindings" => await RunOnDispatcher(() => GetBindings(request)),
                "get_command_state" => await RunOnDispatcher(() => GetCommandState(request)),
                "get_validation_state" => await RunOnDispatcher(() => GetValidationState(request)),
                "execute_command" => ValidateExecuteCommandRequest(request)
                    ?? await RunOnDispatcher(() => ExecuteCommand(request)),
                "get_dispatcher_status" => await RunOnDispatcher(() => GetDispatcherStatus()),
                _ => JsonSerializer.Serialize(ProbeResponse.Failure(request.Method, ErrorCodes.InvalidArgs, $"Unknown method: {request.Method}"))
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(ProbeResponse.Failure("?", ErrorCodes.Internal, ex.Message));
        }
    }

    private static async Task<string> RunOnDispatcher(Func<string> action)
    {
        if (Application.Current?.Dispatcher is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("?", ErrorCodes.Internal, "No WPF dispatcher"));

        var tcs = new TaskCompletionSource<string>();
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetResult(JsonSerializer.Serialize(ProbeResponse.Failure("?", ErrorCodes.Internal, ex.Message)));
            }
        });
        return await tcs.Task;
    }

    private static string GetDataContext(ProbeRequest request)
    {
        var window = GetTargetWindow(request);
        if (window is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_datacontext", ErrorCodes.ElementNotFound, "Window not found"));

        var dc = window.DataContext;
        if (dc is null)
            return JsonSerializer.Serialize(ProbeResponse.Success("get_datacontext", "null", null));

        var type = dc.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new { name = p.Name, type = p.PropertyType.Name, value = SafeGetValue(p, dc) })
            .ToList();

        var payload = new { typeName = type.FullName, properties = props };
        return JsonSerializer.Serialize(ProbeResponse.Success("get_datacontext", null,
            JsonSerializer.SerializeToElement(payload)));
    }

    private static string GetViewModelProperties(ProbeRequest request)
    {
        var window = GetTargetWindow(request);
        var dc = window?.DataContext;
        if (dc is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_viewmodel_properties", ErrorCodes.ElementNotFound, "No DataContext"));

        var type = dc.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new
            {
                name = p.Name,
                type = p.PropertyType.Name,
                canRead = p.CanRead,
                canWrite = p.CanWrite,
                value = SafeGetValue(p, dc)
            })
            .ToList();

        return JsonSerializer.Serialize(ProbeResponse.Success("get_viewmodel_properties", null,
            JsonSerializer.SerializeToElement(props)));
    }

    private static string GetBindingErrors()
    {
        var errors = new List<object>();
        if (Application.Current is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_binding_errors", ErrorCodes.Internal, "No WPF app"));

        foreach (var window in Application.Current.Windows.OfType<Window>())
        {
            var windowErrors = System.Windows.Controls.Validation.GetErrors(window);
            foreach (var error in windowErrors)
            {
                errors.Add(new
                {
                    window = window.Title,
                    message = error.ErrorContent?.ToString(),
                    bindingPath = (error.BindingInError as System.Windows.Data.BindingExpression)?.ParentBinding.Path.Path
                });
            }
        }

        return JsonSerializer.Serialize(ProbeResponse.Success("get_binding_errors", null,
            JsonSerializer.SerializeToElement(new { errorCount = errors.Count, errors })));
    }

    private static string GetBindings(ProbeRequest request)
    {
        var window = GetTargetWindow(request);
        if (window is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_bindings", ErrorCodes.ElementNotFound, "Window not found"));

        var bindings = new List<object>();
        CollectBindings(window, bindings);

        return JsonSerializer.Serialize(ProbeResponse.Success("get_bindings", null,
            JsonSerializer.SerializeToElement(new { count = bindings.Count, bindings = bindings.Take(50) })));
    }

    private static string GetCommandState(ProbeRequest request)
    {
        var window = GetTargetWindow(request);
        var dc = window?.DataContext;
        if (dc is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_command_state", ErrorCodes.ElementNotFound, "No DataContext"));

        var type = dc.GetType();
        var commands = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => typeof(System.Windows.Input.ICommand).IsAssignableFrom(p.PropertyType))
            .Select(p =>
            {
                var cmd = p.GetValue(dc) as System.Windows.Input.ICommand;
                return new { name = p.Name, canExecute = cmd?.CanExecute(null) ?? false };
            })
            .ToList();

        return JsonSerializer.Serialize(ProbeResponse.Success("get_command_state", null,
            JsonSerializer.SerializeToElement(commands)));
    }

    private static string GetValidationState(ProbeRequest request)
    {
        var window = GetTargetWindow(request);
        if (window is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_validation_state", ErrorCodes.ElementNotFound, "Window not found"));

        var errors = System.Windows.Controls.Validation.GetErrors(window);
        var errorList = errors.Select(e => new
        {
            message = e.ErrorContent?.ToString(),
            bindingPath = (e.BindingInError as System.Windows.Data.BindingExpression)?.ParentBinding.Path.Path
        }).ToList();

        return JsonSerializer.Serialize(ProbeResponse.Success("get_validation_state", null,
            JsonSerializer.SerializeToElement(new { hasErrors = errorList.Count > 0, errors = errorList })));
    }

    private static string? ValidateExecuteCommandRequest(ProbeRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, JsonElement>();
        if (!parameters.TryGetValue("commandName", out var commandNameEl) ||
            string.IsNullOrWhiteSpace(commandNameEl.GetString()))
            return JsonSerializer.Serialize(ProbeResponse.Failure("execute_command", ErrorCodes.InvalidArgs, "commandName required"));
        return null;
    }

    private static string ExecuteCommand(ProbeRequest request)
    {
        var window = GetTargetWindow(request);
        var dc = window?.DataContext;
        if (dc is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("execute_command", ErrorCodes.ElementNotFound, "No DataContext"));

        var parameters = request.Parameters ?? new Dictionary<string, JsonElement>();
        var commandName = parameters["commandName"].GetString()!;
        var prop = dc.GetType().GetProperty(commandName);
        var cmd = prop?.GetValue(dc) as System.Windows.Input.ICommand;
        if (cmd is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("execute_command", ErrorCodes.ElementNotFound, $"Command '{commandName}' not found"));

        if (!cmd.CanExecute(null))
            return JsonSerializer.Serialize(ProbeResponse.Failure("execute_command", ErrorCodes.PatternNotSupported, $"Command '{commandName}' cannot execute"));

        var parameter = parameters.TryGetValue("parameter", out var p) && p.ValueKind != JsonValueKind.Null
            ? p.ToString()
            : null;
        cmd.Execute(parameter);
        return JsonSerializer.Serialize(ProbeResponse.Success("execute_command", "executed", null));
    }

    private static string GetDispatcherStatus()
    {
        if (Application.Current is null)
            return JsonSerializer.Serialize(ProbeResponse.Failure("get_dispatcher_status", ErrorCodes.Internal, "No WPF app"));

        var dispatcher = Application.Current.Dispatcher;
        return JsonSerializer.Serialize(ProbeResponse.Success("get_dispatcher_status", null,
            JsonSerializer.SerializeToElement(new
            {
                hasShutdownStarted = dispatcher.HasShutdownStarted,
                hasShutdownFinished = dispatcher.HasShutdownFinished,
                thread = dispatcher.Thread.Name ?? $"Thread-{dispatcher.Thread.ManagedThreadId}"
            })));
    }

    private static Window? GetTargetWindow(ProbeRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, JsonElement>();
        if (parameters.TryGetValue("windowTitle", out var titleEl))
        {
            var title = titleEl.GetString();
            if (!string.IsNullOrEmpty(title))
                return Application.Current!.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        }
        return Application.Current?.MainWindow;
    }

    private static void CollectBindings(DependencyObject obj, List<object> bindings, int depth = 0)
    {
        if (depth > 5 || bindings.Count >= 50) return;

        var localValueEnumerator = obj.GetLocalValueEnumerator();
        while (localValueEnumerator.MoveNext())
        {
            var entry = localValueEnumerator.Current;
            var binding = System.Windows.Data.BindingOperations.GetBinding(obj, entry.Property);
            if (binding is not null)
            {
                bindings.Add(new
                {
                    property = entry.Property.Name,
                    path = binding.Path?.Path,
                    mode = binding.Mode.ToString(),
                    elementType = obj.GetType().Name
                });
            }
        }

        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj);
        for (int i = 0; i < childCount && bindings.Count < 50; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
            CollectBindings(child, bindings, depth + 1);
        }
    }

    private static string? SafeGetValue(PropertyInfo prop, object obj)
    {
        try
        {
            var val = prop.GetValue(obj);
            if (val is null) return "null";
            if (val is string s) return s.Length > 100 ? s[..100] + "..." : s;
            if (val.GetType().IsPrimitive || val is decimal || val is DateTime) return val.ToString();
            if (val is System.Collections.ICollection col)
                return $"[Collection: {col.Count} items]";
            if (val is System.Collections.IEnumerable)
                return $"[{val.GetType().Name}]";
            return $"[{val.GetType().Name}]";
        }
        catch (Exception ex) { return $"[error: {ex.GetType().Name}]"; }
    }

    public void Stop()
    {
        var cts = _cts;
        if (cts is null) return;
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
        try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        cts.Dispose();
        _cts = null;
        _listenTask = null;
        if (ReferenceEquals(_instance, this)) _instance = null;
    }

    public void Dispose() => Stop();

    public static IReadOnlyList<string> DefaultAllowedMethods { get; } = new[]
    {
        "ping",
        "get_datacontext",
        "get_viewmodel_properties",
        "get_binding_errors",
        "get_bindings",
        "get_command_state",
        "get_validation_state",
        "get_dispatcher_status",
        "execute_command"
    };
}
