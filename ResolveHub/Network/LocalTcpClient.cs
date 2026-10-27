// =============================================================================
// LocalTcpClient.cs — Asynchronous Loopback TCP Client
// Sends JSON requests to LocalTcpServer and awaits responses.
// =============================================================================

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ResolveHub.Config;

namespace ResolveHub.Network
{
    /// <summary>
    /// Thin async TCP client for communicating with <see cref="LocalTcpServer"/>.
    ///
    /// Usage:
    ///   var response = await LocalTcpClient.SendAsync("PING");
    ///
    /// The client opens a fresh connection per request (stateless) which is
    /// appropriate for low-frequency local IPC. A persistent connection would be
    /// needed only if request frequency exceeded ~100/sec.
    /// </summary>
    public static class LocalTcpClient
    {
        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Sends an action string (and optional payload object) to the local TCP server
        /// and returns the parsed <see cref="TcpResponse"/>.
        /// </summary>
        /// <param name="action">The server-side action name (e.g. "PING").</param>
        /// <param name="payload">Optional object to serialise as the payload.</param>
        /// <returns>The server's response.</returns>
        public static async Task<TcpResponse> SendAsync(string action, object? payload = null)
        {
            string requestJson = BuildRequest(action, payload);

            try
            {
                using var client = new TcpClient();
                client.SendTimeout    = AppSettings.TcpTimeout;
                client.ReceiveTimeout = AppSettings.TcpTimeout;

                await client.ConnectAsync(AppSettings.TcpHost, AppSettings.TcpPort);

                await using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
                    { AutoFlush = true };

                // Send request (newline-delimited)
                await writer.WriteLineAsync(requestJson);

                // Read response (server sends exactly one line)
                string? responseJson = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(responseJson))
                    return new TcpResponse(false, "Empty response from server.");

                return JsonSerializer.Deserialize<TcpResponse>(responseJson)
                    ?? new TcpResponse(false, "Failed to parse server response.");
            }
            catch (SocketException ex)
            {
                // Server may not be running — return a graceful error instead of crashing
                return new TcpResponse(false, $"Connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return new TcpResponse(false, $"Unexpected error: {ex.Message}");
            }
        }

        // -----------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------
        private static string BuildRequest(string action, object? payload)
        {
            // Serialise payload object to JsonElement for the envelope
            JsonElement? payloadElement = null;
            if (payload is not null)
            {
                string payloadJson = JsonSerializer.Serialize(payload);
                payloadElement     = JsonDocument.Parse(payloadJson).RootElement;
            }

            var request = new TcpRequest(action, payloadElement);
            return JsonSerializer.Serialize(request);
        }
    }
}
