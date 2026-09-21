using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeployManager.Services.HardwareAudit.Auditors
{
    public static class UserSystemAuditor
    {
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

        public static SystemUserInfo Audit()
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
    }
}
