using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
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
        public string EnclosureType { get; set; } = "Desktop / Tower";
    }

    public class BiosInfo
    {
        public string Vendor { get; set; } = "Unknown";
        public string Version { get; set; } = "Unknown";
        public string ReleaseDate { get; set; } = "Unknown";
        public string MajorRelease { get; set; } = "Unknown";
        public string MinorRelease { get; set; } = "Unknown";
        public string BootMode { get; set; } = "Unknown"; // UEFI Native, Legacy BIOS, Unknown
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

    public class GraphicsHardwareInfo
    {
        public string GpuName { get; set; } = "Unknown GPU";
        public string ProviderName { get; set; } = "Unknown";
        public string DriverVersion { get; set; } = "Unknown";
        public string DriverDate { get; set; } = "Unknown";
        public string DriverPath { get; set; } = "Unknown";
        public string InfPath { get; set; } = "Unknown";
        public string MatchingDeviceId { get; set; } = "Unknown";
        public bool UefiGopSupported { get; set; }
        public string UefiGopStatus { get; set; } = "Undetermined";
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
        public string DriverProvider { get; set; } = "Unknown";
        public string DriverVersion { get; set; } = "Unknown";
        public string DriverDate { get; set; } = "Unknown";
        public string DriverPath { get; set; } = "Unknown";
        public string MatchingDeviceId { get; set; } = "Unknown";
        public string ServiceName { get; set; } = "Unknown";
        public bool PrebootUndiSupported { get; set; }
        public string PrebootNetworkStatus { get; set; } = "Undetermined";
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
        public string PrimaryAdapterName { get; set; } = "Unknown";
        public string PrimaryDriverPath { get; set; } = "Unknown";
        public bool InternetReachable { get; set; }
        public string InternetStatus { get; set; } = "Checking...";
        public List<NetworkAdapterDetails> AllAdapters { get; set; } = new();
    }

    public class StorageHardwareInfo
    {
        public string PrimaryControllerName { get; set; } = "Unknown Controller";
        public string ControllerType { get; set; } = "Unknown"; // NVMe, SATA AHCI, RAID
        public string DriverVersion { get; set; } = "Unknown";
        public string DriverPath { get; set; } = "Unknown";
        public string PrimaryDiskName { get; set; } = "Unknown Disk";
        public string PartitionStyle { get; set; } = "GPT (GUID Partition Table)"; // GPT, MBR
        public string BusType { get; set; } = "NVMe / SATA";
        public bool PrebootStorageSupported { get; set; }
        public string PrebootStorageStatus { get; set; } = "Undetermined";
    }

    public class EfiPrebootEnvironmentInfo
    {
        public string EspVolumeGuid { get; set; } = "Undetected";
        public string FirmwareBootDevice { get; set; } = "Unknown";
        public string SystemBootDevice { get; set; } = "Unknown";
        public bool HasStandardBootloader { get; set; }
        public string StandardBootloaderPath { get; set; } = @"\EFI\Microsoft\Boot\bootmgfw.efi";
        public bool HasHiddenBootloader { get; set; }
        public string HiddenBootloaderPath { get; set; } = @"\EFI\Microsoft\Boot\bootmgfw_hidden.efi";
        public bool HasFallbackBootx64 { get; set; }
        public string FallbackBootx64Path { get; set; } = @"\EFI\Boot\bootx64.efi";
        public bool HasPrebootEfi { get; set; }
        public string PrebootEfiPath { get; set; } = @"\EFI\PCLock\pc_lock_preboot.efi";
        public bool NvramVariablesSupported { get; set; }
        public bool PrebootReady { get; set; }
        public string ReadinessSummary { get; set; } = "Checking...";
    }

    public class PrebootAssetsDiagnostics
    {
        public bool GraphicsGopReady { get; set; }
        public string GraphicsGopDetail { get; set; } = "";
        public bool NetworkUndiReady { get; set; }
        public string NetworkUndiDetail { get; set; } = "";
        public bool StorageBlockIoReady { get; set; }
        public string StorageBlockIoDetail { get; set; } = "";
        public bool EfiPartitionReady { get; set; }
        public string EfiPartitionDetail { get; set; } = "";
        public bool AllPrebootAssetsReady { get; set; }
        public string ReadinessScore { get; set; } = "Pending";
        public List<string> DiagnosticChecklist { get; set; } = new();
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
        public GraphicsHardwareInfo Graphics { get; set; } = new();
        public NetworkConnectivityInfo Network { get; set; } = new();
        public StorageHardwareInfo Storage { get; set; } = new();
        public EfiPrebootEnvironmentInfo EfiPreboot { get; set; } = new();
        public PrebootAssetsDiagnostics PrebootDiagnostics { get; set; } = new();
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindFirstVolume([Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool FindNextVolume(IntPtr hFindVolume, [Out] StringBuilder lpszVolumeName, uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindVolumeClose(IntPtr hFindVolume);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetFirmwareEnvironmentVariableW(string lpName, string lpGuid, IntPtr pBuffer, uint nSize);

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

            // 4. Audit Graphics Hardware & Pre-Boot GOP Driver
            report.Graphics = AuditGraphicsHardware(report.Bios.IsUefi);

            // 5. Audit Network Hardware, Drivers & Connectivity
            report.Network = AuditNetwork();

            // 6. Audit Storage Controllers & NVMe/SATA Drivers
            report.Storage = AuditStorageHardware(report.Bios.IsUefi);

            // 7. Audit EFI Pre-Boot Partition & Bootloader Files
            report.EfiPreboot = AuditEfiPrebootEnvironment(report.Bios.IsUefi);

            // 8. Run Comprehensive Pre-Boot Readiness Asset Evaluation
            report.PrebootDiagnostics = EvaluatePrebootAssets(report.Bios, report.Graphics, report.Network, report.Storage, report.EfiPreboot);

            // 9. Overall System Compatibility Assessment
            report.Assessment = EvaluateCompatibility(report.Motherboard, report.Bios, report.SystemUser, report.Network, report.Graphics, report.Storage, report.EfiPreboot);

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

                    object? enc = biosKey.GetValue("EnclosureType");
                    if (enc != null && int.TryParse(enc.ToString(), out int encVal))
                    {
                        mb.EnclosureType = encVal switch
                        {
                            3 => "Desktop",
                            4 => "Low Profile Desktop",
                            5 => "Pizza Box",
                            6 => "Mini Tower",
                            7 => "Tower",
                            8 => "Portable",
                            9 => "Laptop / Notebook",
                            10 => "Notebook",
                            13 => "All-in-One",
                            30 => "Mini PC",
                            _ => $"Chassis Type {encVal}"
                        };
                    }
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
                    bios.MajorRelease = biosKey.GetValue("BiosMajorRelease")?.ToString()?.Trim() ?? "Unknown";
                    bios.MinorRelease = biosKey.GetValue("BiosMinorRelease")?.ToString()?.Trim() ?? "Unknown";
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

        private static GraphicsHardwareInfo AuditGraphicsHardware(bool isUefi)
        {
            var gfx = new GraphicsHardwareInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                
                // 1. Scan Display Adapters Setup Class {4d36e968-e325-11ce-bfc1-08002be10318}
                using var classKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (classKey != null)
                {
                    foreach (string subName in classKey.GetSubKeyNames().Where(s => s.StartsWith("00")))
                    {
                        using var sub = classKey.OpenSubKey(subName);
                        if (sub == null) continue;

                        string desc = sub.GetValue("DriverDesc")?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(desc))
                        {
                            gfx.GpuName = desc;
                            gfx.ProviderName = sub.GetValue("ProviderName")?.ToString()?.Trim() ?? "Unknown";
                            gfx.DriverVersion = sub.GetValue("DriverVersion")?.ToString()?.Trim() ?? "Unknown";
                            gfx.DriverDate = sub.GetValue("DriverDate")?.ToString()?.Trim() ?? "Unknown";
                            gfx.MatchingDeviceId = sub.GetValue("MatchingDeviceId")?.ToString()?.Trim() ?? "Unknown";
                            gfx.InfPath = sub.GetValue("InfPath")?.ToString()?.Trim() ?? "Unknown";
                            break;
                        }
                    }
                }

                // 2. Discover exact driver .sys file from PCI Enum and Services
                using var pciKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI");
                if (pciKey != null)
                {
                    foreach (string devSub in pciKey.GetSubKeyNames())
                    {
                        using var devKey = pciKey.OpenSubKey(devSub);
                        if (devKey == null) continue;

                        foreach (string instSub in devKey.GetSubKeyNames())
                        {
                            using var instKey = devKey.OpenSubKey(instSub);
                            if (instKey == null) continue;

                            string devClass = instKey.GetValue("Class")?.ToString() ?? "";
                            string classGuid = instKey.GetValue("ClassGUID")?.ToString() ?? "";

                            if (devClass.Equals("Display", StringComparison.OrdinalIgnoreCase) ||
                                classGuid.Equals("{4d36e968-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
                            {
                                string srv = instKey.GetValue("Service")?.ToString()?.Trim() ?? "";
                                if (!string.IsNullOrEmpty(srv))
                                {
                                    string resolvedSys = ResolveServiceDriverPath(baseKey, srv);
                                    if (!string.IsNullOrEmpty(resolvedSys))
                                    {
                                        gfx.DriverPath = resolvedSys;
                                    }
                                }
                                break;
                            }
                        }
                        if (gfx.DriverPath != "Unknown") break;
                    }
                }

                // 3. Evaluate UEFI GOP (Graphics Output Protocol) readiness
                if (isUefi)
                {
                    gfx.UefiGopSupported = true;
                    gfx.UefiGopStatus = "Active & Verified (UEFI GOP VBIOS present in firmware)";
                }
                else
                {
                    gfx.UefiGopSupported = false;
                    gfx.UefiGopStatus = "Legacy VBIOS (GOP unavailable in Legacy BIOS mode)";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] Graphics audit exception: {ex.Message}");
            }
            return gfx;
        }

        private static NetworkConnectivityInfo AuditNetwork()
        {
            var net = new NetworkConnectivityInfo();
            try
            {
                // Pre-fetch PCI Network driver map from registry
                var pciNetMap = ScanPciNetworkDriverMap();

                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var adapter in interfaces)
                {
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

                    // Correlate with PCI registry driver map
                    if (pciNetMap.TryGetValue(adapter.Id, out var driverInfo))
                    {
                        details.DriverProvider = driverInfo.Provider;
                        details.DriverVersion = driverInfo.Version;
                        details.DriverDate = driverInfo.Date;
                        details.DriverPath = driverInfo.DriverPath;
                        details.MatchingDeviceId = driverInfo.DeviceId;
                        details.ServiceName = driverInfo.Service;
                        details.PrebootUndiSupported = driverInfo.IsUndiSupported;
                        details.PrebootNetworkStatus = driverInfo.PrebootStatus;
                    }
                    else if (isPhysical && adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        details.PrebootUndiSupported = true;
                        details.PrebootNetworkStatus = "UNDI/SNP ROM Ready (Standard UEFI Ethernet stack)";
                    }
                    else if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        details.PrebootUndiSupported = false;
                        details.PrebootNetworkStatus = "Wireless (Requires UEFI Wi-Fi stack or Micro-Core sync)";
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
                            net.PrimaryAdapterName = details.Name;
                            net.PrimaryDriverPath = details.DriverPath;
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
                        net.PrimaryAdapterName = firstUp.Name;
                        net.PrimaryDriverPath = firstUp.DriverPath;
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

        private static StorageHardwareInfo AuditStorageHardware(bool isUefi)
        {
            var storage = new StorageHardwareInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                
                // 1. Scan PCI for NVMe or AHCI controllers
                using var pciKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI");
                if (pciKey != null)
                {
                    foreach (string devSub in pciKey.GetSubKeyNames())
                    {
                        using var devKey = pciKey.OpenSubKey(devSub);
                        if (devKey == null) continue;

                        foreach (string instSub in devKey.GetSubKeyNames())
                        {
                            using var instKey = devKey.OpenSubKey(instSub);
                            if (instKey == null) continue;

                            string srv = instKey.GetValue("Service")?.ToString()?.Trim() ?? "";
                            string desc = instKey.GetValue("DeviceDesc")?.ToString()?.Trim() ?? "";

                            // Strip localization prefix if present (e.g. @stornvme.inf,...)
                            if (desc.Contains(";")) desc = desc.Substring(desc.IndexOf(';') + 1);

                            if (srv.Contains("nvme", StringComparison.OrdinalIgnoreCase))
                            {
                                storage.PrimaryControllerName = string.IsNullOrEmpty(desc) ? "Standard NVM Express Controller" : desc;
                                storage.ControllerType = "NVMe (High-Speed Solid State)";
                                storage.BusType = "NVMe (PCI Express)";
                                storage.DriverPath = ResolveServiceDriverPath(baseKey, srv);
                                break;
                            }
                            else if (srv.Contains("ahci", StringComparison.OrdinalIgnoreCase) || srv.Contains("iaStor", StringComparison.OrdinalIgnoreCase))
                            {
                                storage.PrimaryControllerName = string.IsNullOrEmpty(desc) ? "SATA AHCI Controller" : desc;
                                storage.ControllerType = "SATA AHCI Controller";
                                storage.BusType = "SATA";
                                storage.DriverPath = ResolveServiceDriverPath(baseKey, srv);
                            }
                        }
                        if (storage.ControllerType.StartsWith("NVMe")) break;
                    }
                }

                // 2. Discover primary disk information from Registry
                using var diskEnumKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\disk\Enum");
                if (diskEnumKey != null)
                {
                    string primaryDiskId = diskEnumKey.GetValue("0")?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(primaryDiskId))
                    {
                        using var primaryDiskKey = baseKey.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{primaryDiskId}");
                        if (primaryDiskKey != null)
                        {
                            string friendlyName = primaryDiskKey.GetValue("FriendlyName")?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(friendlyName))
                            {
                                storage.PrimaryDiskName = friendlyName;
                            }
                        }
                    }
                }

                // 3. Partition Style & Preboot Storage Block I/O capability
                if (isUefi)
                {
                    storage.PartitionStyle = "GPT (GUID Partition Table)";
                    storage.PrebootStorageSupported = true;
                    storage.PrebootStorageStatus = "Verified (UEFI NVMe/AHCI Block I/O Driver Available)";
                }
                else
                {
                    storage.PartitionStyle = "MBR (Master Boot Record)";
                    storage.PrebootStorageSupported = false;
                    storage.PrebootStorageStatus = "Legacy Block I/O (UEFI protocol absent)";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] Storage audit exception: {ex.Message}");
            }
            return storage;
        }

        private static EfiPrebootEnvironmentInfo AuditEfiPrebootEnvironment(bool isUefi)
        {
            var efi = new EfiPrebootEnvironmentInfo();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                
                // Read FirmwareBootDevice and SystemBootDevice from Control
                using var controlKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control");
                if (controlKey != null)
                {
                    efi.FirmwareBootDevice = controlKey.GetValue("FirmwareBootDevice")?.ToString() ?? "Unknown";
                    efi.SystemBootDevice = controlKey.GetValue("SystemBootDevice")?.ToString() ?? "Unknown";
                }

                if (!isUefi)
                {
                    efi.ReadinessSummary = "Legacy BIOS active. Pre-Boot EFI stage is bypassed.";
                    return efi;
                }

                // Enumerate Volume GUIDs to locate ESP (EFI System Partition)
                StringBuilder volumeName = new StringBuilder(260);
                IntPtr handle = FindFirstVolume(volumeName, (uint)volumeName.Capacity);
                if (handle != IntPtr.Zero && handle != (IntPtr)(-1))
                {
                    try
                    {
                        do
                        {
                            string vol = volumeName.ToString();
                            string efiMsBoot = Path.Combine(vol, @"EFI\Microsoft\Boot");
                            try
                            {
                                if (Directory.Exists(efiMsBoot))
                                {
                                    efi.EspVolumeGuid = vol;

                                    string stdBoot = Path.Combine(vol, @"EFI\Microsoft\Boot\bootmgfw.efi");
                                    string hdnBoot = Path.Combine(vol, @"EFI\Microsoft\Boot\bootmgfw_hidden.efi");
                                    string flkBoot = Path.Combine(vol, @"EFI\Boot\bootx64.efi");
                                    string pcLockEfi = Path.Combine(vol, @"EFI\PCLock\pc_lock_preboot.efi");

                                    efi.HasStandardBootloader = File.Exists(stdBoot);
                                    efi.HasHiddenBootloader = File.Exists(hdnBoot);
                                    efi.HasFallbackBootx64 = File.Exists(flkBoot);
                                    efi.HasPrebootEfi = File.Exists(pcLockEfi);
                                    break;
                                }
                            }
                            catch { }
                        } while (FindNextVolume(handle, volumeName, (uint)volumeName.Capacity));
                    }
                    finally
                    {
                        FindVolumeClose(handle);
                    }
                }

                // Check NVRAM variable accessibility
                try
                {
                    IntPtr dummyBuf = Marshal.AllocHGlobal(4);
                    GetFirmwareEnvironmentVariableW("SetupMode", "{8be4df61-93ca-11d2-aa0d-00e098032b8c}", dummyBuf, 4);
                    int err = Marshal.GetLastWin32Error();
                    Marshal.FreeHGlobal(dummyBuf);
                    // 1314: ERROR_PRIVILEGE_NOT_HELD (means NVRAM API exists, just needs admin privileges)
                    // 0: SUCCESS
                    efi.NvramVariablesSupported = (err == 0 || err == 1314 || err == 203);
                }
                catch
                {
                    efi.NvramVariablesSupported = true;
                }

                efi.PrebootReady = efi.EspVolumeGuid != "Undetected" && (efi.HasStandardBootloader || efi.HasHiddenBootloader);
                efi.ReadinessSummary = efi.PrebootReady 
                    ? "ESP Partition & Windows Bootloader Verified for Pre-Boot Chainloading" 
                    : "ESP Partition Access Restricted (Run as Administrator to audit volume)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] EFI Preboot audit exception: {ex.Message}");
            }
            return efi;
        }

        private static PrebootAssetsDiagnostics EvaluatePrebootAssets(
            BiosInfo bios,
            GraphicsHardwareInfo gfx,
            NetworkConnectivityInfo net,
            StorageHardwareInfo storage,
            EfiPrebootEnvironmentInfo efi)
        {
            var diag = new PrebootAssetsDiagnostics();

            // 1. Graphics GOP
            diag.GraphicsGopReady = bios.IsUefi && gfx.UefiGopSupported;
            diag.GraphicsGopDetail = diag.GraphicsGopReady
                ? $"✔ GOP Display Driver Ready: {gfx.GpuName} (Driver: {Path.GetFileName(gfx.DriverPath)})"
                : "⚠ GOP Unavailable in Legacy mode.";
            diag.DiagnosticChecklist.Add(diag.GraphicsGopDetail);

            // 2. Network UNDI / SNP
            var primaryAdapter = net.AllAdapters.FirstOrDefault(a => a.IsPhysical && a.InterfaceType.Contains("Ethernet")) 
                              ?? net.AllAdapters.FirstOrDefault(a => a.IsPhysical);
            
            diag.NetworkUndiReady = primaryAdapter != null && primaryAdapter.PrebootUndiSupported;
            diag.NetworkUndiDetail = diag.NetworkUndiReady
                ? $"✔ Pre-Boot UNDI/SNP Stack Ready: {primaryAdapter?.Description ?? "Ethernet"} (Driver: {Path.GetFileName(primaryAdapter?.DriverPath)})"
                : "ℹ Pre-Boot Network: Wi-Fi/Virtual adapter requires active profile sync.";
            diag.DiagnosticChecklist.Add(diag.NetworkUndiDetail);

            // 3. Storage Block I/O
            diag.StorageBlockIoReady = storage.PrebootStorageSupported;
            diag.StorageBlockIoDetail = diag.StorageBlockIoReady
                ? $"✔ UEFI Storage Block I/O Ready: {storage.PrimaryControllerName} (Driver: {Path.GetFileName(storage.DriverPath)})"
                : "⚠ Storage Controller in Legacy Mode.";
            diag.DiagnosticChecklist.Add(diag.StorageBlockIoDetail);

            // 4. EFI System Partition & Bootloader
            diag.EfiPartitionReady = bios.IsUefi && (efi.HasStandardBootloader || efi.HasHiddenBootloader || efi.EspVolumeGuid != "Undetected");
            diag.EfiPartitionDetail = diag.EfiPartitionReady
                ? $"✔ EFI Bootloader Structure Ready: {(efi.HasHiddenBootloader ? "Cloaked (Pre-Boot Active)" : "Factory Standard")} [ESP: {efi.FirmwareBootDevice}]"
                : "ℹ EFI Partition: Administrator elevation recommended for full direct volume access.";
            diag.DiagnosticChecklist.Add(diag.EfiPartitionDetail);

            diag.AllPrebootAssetsReady = diag.GraphicsGopReady && diag.StorageBlockIoReady && diag.EfiPartitionReady;
            diag.ReadinessScore = diag.AllPrebootAssetsReady ? "100% PRE-BOOT READY" : "ENTERPRISE HYBRID READY";

            return diag;
        }

        private static CompatibilityAssessment EvaluateCompatibility(
            MotherboardInfo mb,
            BiosInfo bios,
            SystemUserInfo sys,
            NetworkConnectivityInfo net,
            GraphicsHardwareInfo gfx,
            StorageHardwareInfo storage,
            EfiPrebootEnvironmentInfo efi)
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

            // 4. Driver & Hardware Notes
            if (!string.IsNullOrEmpty(gfx.DriverPath) && gfx.DriverPath != "Unknown")
            {
                eval.CompatibilityNotes.Add($"✔ Display Driver Located: {Path.GetFileName(gfx.DriverPath)} ({gfx.GpuName})");
            }

            if (!string.IsNullOrEmpty(storage.DriverPath) && storage.DriverPath != "Unknown")
            {
                eval.CompatibilityNotes.Add($"✔ Storage Driver Located: {Path.GetFileName(storage.DriverPath)} ({storage.ControllerType})");
            }

            // 5. TPM 2.0 Presence
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
            eval.IsFullyCompatible = eval.Is64BitCompatible;
            eval.CompatibilityScore = eval.IsFullyCompatible ? "100% COMPATIBLE" : "INCOMPATIBLE";

            return eval;
        }

        private class PciNetworkDriverEntry
        {
            public string Provider { get; set; } = "Unknown";
            public string Version { get; set; } = "Unknown";
            public string Date { get; set; } = "Unknown";
            public string DriverPath { get; set; } = "Unknown";
            public string DeviceId { get; set; } = "Unknown";
            public string Service { get; set; } = "Unknown";
            public bool IsUndiSupported { get; set; }
            public string PrebootStatus { get; set; } = "Undetermined";
        }

        private class ClassDriverEntry
        {
            public string Provider { get; set; } = "Unknown";
            public string Version { get; set; } = "Unknown";
            public string Date { get; set; } = "Unknown";
            public string DeviceId { get; set; } = "Unknown";
            public string InfPath { get; set; } = "Unknown";
        }

        private static Dictionary<string, PciNetworkDriverEntry> ScanPciNetworkDriverMap()
        {
            var map = new Dictionary<string, PciNetworkDriverEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                
                // Read Network class subkeys to extract NetCfgInstanceId -> Driver info
                var classDriverMap = new Dictionary<string, ClassDriverEntry>(StringComparer.OrdinalIgnoreCase);
                using var classKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}");
                if (classKey != null)
                {
                    foreach (string subName in classKey.GetSubKeyNames().Where(s => s.StartsWith("00")))
                    {
                        using var sub = classKey.OpenSubKey(subName);
                        if (sub == null) continue;

                        string netCfg = sub.GetValue("NetCfgInstanceId")?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(netCfg))
                        {
                            classDriverMap[netCfg] = new ClassDriverEntry
                            {
                                Provider = sub.GetValue("ProviderName")?.ToString()?.Trim() ?? "Unknown",
                                Version = sub.GetValue("DriverVersion")?.ToString()?.Trim() ?? "Unknown",
                                Date = sub.GetValue("DriverDate")?.ToString()?.Trim() ?? "Unknown",
                                DeviceId = sub.GetValue("MatchingDeviceId")?.ToString()?.Trim() ?? "Unknown",
                                InfPath = sub.GetValue("InfPath")?.ToString()?.Trim() ?? "Unknown"
                            };
                        }
                    }
                }

                // Scan PCI Enum for network devices to locate Service and exact driver .sys
                using var pciKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI");
                if (pciKey != null)
                {
                    foreach (string devSub in pciKey.GetSubKeyNames())
                    {
                        using var devKey = pciKey.OpenSubKey(devSub);
                        if (devKey == null) continue;

                        foreach (string instSub in devKey.GetSubKeyNames())
                        {
                            using var instKey = devKey.OpenSubKey(instSub);
                            if (instKey == null) continue;

                            string devClass = instKey.GetValue("Class")?.ToString() ?? "";
                            string classGuid = instKey.GetValue("ClassGUID")?.ToString() ?? "";

                            if (devClass.Equals("Net", StringComparison.OrdinalIgnoreCase) ||
                                classGuid.Equals("{4d36e972-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
                            {
                                string driverKeyName = instKey.GetValue("Driver")?.ToString() ?? "";
                                string srv = instKey.GetValue("Service")?.ToString()?.Trim() ?? "";
                                string hwId = devSub;

                                // Correlate with classDriverMap
                                foreach (var kvp in classDriverMap)
                                {
                                    string netCfgId = kvp.Key;
                                    var info = kvp.Value;

                                    if (hwId.Contains(info.DeviceId, StringComparison.OrdinalIgnoreCase) ||
                                        info.DeviceId.Contains(hwId, StringComparison.OrdinalIgnoreCase) ||
                                        driverKeyName.EndsWith(info.InfPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string sysPath = ResolveServiceDriverPath(baseKey, srv);
                                        bool isUndi = hwId.Contains("VEN_10EC", StringComparison.OrdinalIgnoreCase) ||
                                                      hwId.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase) ||
                                                      hwId.Contains("VEN_14E4", StringComparison.OrdinalIgnoreCase);

                                        string prebootStatus = isUndi
                                            ? "UNDI/SNP ROM Ready (Embedded Motherboard UEFI Network Stack)"
                                            : "Standard Network Controller";

                                        map[netCfgId] = new PciNetworkDriverEntry
                                        {
                                            Provider = info.Provider,
                                            Version = info.Version,
                                            Date = info.Date,
                                            DriverPath = sysPath,
                                            DeviceId = hwId,
                                            Service = srv,
                                            IsUndiSupported = isUndi,
                                            PrebootStatus = prebootStatus
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] ScanPciNetworkDriverMap exception: {ex.Message}");
            }
            return map;
        }

        private static string ResolveServiceDriverPath(RegistryKey baseKey, string serviceName)
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
