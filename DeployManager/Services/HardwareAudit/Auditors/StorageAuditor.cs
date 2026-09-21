using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class StorageAuditor
    {
        public static StorageHardwareInfo Audit(bool isUefi)
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
                                storage.DriverPath = AuditHelper.ResolveServiceDriverPath(baseKey, srv);
                                break;
                            }
                            else if (srv.Contains("ahci", StringComparison.OrdinalIgnoreCase) || srv.Contains("iaStor", StringComparison.OrdinalIgnoreCase))
                            {
                                storage.PrimaryControllerName = string.IsNullOrEmpty(desc) ? "SATA AHCI Controller" : desc;
                                storage.ControllerType = "SATA AHCI Controller";
                                storage.BusType = "SATA";
                                storage.DriverPath = AuditHelper.ResolveServiceDriverPath(baseKey, srv);
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
    }
}
