using System;
using System.IO;
using System.Net.Sockets;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class AuditHelper
    {
        public static string ResolveServiceDriverPath(RegistryKey baseKey, string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return "Unknown";
            try
            {
                using var srvKey = baseKey.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                if (srvKey != null)
                {
                    string rawPath = srvKey.GetValue("ImagePath")?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(rawPath))
                    {
                        string clean = rawPath;
                        if (clean.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
                        {
                            clean = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), clean.Substring(12));
                        }
                        else if (clean.StartsWith(@"system32\", StringComparison.OrdinalIgnoreCase))
                        {
                            clean = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), clean);
                        }
                        else if (clean.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
                        {
                            clean = clean.Substring(4);
                        }

                        if (File.Exists(clean)) return clean;
                        return clean;
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        public static bool TestInternetReachability()
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect("1.1.1.1", 53, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(900));
                if (success && client.Connected)
                {
                    client.EndConnect(result);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static bool CheckTpmPresence()
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var tpmKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM\WMI");
                return tpmKey != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
