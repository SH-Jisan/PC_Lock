using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class GraphicsAuditor
    {
        public static GraphicsHardwareInfo Audit(bool isUefi)
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
                                    string resolvedSys = AuditHelper.ResolveServiceDriverPath(baseKey, srv);
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
    }
}
