using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class MotherboardAuditor
    {
        public static MotherboardInfo Audit()
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
    }
}
