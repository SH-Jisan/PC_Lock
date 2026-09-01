using System;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using PC.SecurityAgent.Controllers;
using PC.SecurityAgent.Hardware;
using PC.SecurityAgent.Security;

namespace PC.SecurityAgent.Network
{
    public class WssClient
    {
        private readonly string _serverUrl;
        private readonly string _pcDeviceId;
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _reconnectTrigger;

        public WssClient(string serverUrl, string pcDeviceId)
        {
            string normalized = serverUrl.Trim().TrimEnd('/');
            if (normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "wss://" + normalized.Substring(8);
            }
            else if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "ws://" + normalized.Substring(7);
            }
            else if (!normalized.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) && !normalized.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "wss://" + normalized;
            }

            _serverUrl = normalized;
            _pcDeviceId = pcDeviceId;

            // 1. Hook Hardware Network State Changes (Instant Reconnect on DHCP/Wi-Fi ready)
            NetworkChange.NetworkAvailabilityChanged += (s, e) =>
            {
                if (e.IsAvailable)
                {
                    Console.WriteLine("[Network Sensor] Network adapter is now ONLINE. Triggering immediate gateway connection...");
                    _reconnectTrigger?.Cancel();
                }
                else
                {
                    Console.WriteLine("[Network Sensor] Network adapter temporarily disconnected.");
                }
            };

            NetworkChange.NetworkAddressChanged += (s, e) =>
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    Console.WriteLine("[Network Sensor] IP Address assigned/renewed. Verifying connection...");
                    _reconnectTrigger?.Cancel();
                }
            };

            // 2. Hook System Power Modes (Sleep / Hibernate Resume)
            SystemEvents.PowerModeChanged += (s, e) =>
            {
                if (e.Mode == PowerModes.Resume)
                {
                    Console.WriteLine("[Power Sensor] System resumed from sleep/standby. Re-establishing secure relay tunnel...");
                    _reconnectTrigger?.Cancel();
                }
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int retryDelay = 2000;

            while (!cancellationToken.IsCancellationRequested)
            {
                _reconnectTrigger = new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _reconnectTrigger.Token);

                try
                {
                    // If network is not available yet on boot, wait briefly
                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        Console.WriteLine("[WSS Client] Waiting for network interface initialization...");
                        await Task.Delay(3000, linkedCts.Token);
                    }

                    _ws = new ClientWebSocket();
                    _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

                    Uri connectUri = new Uri($"{_serverUrl}?device_id={_pcDeviceId}&device_type=PC");
                    Console.WriteLine($"[WSS Client] Connecting to Relay Gateway at {connectUri}...");

                    await _ws.ConnectAsync(connectUri, linkedCts.Token);
                    Console.WriteLine("[WSS Client] Connected to Relay Gateway! Live Telemetry Active.");

                    // Reset retry backoff on successful connection
                    retryDelay = 2000;

                    // Send Initial Registration & Status Telemetry Frame
                    await SendInitialRegistrationAsync();

                    using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                    var heartbeatTask = HeartbeatLoopAsync(heartbeatCts.Token);

                    await ReceiveLoopAsync(linkedCts.Token);

                    heartbeatCts.Cancel();
                    await Task.WhenAny(heartbeatTask, Task.Delay(100));
                }
                catch (OperationCanceledException) when (_reconnectTrigger.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("[WSS Client] Immediate reconnect signal triggered.");
                    retryDelay = 1000;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS Client Warning] Relay connection interrupted ({ex.Message}). Retrying in {retryDelay / 1000}s...");
                    try
                    {
                        await Task.Delay(retryDelay, linkedCts.Token);
                    }
                    catch { }

                    // Exponential backoff capped at 8 seconds
                    retryDelay = Math.Min(retryDelay * 2, 8000);
                }
                finally
                {
                    try
                    {
                        _ws?.Dispose();
                        _ws = null;
                    }
                    catch { }
                }
            }
        }

        private async Task SendInitialRegistrationAsync()
        {
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    string pubKey = TpmKeyManager.GetOrCreateDevicePublicKey();
                    var regMsg = new
                    {
                        event_type = "PC_STATUS_REPORT",
                        pc_id = _pcDeviceId,
                        device_name = $"{Environment.MachineName} (PC-01)",
                        lock_status = LockController.CurrentState.ToString(),
                        public_key = pubKey,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    string json = JsonSerializer.Serialize(regMsg);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch { }
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await Task.Delay(12000, ct); // Every 12 seconds
                    var heartbeatMsg = new
                    {
                        event_type = "HEARTBEAT",
                        pc_id = _pcDeviceId,
                        lock_status = LockController.CurrentState.ToString(),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    string json = JsonSerializer.Serialize(heartbeatMsg);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
                catch { break; }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            while (_ws != null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                string jsonStr = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    var payload = JsonSerializer.Deserialize<CommandPayload>(jsonStr);
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.Action))
                    {
                        ProcessCommand(payload);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS Client Parse Error]: {ex.Message}");
                }
            }
        }

        private void ProcessCommand(CommandPayload payload)
        {
            Console.WriteLine($"[WSS Client] Processing Remote Command: {payload.Action} (Command ID: {payload.CommandId})");

            var (isValid, reason) = CommandValidator.ValidatePayload(payload, "");
            if (!isValid)
            {
                Console.WriteLine($"[SECURITY REJECTION] {reason}");
                return;
            }

            Console.WriteLine($"[SECURITY ACCEPTED] {reason}. Executing {payload.Action}...");

            if (payload.Action == "LOCK_PC")
            {
                LockController.LockPC(false);
            }
            else if (payload.Action == "UNLOCK_PC")
            {
                LockController.UnlockPC(false);
            }
        }

        public async Task SendStatusUpdateAsync(string newStatus)
        {
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    var msg = new
                    {
                        event_type = "PC_STATUS_REPORT",
                        pc_id = _pcDeviceId,
                        lock_status = newStatus,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    string json = JsonSerializer.Serialize(msg);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch { }
            }
        }
    }
}
