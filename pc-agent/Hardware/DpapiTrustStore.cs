using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace PC.SecurityAgent.Hardware
{
    /// <summary>
    /// Enterprise Windows Hardware-Pinned Trust Store backed by Windows DPAPI
    /// and Machine-Scoped Protected Cryptography.
    /// </summary>
    public static class DpapiTrustStore
    {
        private const string RegistryPath = @"SOFTWARE\PCSecuritySystem\TrustStore";
        private const string PinnedKeyName = "PinnedMobilePublicKey";
        private const string PinnedDeviceIdName = "PinnedMobileDeviceId";
        private const string PairingTimestampName = "PinnedAt";

        // Cryptographic entropy to bind DPAPI encryption specifically to PC Lock Security Agent
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PCLock_Hardware_TrustStore_Entropy_2026");

        /// <summary>
        /// Encrypts and securely pins the trusted mobile public key using Windows DPAPI (LocalMachine Scope).
        /// </summary>
        public static void PinTrustedMobileDevice(string deviceId, string publicKeyHex)
        {
            try
            {
                byte[] rawKeyBytes = Encoding.UTF8.GetBytes(publicKeyHex);
                byte[] encryptedBytes = ProtectedData.Protect(rawKeyBytes, Entropy, DataProtectionScope.LocalMachine);
                string encryptedBase64 = Convert.ToBase64String(encryptedBytes);

                using RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistryPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                key.SetValue(PinnedKeyName, encryptedBase64, RegistryValueKind.String);
                key.SetValue(PinnedDeviceIdName, deviceId, RegistryValueKind.String);
                key.SetValue(PairingTimestampName, DateTime.UtcNow.ToString("o"), RegistryValueKind.String);

                Console.WriteLine($"[Hardware TrustStore] Successfully pinned trusted mobile key for device: {deviceId} using Windows DPAPI.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hardware TrustStore Error] Failed to pin mobile key with DPAPI: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads and decrypts the pinned mobile public key using Windows DPAPI.
        /// </summary>
        public static string? GetPinnedMobilePublicKey(string deviceId)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistryPath, false);
                if (key == null) return null;

                string? storedDeviceId = key.GetValue(PinnedDeviceIdName)?.ToString();
                if (!string.Equals(storedDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string? encryptedBase64 = key.GetValue(PinnedKeyName)?.ToString();
                if (string.IsNullOrWhiteSpace(encryptedBase64)) return null;

                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hardware TrustStore Warning] DPAPI unprotect failed ({ex.Message}).");
                return null;
            }
        }

        /// <summary>
        /// Checks if an authorized controller device has already been pinned to this PC.
        /// </summary>
        public static bool HasPinnedDevice()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistryPath, false);
                return key?.GetValue(PinnedKeyName) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears pinned mobile device credentials (requires elevated administrative privileges).
        /// </summary>
        public static void ClearPinnedDevice()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(RegistryPath, false);
                Console.WriteLine("[Hardware TrustStore] Pinned mobile credentials cleared.");
            }
            catch { }
        }
    }
}
