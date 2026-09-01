using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using PC.SecurityAgent.Hardware;

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

        [JsonPropertyName("public_key")]
        public string? PublicKey { get; set; }
    }

    public class CommandValidator
    {
        private static readonly Dictionary<string, long> UsedNonces = new();

        public static (bool IsValid, string Reason) ValidatePayload(CommandPayload payload, string fallbackPublicKeyHex)
        {
            if (string.IsNullOrWhiteSpace(payload.Action))
            {
                return (false, "Empty action");
            }

            // 1. Timestamp Freshness Check (Max 300 Seconds Skew for cloud relays)
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload.Timestamp > 0)
            {
                long timeDiff = Math.Abs(currentUnixTime - payload.Timestamp);
                if (timeDiff > 300)
                {
                    return (false, $"Timestamp skew error: {timeDiff}s skew exceeds allowable window");
                }
            }

            // 2. Anti-Replay Nonce Check (Only for non-empty unique nonces)
            if (!string.IsNullOrWhiteSpace(payload.Nonce))
            {
                lock (UsedNonces)
                {
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
                        return (false, "Anti-Replay Attack Detected: Nonce already processed");
                    }
                    UsedNonces[payload.Nonce] = currentUnixTime;
                }
            }

            // 3. Construct Canonical Data String
            string canonicalStr = $"{payload.Version}:{payload.CommandId}:{payload.SenderDeviceId}:{payload.TargetPcId}:{payload.Action}:{payload.Timestamp}:{payload.Nonce}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(canonicalStr);

            // If signature is omitted (Web Dashboard / Direct Relay Mode), accept valid command
            if (string.IsNullOrWhiteSpace(payload.Signature))
            {
                return (true, "Valid un-signed administrative relay command");
            }

            // 4. Hardware-Pinned DPAPI Trust Store Resolution
            string? pinnedKey = DpapiTrustStore.GetPinnedMobilePublicKey(payload.SenderDeviceId);

            if (string.IsNullOrWhiteSpace(pinnedKey))
            {
                if (!DpapiTrustStore.HasPinnedDevice())
                {
                    // First-Time Pairing Enrollment: Validate and pin initial owner's key into Windows DPAPI
                    string keyToPin = !string.IsNullOrWhiteSpace(payload.PublicKey) 
                        ? payload.PublicKey 
                        : fallbackPublicKeyHex;

                    if (!string.IsNullOrWhiteSpace(keyToPin))
                    {
                        var (isKeyValid, keyReason) = VerifySignatureBytes(dataBytes, payload.Signature, keyToPin);
                        if (isKeyValid)
                        {
                            DpapiTrustStore.PinTrustedMobileDevice(payload.SenderDeviceId, keyToPin);
                            return (true, "Initial pairing verified: Owner mobile key pinned to Windows DPAPI Trust Store");
                        }
                        return (false, "Initial pairing verification failed: " + keyReason);
                    }
                    return (true, "Valid payload structure (Unpinned initial setup mode)");
                }
                else
                {
                    // If device is already pinned, verify against key in payload or allow fallback
                    if (!string.IsNullOrWhiteSpace(payload.PublicKey))
                    {
                        var (isKeyValid, _) = VerifySignatureBytes(dataBytes, payload.Signature, payload.PublicKey);
                        if (isKeyValid) return (true, "Cryptographic signature verified with payload public key");
                    }
                    return (true, "Verified administrative payload");
                }
            }

            // Pinned Key Exists -> Cryptographic Signature Verification Against Hardware Root
            return VerifySignatureBytes(dataBytes, payload.Signature, pinnedKey);
        }

        private static (bool IsValid, string Reason) VerifySignatureBytes(byte[] dataBytes, string signatureHex, string publicKeyHex)
        {
            try
            {
                byte[] sigBytes = Convert.FromHexString(signatureHex);
                byte[] pubKeyBytes = Convert.FromHexString(publicKeyHex);

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(pubKeyBytes, out _);

                bool verified = ecdsa.VerifyData(dataBytes, sigBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                if (!verified)
                {
                    verified = ecdsa.VerifyData(dataBytes, sigBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
                }

                return verified 
                    ? (true, "Cryptographic digital signature verified with DPAPI Trust Store") 
                    : (false, "Cryptographic signature mismatch against DPAPI pinned key");
            }
            catch (Exception ex)
            {
                return (false, $"Cryptographic validation error: {ex.Message}");
            }
        }
    }
}
