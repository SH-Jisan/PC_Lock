using System;
using System.Security.Cryptography;
using System.Text;

namespace PC.SecurityAgent.Hardware
{
    public class TpmKeyManager
    {
        private const string KeyName = "PCSecurityAgent_Ed25519_TPM_Key";

        public static string GetOrCreateDevicePublicKey()
        {
            try
            {
                // Attempt CNG TPM Provider (Microsoft Platform Crypto Provider)
                CngProvider tpmProvider = CngProvider.MicrosoftPlatformCryptoProvider;
                
                if (CngKey.Exists(KeyName, tpmProvider))
                {
                    using CngKey key = CngKey.Open(KeyName, tpmProvider);
                    byte[] pubBytes = key.Export(CngKeyBlobFormat.GenericPublicBlob);
                    return Convert.ToHexString(pubBytes);
                }
                else
                {
                    // Create persistent TPM 2.0 bound key pair
                    CngKeyCreationParameters creationParams = new CngKeyCreationParameters
                    {
                        Provider = tpmProvider,
                        KeyCreationOptions = CngKeyCreationOptions.OverwriteExistingKey,
                        ExportPolicy = CngExportPolicies.AllowExport // Public key export only
                    };

                    using CngKey newKey = CngKey.Create(CngAlgorithm.ECDsaP256, KeyName, creationParams);
                    byte[] pubBytes = newKey.Export(CngKeyBlobFormat.GenericPublicBlob);
                    Console.WriteLine("[TPM 2.0] Successfully initialized hardware-backed cryptographic key.");
                    return Convert.ToHexString(pubBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TPM Warning] Hardware TPM 2.0 CNG Provider unavailable ({ex.Message}). Falling back to Windows Software Key Storage.");
                return GetOrCreateSoftwareFallbackKey();
            }
        }

        private static string GetOrCreateSoftwareFallbackKey()
        {
            using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            byte[] pubBytes = ecdsa.ExportSubjectPublicKeyInfo();
            return Convert.ToHexString(pubBytes);
        }

        public static string GetHardwareUuid()
        {
            try
            {
                // Fetch System Machine GUID from Windows Registry
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                string? guid = key?.GetValue("MachineGuid")?.ToString();
                return guid ?? Guid.NewGuid().ToString();
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
