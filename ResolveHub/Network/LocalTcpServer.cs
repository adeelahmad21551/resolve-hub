// =============================================================================
// LocalTcpServer.cs — Asynchronous Loopback TCP Server
// Bound to 127.0.0.1:5000 — NOT accessible from outside the machine.
// =============================================================================

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ResolveHub.Config;

namespace ResolveHub.Network
{
    // -------------------------------------------------------------------------
    // Simple DTO for inter-process messages (JSON-serialisable)
    // -------------------------------------------------------------------------

    /// <summary>Envelope sent from client to server.</summary>
    public record TcpRequest(string Action, JsonElement? Payload);

    /// <summary>Envelope returned from server to client.</summary>
    public record TcpResponse(bool Success, string Message, JsonElement? Data = null);

    // -------------------------------------------------------------------------
    // Server
    // -------------------------------------------------------------------------

    /// <summary>
    /// A lightweight loopback TCP server that listens on 127.0.0.1:5000.
    /// Each accepted connection is handled on its own Task (thread-pool thread).
    ///
    /// Decision: Raw TCP sockets were used (rather than SignalR or gRPC) because
    /// this is a local single-machine application and the spec explicitly requires
    /// a TCP socket implementation for the DSA/networking milestone.
    ///
    /// Security: The server is bound ONLY to the loopback address (127.0.0.1),
    /// making it unreachable from the network. No authentication is required for
    /// loopback because an attacker with local access already has higher
    /// privilege than the socket can provide.
    /// </summary>
    public sealed class LocalTcpServer : IDisposable
    {
        private TcpListener?          _listener;
        private CancellationTokenSource _cts = new();
        private bool                  _disposed;

        // Raised on the thread-pool when a request arrives
        public event Action<string>? OnRequestReceived;
        public event Action<string>? OnLogMessage;

        // -----------------------------------------------------------------
        // Start / Stop
        // -----------------------------------------------------------------

        /// <summary>Starts listening asynchronously.</summary>
        public void Start()
        {
            if (_listener is not null) return;

            var endpoint = new IPEndPoint(IPAddress.Loopback, AppSettings.TcpPort);
            _listener    = new TcpListener(endpoint);
            _listener.Start();

            Log($"TCP server started on {AppSettings.TcpHost}:{AppSettings.TcpPort}");

            // Fire-and-forget accept loop on the thread pool
            Task.Run(AcceptLoopAsync, _cts.Token);
        }

        /// <summary>Stops the server gracefully.</summary>
        public void Stop()
        {
            _cts.Cancel();
            _listener?.Stop();
            _listener = null;
            Log("TCP server stopped.");
        }

        // -----------------------------------------------------------------
        // Accept loop — runs until cancellation is requested
        // -----------------------------------------------------------------
        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener!.AcceptTcpClientAsync(_cts.Token);
                    // Each client is handled concurrently on the thread pool
                    _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;   // Clean shutdown
                }
                catch (Exception ex)
                {
                    Log($"Accept error: {ex.Message}");
                }
            }
        }

        // -----------------------------------------------------------------
        // Client handler
        // -----------------------------------------------------------------
        private async Task HandleClientAsync(TcpClient client)
        {
            string remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            Log($"Client connected: {remoteEp}");

            try
            {
                await using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
                    { AutoFlush = true };

                string? requestJson = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestJson)) return;

                OnRequestReceived?.Invoke(requestJson);
                Log($"Received: {requestJson}");

                // Deserialise and dispatch
                TcpRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<TcpRequest>(requestJson);
                }
                catch
                {
                    var errorResp = new TcpResponse(false, "Invalid JSON payload.");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResp));
                    return;
                }

                TcpResponse response = DispatchAction(request!);
                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                Log($"Handler error: {ex.Message}");
            }
            finally
            {
                client.Dispose();
                Log($"Client disconnected: {remoteEp}");
            }
        }

        // -----------------------------------------------------------------
        // Action dispatcher — extend with additional actions here
        // -----------------------------------------------------------------
        private static TcpResponse DispatchAction(TcpRequest request) =>
            request.Action?.ToUpperInvariant() switch
            {
                "PING"   => new TcpResponse(true,  "PONG"),
                "STATUS" => new TcpResponse(true,  "Server is running."),
                _        => new TcpResponse(false, $"Unknown action: {request.Action}")
            };

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private void Log(string message)
        {
            string stamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLogMessage?.Invoke(stamped);
            System.Diagnostics.Debug.WriteLine(stamped);
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------
        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
