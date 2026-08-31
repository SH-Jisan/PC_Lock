using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PC.SecurityAgent.Controllers;
using PC.SecurityAgent.Security;

namespace PC.SecurityAgent.Network
{
    public class WssClient
    {
        private readonly string _serverUrl;
        private readonly string _pcDeviceId;
        private ClientWebSocket? _ws;

        public WssClient(string serverUrl, string pcDeviceId)
        {
            _serverUrl = serverUrl;
            _pcDeviceId = pcDeviceId;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _ws = new ClientWebSocket();
                    Uri connectUri = new Uri($"{_serverUrl}?device_id={_pcDeviceId}&device_type=PC");
                    
                    Console.WriteLine($"[WSS Client] Connecting to Relay Gateway at {connectUri}...");
                    await _ws.ConnectAsync(connectUri, cancellationToken);
                    Console.WriteLine("[WSS Client] Connected to Relay Gateway! Listening for commands...");

                    await ReceiveLoopAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS Client Error] Connection lost ({ex.Message}). Retrying in 5 seconds...");
                    await Task.Delay(5000, cancellationToken);
                }
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
                Console.WriteLine($"[WSS Client] Received Payload: {jsonStr}");

                try
                {
                    var payload = JsonSerializer.Deserialize<CommandPayload>(jsonStr);
                    if (payload != null)
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
            Console.WriteLine($"[WSS Client] Processing Action: {payload.Action} (ID: {payload.CommandId})");

            var (isValid, reason) = CommandValidator.ValidatePayload(payload, "");
            if (!isValid)
            {
                Console.WriteLine($"[SECURITY REJECTION] {reason}");
                return;
            }

            if (payload.Action == "LOCK_PC")
            {
                LockController.LockPC();
                _ = SendStatusUpdateAsync("LOCKED");
            }
            else if (payload.Action == "UNLOCK_PC")
            {
                LockController.UnlockPC();
                _ = SendStatusUpdateAsync("UNLOCKED");
            }
        }

        private async Task SendStatusUpdateAsync(string newStatus)
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

