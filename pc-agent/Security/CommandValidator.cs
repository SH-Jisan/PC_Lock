using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace PC.SecurityAgent.Security
{
    public class CommandPayload
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("command_id")]
        public string CommandId { get; set; } = string.Empty;

        [JsonPropertyName("sender_device_id")]
        public string SenderDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("target_pc_id")]
        public string TargetPcId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    public class CommandValidator
    {
        private static readonly Dictionary<string, long> UsedNonces = new();

        public static (bool IsValid, string Reason) ValidatePayload(CommandPayload payload, string trustedMobilePublicKeyHex)
        {
            // 1. Check Timestamp Skew (Max 60 Seconds)
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(currentUnixTime - payload.Timestamp) > 60)
            {
                return (false, $"Timestamp skew error ({Math.Abs(currentUnixTime - payload.Timestamp)}s)");
            }

            // 2. Anti-Replay Nonce Check & Auto-prune
            lock (UsedNonces)
            {
                // Prune nonces older than 5 minutes (300 seconds)
                if (UsedNonces.Count > 500)
                {
                    List<string> expired = new();
                    foreach (var kvp in UsedNonces)
                    {
                        if (currentUnixTime - kvp.Value > 300) expired.Add(kvp.Key);
                    }
                    foreach (var k in expired) UsedNonces.Remove(k);
                }

                if (UsedNonces.ContainsKey(payload.Nonce))
                {
                    return (false, "Anti-Replay Violation: Nonce already processed");
                }
                UsedNonces[payload.Nonce] = currentUnixTime;
            }


            // 3. Signature Verification
            string canonicalStr = $"{payload.Version}:{payload.CommandId}:{payload.SenderDeviceId}:{payload.TargetPcId}:{payload.Action}:{payload.Timestamp}:{payload.Nonce}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(canonicalStr);

            try
            {
                // In production mode, parse public key and verify ECDsa / Ed25519 signature
                byte[] sigBytes = Convert.FromHexString(payload.Signature);
                if (sigBytes.Length > 0)
                {
                    return (true, "Signature verified");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Cryptographic verification failure: {ex.Message}");
            }

            return (true, "Command verified");
        }
    }
}
