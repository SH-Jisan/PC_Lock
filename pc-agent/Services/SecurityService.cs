using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PC.SecurityAgent.Controllers;
using PC.SecurityAgent.Hardware;
using PC.SecurityAgent.Network;

namespace PC.SecurityAgent.Services
{
    public class SecurityService : BackgroundService
    {
        private readonly ILogger<SecurityService> _logger;
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public SecurityService(ILogger<SecurityService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=================================================");
            _logger.LogInformation("🛡️ Windows PC Security Agent Service Active");
            _logger.LogInformation("=================================================");

            // 1. Initialize TPM 2.0 Hardware Key & Hardware UUID
            string pubKey = TpmKeyManager.GetOrCreateDevicePublicKey();
            string hardwareGuid = TpmKeyManager.GetHardwareUuid();
            
            string envPcId = Environment.GetEnvironmentVariable("PC_SECURITY_DEVICE_ID") ?? string.Empty;
            string pcDeviceId = !string.IsNullOrWhiteSpace(envPcId) ? envPcId : $"pc_{hardwareGuid[..8]}";

            LockController.ActiveDeviceId = pcDeviceId;

            _logger.LogInformation($"[Agent Identity] PC Device ID: {pcDeviceId}");
            _logger.LogInformation($"[Agent Identity] TPM Public Key: {pubKey[..24]}...");

            // 2. Hardware / Firmware Dual-Mode Telemetry Detection
            var (secLevel, secDescription) = FirmwareSecurityDetector.DetectSecurityLevel();
            _logger.LogInformation($"[Firmware Architecture] Active Security Tier: {secDescription}");

            // 3. Check startup lock state:
            LockController.LockState initialLockState = LockController.ReadPersistedLockState();
            _logger.LogInformation($"[Boot State] Current Persisted Lock State: {initialLockState}");

            if (initialLockState == LockController.LockState.UNLOCKED)
            {
                _logger.LogInformation("🟢 PC operating normally (UNLOCKED state). Ready for remote commands.");
            }
            else
            {
                _logger.LogWarning("🔴 PC restored to LOCKED state due to active remote security policy.");
                LockController.LockPC();
            }

            // 4. Hook Windows Session Switch Events (Defense against local password bypass while remotely locked)
            SystemEvents.SessionSwitch += (sender, e) =>
            {
                if (e.Reason == SessionSwitchReason.SessionUnlock)
                {
                    if (LockController.CurrentState == LockController.LockState.LOCKED)
                    {
                        _logger.LogWarning("[Security Enforcement] Unauthorized local Windows user password unlock attempt intercepted. Relocking immediately...");
                        LockController.LockPC();
                    }
                }
            };

            // 5. Mode 1 Tri-Vector Auto-Healer & Pre-Shutdown Protection
            _logger.LogInformation("🛡️ Initializing BootGuard Self-Healer & Pre-Shutdown Protection...");
            
            Task.Run(() => BootGuardHealer.HealBootConfiguration());

            using var healTimer = new System.Threading.Timer(_ =>
            {
                BootGuardHealer.HealBootConfiguration();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                _logger.LogInformation("[Pre-Shutdown Hook] System shutting down/rebooting. Running final boot cloaking pass...");
                BootGuardHealer.HealBootConfiguration();
            };

            // 6. Start Background REST State Sync Poll Loop (Every 3 seconds for instant lock fail-safe)
            string relayUrl = Environment.GetEnvironmentVariable("PC_SECURITY_RELAY_URL") ?? "wss://pc-lock.onrender.com";
            string httpApiBase = relayUrl.Replace("wss://", "https://").Replace("ws://", "http://").TrimEnd('/');
            LockController.RelayHttpBaseUrl = httpApiBase;

            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(3000, stoppingToken);
                        string statusUrl = $"{httpApiBase}/api/devices/status";
                        string json = await HttpClient.GetStringAsync(statusUrl, stoppingToken);
                        
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("pcs", out var pcsEl) && pcsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var pc in pcsEl.EnumerateArray())
                            {
                                if (pc.TryGetProperty("id", out var idProp) && idProp.GetString() == pcDeviceId)
                                {
                                    if (pc.TryGetProperty("lock_status", out var lockProp))
                                    {
                                        string remoteStatus = lockProp.GetString() ?? "UNLOCKED";
                                        if (remoteStatus == "LOCKED" && LockController.CurrentState != LockController.LockState.LOCKED)
                                        {
                                            _logger.LogWarning("[Cloud Sync] Remote Lock Policy 'LOCKED' detected. Executing PC Lock...");
                                            LockController.LockPC();
                                        }
                                        else if (remoteStatus == "UNLOCKED" && LockController.CurrentState == LockController.LockState.LOCKED)
                                        {
                                            _logger.LogInformation("[Cloud Sync] Remote Lock Policy 'UNLOCKED' detected. Clearing PC Lock...");
                                            LockController.UnlockPC();
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }, stoppingToken);

            // 7. Connect Outbound Real-Time WSS Client to Relay Server
            WssClient client = new WssClient(relayUrl, pcDeviceId);
            await client.StartAsync(stoppingToken);
        }
    }
}
