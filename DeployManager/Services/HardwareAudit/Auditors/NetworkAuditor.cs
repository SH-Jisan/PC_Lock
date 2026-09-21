using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class NetworkAuditor
    {
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

        public static NetworkConnectivityInfo Audit()
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
                net.InternetReachable = AuditHelper.TestInternetReachability();
                net.InternetStatus = net.InternetReachable ? "Connected (Online)" : "Local / Offline";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareAudit] Network audit exception: {ex.Message}");
            }
            return net;
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
                                        string sysPath = AuditHelper.ResolveServiceDriverPath(baseKey, srv);
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
    }
}
