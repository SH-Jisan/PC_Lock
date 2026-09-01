using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PC.SecurityAgent.Controllers;
using PC.SecurityAgent.Hardware;
using PC.SecurityAgent.Network;

namespace PC.SecurityAgent.Services
{
    public class SecurityService : BackgroundService
    {
        private readonly ILogger<SecurityService> _logger;

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
            }

            // 4. Mode 1 Tri-Vector Auto-Healer & Pre-Shutdown Protection
            _logger.LogInformation("🛡️ Initializing BootGuard Self-Healer & Pre-Shutdown Protection...");
            
            // Run initial heal on startup
            Task.Run(() => BootGuardHealer.HealBootConfiguration());

            // Continuous Background Auto-Healer Timer (Every 5 minutes)
            using var healTimer = new System.Threading.Timer(_ =>
            {
                BootGuardHealer.HealBootConfiguration();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Pre-Shutdown & System Reboot Protection Hook
            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                _logger.LogInformation("[Pre-Shutdown Hook] System shutting down/rebooting. Running final boot cloaking pass...");
                BootGuardHealer.HealBootConfiguration();
            };

            // 5. Connect Outbound WSS Client to Relay Server
            string relayUrl = Environment.GetEnvironmentVariable("PC_SECURITY_RELAY_URL") ?? "ws://localhost:4000";
            WssClient client = new WssClient(relayUrl, pcDeviceId);

            await client.StartAsync(stoppingToken);
        }
    }
}
