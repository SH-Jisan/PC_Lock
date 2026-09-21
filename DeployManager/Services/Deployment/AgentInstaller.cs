using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DeployManager.Services
{
    /// <summary>
    /// Manages the installation, configuration, execution, and cleanup of the PC Security Agent daemon.
    /// </summary>
    public static class AgentInstaller
    {
        public const string PermanentDir = @"C:\Program Files\PCSecuritySystem";
        public const string ProcessName = "PC.SecurityAgent.exe";
        public const string RunKeyName = "PCSecurityAgent";

        public static void ResolveSecurityAgent(string baseDir, out string exePath, out string arguments)
        {
            string[] exeCandidates = new[]
            {
                Path.Combine(baseDir, "PC.SecurityAgent.exe"),
                Path.Combine(baseDir, "Agent", "PC.SecurityAgent.exe"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\Agent\PC.SecurityAgent.exe")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\bin_publish\PC.SecurityAgent.exe")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\release_package\Agent\PC.SecurityAgent.exe")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\pc-agent\bin\Release\net8.0-windows\win-x64\publish\PC.SecurityAgent.exe")),
                Path.Combine(PermanentDir, ProcessName)
            };

            foreach (var path in exeCandidates)
            {
                if (File.Exists(path))
                {
                    exePath = path;
                    arguments = "";
                    return;
                }
            }

            string[] dllCandidates = new[]
            {
                Path.Combine(baseDir, "PC.SecurityAgent.dll"),
                Path.Combine(baseDir, "Agent", "PC.SecurityAgent.dll"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\pc-agent\bin\App\PC.SecurityAgent.dll")),
                @"D:\Soft\PC_Lock\pc-agent\bin\App\PC.SecurityAgent.dll"
            };

            foreach (var path in dllCandidates)
            {
                if (File.Exists(path))
                {
                    string dotnet = @"D:\Soft\dotnet\dotnet.exe";
                    if (!File.Exists(dotnet)) dotnet = "dotnet";
                    exePath = dotnet;
                    arguments = $"\"{path}\"";
                    return;
                }
            }

            exePath = Path.Combine(baseDir, ProcessName);
            arguments = "";
        }

        public static bool InstallPermanent(ref string agentExe, ref string agentArgs, Action<string> log)
        {
            try
            {
                if (!Directory.Exists(PermanentDir)) Directory.CreateDirectory(PermanentDir);

                string permanentAgentExe = Path.Combine(PermanentDir, ProcessName);
                if (!string.Equals(agentExe, permanentAgentExe, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(agentExe, permanentAgentExe, true);
                    agentExe = permanentAgentExe;
                    agentArgs = "";
                    log("[✔] Copied standalone agent to permanent path: C:\\Program Files\\PCSecuritySystem\\");
                }

                // Pre-flight hardware audit and save persistent profile
                try
                {
                    var audit = HardwareAuditService.RunAudit();
                    string auditJson = audit.ToJson(true);
                    File.WriteAllText(Path.Combine(PermanentDir, "hardware_audit.json"), auditJson);
                    log($"[✔] Workstation Hardware Profile: {audit.Motherboard.Manufacturer} {audit.Motherboard.Product} [MAC: {audit.Network.PrimaryMacAddress}]");
                    log("[✔] Saved hardware audit profile: C:\\Program Files\\PCSecuritySystem\\hardware_audit.json");
                }
                catch (Exception aEx)
                {
                    log($"[!] Hardware profile note: {aEx.Message}");
                }
                return true;
            }
            catch (Exception ex)
            {
                log($"[!] Warning during permanent agent install: {ex.Message}");
                return false;
            }
        }

        public static void TerminateRunningAgent()
        {
            BootloaderManager.ExecuteCommand("taskkill", $"/F /IM {ProcessName}");
        }

        public static bool StartAgentProcess(string agentExe, string agentArgs, Action<string> log)
        {
            TerminateRunningAgent();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = agentExe,
                    Arguments = agentArgs,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
                return true;
            }
            catch (Exception pEx)
            {
                log($"❌ [ERROR] Failed to start Security Agent: {pEx.Message}");
                return false;
            }
        }

        public static void ConfigureRunKey(string agentExe, string agentArgs)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                string runCmd = string.IsNullOrEmpty(agentArgs) ? $"\"{agentExe}\"" : $"\"{agentExe}\" {agentArgs}";
                key?.SetValue(RunKeyName, runCmd);
            }
            catch { }
        }

        public static void StopAndPurgeServices(Action<string> log)
        {
            BootloaderManager.ExecuteCommand("sc", "stop \"PCSecurityAgent\"");
            BootloaderManager.ExecuteCommand("sc", "delete \"PCSecurityAgent\"");
            BootloaderManager.ExecuteCommand("sc", "stop \"PCSecurityAgentService\"");
            BootloaderManager.ExecuteCommand("sc", "delete \"PCSecurityAgentService\"");
            TerminateRunningAgent();
            log("[✔] All security daemons and background tasks stopped.");
        }

        public static void CleanRegistryAndFiles(Action<string> log)
        {
            try
            {
                using var runKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.DeleteValue("PCSecurityAgent", false);
                runKey?.DeleteValue("PCSecurityAgentService", false);

                Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\PCSecuritySystem", false);
                log("[✔] Registry configuration keys completely purged.");

                if (Directory.Exists(PermanentDir))
                {
                    try
                    {
                        Directory.Delete(PermanentDir, true);
                        log("[✔] Cleaned permanent C:\\Program Files\\PCSecuritySystem\\ folder.");
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
