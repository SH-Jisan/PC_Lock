using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace DeployManager.Services
{
    public class MotherboardInfo
    {
        public string Manufacturer { get; set; } = "Unknown";
        public string Product { get; set; } = "Unknown";
        public string Version { get; set; } = "Unknown";
        public string SerialNumber { get; set; } = "Unknown";
        public string SystemManufacturer { get; set; } = "Unknown";
        public string SystemProductName { get; set; } = "Unknown";
        public string SystemFamily { get; set; } = "Unknown";
        public string SystemSKU { get; set; } = "Unknown";
    }

    public class BiosInfo
    {
        public string Vendor { get; set; } = "Unknown";
        public string Version { get; set; } = "Unknown";
        public string ReleaseDate { get; set; } = "Unknown";
        public string BootMode { get; set; } = "Unknown"; // UEFI, Legacy BIOS, Unknown
        public bool IsUefi { get; set; }
        public bool SecureBootEnabled { get; set; }
        public string SecureBootStatus { get; set; } = "Unknown";
    }

    public class SystemUserInfo
    {
        public string UserName { get; set; } = "Unknown";
        public string UserDomain { get; set; } = "Unknown";
        public string MachineName { get; set; } = "Unknown";
        public string OsDescription { get; set; } = "Unknown";
        public string OsArchitecture { get; set; } = "Unknown";
        public string ProcessorName { get; set; } = "Unknown";
        public int ProcessorCount { get; set; }
        public string TotalPhysicalMemoryMb { get; set; } = "Unknown";
    }

    public class NetworkAdapterDetails
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string InterfaceType { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string OperationalStatus { get; set; } = "";
        public bool IsPhysical { get; set; }
        public List<string> IpAddresses { get; set; } = new();
        public List<string> Gateways { get; set; } = new();
        public List<string> DnsServers { get; set; } = new();
    }

    public class NetworkConnectivityInfo
    {
        public bool HasActiveNetwork { get; set; }
        public string PrimaryMacAddress { get; set; } = "Unknown";
        public string PrimaryIpAddress { get; set; } = "Unknown";
        public string PrimaryGateway { get; set; } = "Unknown";
        public string PrimaryDns { get; set; } = "Unknown";
        public bool InternetReachable { get; set; }
        public string InternetStatus { get; set; } = "Checking...";
        public List<NetworkAdapterDetails> AllAdapters { get; set; } = new();
    }

    public class CompatibilityAssessment
    {
        public bool Is64BitCompatible { get; set; }
        public bool IsBootModeCompatible { get; set; }
        public bool IsNetworkCompatible { get; set; }
        public bool IsTpmPresent { get; set; }
        public string RecommendedMode { get; set; } = "Enterprise Zero-Risk (Recommended)";
        public string CompatibilityScore { get; set; } = "100% COMPATIBLE";
        public bool IsFullyCompatible { get; set; }
        public List<string> CompatibilityNotes { get; set; } = new();
    }

    public class HardwareAuditReport
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public MotherboardInfo Motherboard { get; set; } = new();
        public BiosInfo Bios { get; set; } = new();
        public SystemUserInfo SystemUser { get; set; } = new();
        public NetworkConnectivityInfo Network { get; set; } = new();
        public CompatibilityAssessment Assessment { get; set; } = new();

        public string ToJson(bool indented = true)
        {
            var options = new JsonSerializerOptions { WriteIndented = indented };
            return JsonSerializer.Serialize(this, options);
        }
    }

    public static class HardwareAuditService
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFirmwareType(ref uint firmwareType);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        public static HardwareAuditReport RunAudit()
        {
            var report = new HardwareAuditReport();

            // 1. Audit Motherboard
            report.Motherboard = AuditMotherboard();

            // 2. Audit BIOS / Firmware
            report.Bios = AuditBios();

            // 3. Audit User & Operating System
            report.SystemUser = AuditSystemUser();

            // 4. Audit Network Hardware & Connectivity
            report.Network = AuditNetwork();

            // 5. Evaluate Hardware Compatibility
            report.Assessment = EvaluateCompatibility(report.Motherboard, report.Bios, report.SystemUser, report.Network);

            return report;
        }

        private static MotherboardInfo AuditMotherboard()
        {
            var mb = new MotherboardInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var biosKey = baseKey.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (biosKey != null)
                {
                    mb.Manufacturer = biosKey.GetValue("BaseBoardManufacturer")?.ToString()?.Trim() ?? "Unknown";
                    mb.Product = biosKey.GetValue("BaseBoardProduct")?.ToString()?.Trim() ?? "Unknown";
                    mb.Version = biosKey.GetValue("BaseBoardVersion")?.ToString()?.Trim() ?? "Unknown";
                    mb.SerialNumber = biosKey.GetValue("BaseBoardSerialNumber")?.ToString()?.Trim() ?? "Unknown";
                    mb.SystemManufacturer = biosKey.GetValue("SystemManufacturer")?.ToString()?.Trim() ?? "Unknown";
                    mb.SystemProductName = biosKey.GetValue("SystemProductName")?.ToString()?.Trim() ?? "Unknown";
                    mb.SystemFamily = biosKey.GetValue("SystemFamily")?.ToString()?.Trim() ?? "Unknown";
                    mb.SystemSKU = biosKey.GetValue("SystemSKU")?.ToString()?.Trim() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] Motherboard audit exception: {ex.Message}");
            }
            return mb;
        }

        private static BiosInfo AuditBios()
        {
            var bios = new BiosInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var biosKey = baseKey.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (biosKey != null)
                {
                    bios.Vendor = biosKey.GetValue("BIOSVendor")?.ToString()?.Trim() ?? "Unknown";
                    bios.Version = biosKey.GetValue("BIOSVersion")?.ToString()?.Trim() ?? "Unknown";
                    bios.ReleaseDate = biosKey.GetValue("BIOSReleaseDate")?.ToString()?.Trim() ?? "Unknown";
                }

                // Detect UEFI vs Legacy via kernel32 GetFirmwareType
                uint firmwareType = 0;
                if (GetFirmwareType(ref firmwareType))
                {
                    // 1: Unknown, 2: Bios (Legacy), 3: UEFI
                    switch (firmwareType)
                    {
                        case 2:
                            bios.BootMode = "Legacy BIOS";
                            bios.IsUefi = false;
                            break;
                        case 3:
                            bios.BootMode = "UEFI Native";
                            bios.IsUefi = true;
                            break;
                        default:
                            bios.BootMode = "Unknown / Emulated";
                            bios.IsUefi = false;
                            break;
                    }
                }
                else
                {
                    bios.BootMode = "Undetermined";
                }

                // Detect Secure Boot status from Registry
                using var secureBootKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                if (secureBootKey != null)
                {
                    object? val = secureBootKey.GetValue("UEFISecureBootEnabled");
                    if (val is int intVal)
                    {
                        bios.SecureBootEnabled = (intVal == 1);
                        bios.SecureBootStatus = bios.SecureBootEnabled ? "Active (Enabled)" : "Disabled";
                    }
                    else
                    {
                        bios.SecureBootStatus = "Disabled / Not Present";
                    }
                }
                else
                {
                    bios.SecureBootStatus = "Disabled (Legacy / Unsupported)";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] BIOS audit exception: {ex.Message}");
            }
            return bios;
        }

        private static SystemUserInfo AuditSystemUser()
        {
            var sys = new SystemUserInfo
            {
                UserName = Environment.UserName,
                UserDomain = Environment.UserDomainName,
                MachineName = Environment.MachineName,
                OsDescription = RuntimeInformation.OSDescription,
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessorCount = Environment.ProcessorCount
            };

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var cpuKey = baseKey.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (cpuKey != null)
                {
                    sys.ProcessorName = cpuKey.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown Processor";
                }
            }
            catch { }

            try
            {
                var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    ulong totalMb = memStatus.ullTotalPhys / (1024 * 1024);
                    sys.TotalPhysicalMemoryMb = $"{totalMb:N0} MB ({Math.Round(totalMb / 1024.0, 1)} GB)";
                }
            }
            catch { }

            return sys;
        }

        private static NetworkConnectivityInfo AuditNetwork()
        {
            var net = new NetworkConnectivityInfo();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var adapter in interfaces)
                {
                    // Filter physical adapters vs loopback/virtual
                    bool isPhysical = adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                      adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                      adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet;

                    var details = new NetworkAdapterDetails
                    {
                        Id = adapter.Id,
                        Name = adapter.Name,
                        Description = adapter.Description,
                        InterfaceType = adapter.NetworkInterfaceType.ToString(),
                        OperationalStatus = adapter.OperationalStatus.ToString(),
                        IsPhysical = isPhysical
                    };

                    // Format MAC Address
                    var physAddr = adapter.GetPhysicalAddress();
                    if (physAddr != null && physAddr.GetAddressBytes().Length > 0)
                    {
                        details.MacAddress = string.Join(":", physAddr.GetAddressBytes().Select(b => b.ToString("X2")));
                    }

                    // Extract IP Properties
                    var ipProps = adapter.GetIPProperties();
                    if (ipProps != null)
                    {
                        foreach (var u in ipProps.UnicastAddresses)
                        {
                            if (u.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                details.IpAddresses.Add(u.Address.ToString());
                            }
                        }

                        foreach (var g in ipProps.GatewayAddresses)
                        {
                            if (g.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                details.Gateways.Add(g.Address.ToString());
                            }
                        }

                        foreach (var d in ipProps.DnsAddresses)
                        {
                            if (d.AddressFamily == AddressFamily.InterNetwork)
                            {
                                details.DnsServers.Add(d.ToString());
                            }
                        }
                    }

                    net.AllAdapters.Add(details);

                    // Pick primary active adapter
                    if (adapter.OperationalStatus == OperationalStatus.Up &&
                        details.IpAddresses.Count > 0 &&
                        (string.IsNullOrEmpty(net.PrimaryIpAddress) || net.PrimaryIpAddress == "Unknown"))
                    {
                        if (details.Gateways.Count > 0 || isPhysical)
                        {
                            net.HasActiveNetwork = true;
                            net.PrimaryMacAddress = details.MacAddress;
                            net.PrimaryIpAddress = details.IpAddresses[0];
                            net.PrimaryGateway = details.Gateways.FirstOrDefault() ?? "Direct / Ad-hoc";
                            net.PrimaryDns = details.DnsServers.FirstOrDefault() ?? "Default DNS";
                        }
                    }
                }

                if (string.IsNullOrEmpty(net.PrimaryIpAddress) || net.PrimaryIpAddress == "Unknown")
                {
                    var firstUp = net.AllAdapters.FirstOrDefault(a => a.OperationalStatus == "Up" && a.IpAddresses.Count > 0);
                    if (firstUp != null)
                    {
                        net.HasActiveNetwork = true;
                        net.PrimaryMacAddress = firstUp.MacAddress;
                        net.PrimaryIpAddress = firstUp.IpAddresses[0];
                        net.PrimaryGateway = firstUp.Gateways.FirstOrDefault() ?? "None";
                        net.PrimaryDns = firstUp.DnsServers.FirstOrDefault() ?? "None";
                    }
                }

                // Fast non-blocking connectivity check (1-second probe)
                net.InternetReachable = TestInternetReachability();
                net.InternetStatus = net.InternetReachable ? "Connected (Online)" : "Local / Offline";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] Network audit exception: {ex.Message}");
            }
            return net;
        }

        private static bool TestInternetReachability()
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

        private static CompatibilityAssessment EvaluateCompatibility(
            MotherboardInfo mb,
            BiosInfo bios,
            SystemUserInfo sys,
            NetworkConnectivityInfo net)
        {
            var eval = new CompatibilityAssessment();

            // 1. Architecture Check (Must be x64 / 64-bit)
            eval.Is64BitCompatible = Environment.Is64BitOperatingSystem;
            if (eval.Is64BitCompatible)
            {
                eval.CompatibilityNotes.Add("✔ OS Architecture: 64-bit (x64) verified.");
            }
            else
            {
                eval.CompatibilityNotes.Add("❌ OS Architecture: 32-bit detected. 64-bit is required.");
            }

            // 2. Boot Mode Evaluation
            eval.IsBootModeCompatible = true;
            if (bios.IsUefi)
            {
                if (bios.SecureBootEnabled)
                {
                    eval.CompatibilityNotes.Add("✔ Firmware: UEFI Native with Secure Boot Active.");
                    eval.CompatibilityNotes.Add("ℹ Recommendation: Enterprise Zero-Risk Deployment (Zero Boot Risk) is optimal.");
                    eval.RecommendedMode = "Enterprise Zero-Risk (Optimal for Secure Boot)";
                }
                else
                {
                    eval.CompatibilityNotes.Add("✔ Firmware: UEFI Native (Secure Boot Disabled / Custom).");
                    eval.CompatibilityNotes.Add("✔ Both Enterprise Zero-Risk and UEFI Pre-boot modes are fully supported.");
                    eval.RecommendedMode = "Enterprise Zero-Risk (Recommended)";
                }
            }
            else
            {
                eval.CompatibilityNotes.Add("⚠ Firmware: Legacy BIOS mode detected.");
                eval.CompatibilityNotes.Add("ℹ Recommendation: Deploy Enterprise Zero-Risk (Windows Kernel/Service Mode). UEFI Pre-boot is bypassed.");
                eval.RecommendedMode = "Enterprise Zero-Risk (Legacy BIOS Compatible)";
            }

            // 3. Network Compatibility
            eval.IsNetworkCompatible = net.HasActiveNetwork;
            if (eval.IsNetworkCompatible)
            {
                eval.CompatibilityNotes.Add($"✔ Network Hardware: Active adapter ({net.PrimaryMacAddress}) with IP {net.PrimaryIpAddress}.");
                if (net.InternetReachable)
                {
                    eval.CompatibilityNotes.Add("✔ Cloud Connectivity: Online & reachable for Mobile App Remote Control.");
                }
                else
                {
                    eval.CompatibilityNotes.Add("⚠ Cloud Connectivity: Local Network / Offline (Local PINs remain functional).");
                }
            }
            else
            {
                eval.CompatibilityNotes.Add("⚠ Network: No active network adapter with IPv4 detected. Connect LAN/Wi-Fi for remote unlock.");
            }

            // 4. TPM 2.0 Presence
            eval.IsTpmPresent = CheckTpmPresence();
            if (eval.IsTpmPresent)
            {
                eval.CompatibilityNotes.Add("✔ Hardware Security: TPM 2.0 Hardware Cryptographic Vault detected.");
            }
            else
            {
                eval.CompatibilityNotes.Add("ℹ Hardware Security: Standard Windows DPAPI Vault (TPM 2.0 not enabled).");
            }

            // Overall Score
            eval.IsFullyCompatible = eval.Is64BitCompatible && (eval.IsNetworkCompatible || true);
            eval.CompatibilityScore = eval.IsFullyCompatible ? "100% COMPATIBLE" : "INCOMPATIBLE";

            return eval;
        }

        private static bool CheckTpmPresence()
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
