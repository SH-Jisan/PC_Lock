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
            _logger.LogInformation("🛡️ Windows PC Security Agent Service Started");
            _logger.LogInformation("=================================================");

            // 1. Initialize TPM 2.0 Hardware Key & Hardware UUID
            string pubKey = TpmKeyManager.GetOrCreateDevicePublicKey();
            string hardwareGuid = TpmKeyManager.GetHardwareUuid();
            string pcDeviceId = $"pc_{hardwareGuid[..8]}";

            _logger.LogInformation($"[Agent Identity] PC Device ID: {pcDeviceId}");
            _logger.LogInformation($"[Agent Identity] TPM Public Key: {pubKey[..24]}...");

            // 2. Check startup lock state:
            // "The PC must NOT automatically lock simply because the security software starts or Windows boots."
            LockController.LockState initialLockState = LockController.ReadPersistedLockState();
            _logger.LogInformation($"[Boot State] Current Persisted Lock State: {initialLockState}");

            if (initialLockState == LockController.LockState.UNLOCKED)
            {
                _logger.LogInformation("🟢 PC operates normally (UNLOCKED state). Listening for explicit mobile commands.");
            }
            else
            {
                _logger.LogWarning("🔴 PC restored to LOCKED state due to prior active remote lock command.");
            }

            // 3. Connect outbound WSS Client to Relay Server
            string relayUrl = "ws://localhost:4000";
            WssClient client = new WssClient(relayUrl, pcDeviceId);

            await client.StartAsync(stoppingToken);
        }
    }
}
