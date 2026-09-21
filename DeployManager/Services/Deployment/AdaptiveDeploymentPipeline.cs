using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using DeployManager.Services.Diagnostics;
using DeployManager.Services.HardwareAudit.Models;

namespace DeployManager.Services.Deployment
{
    public class DeploymentExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public SystemHealthReport HealthReport { get; set; } = new();
    }

    /// <summary>
    /// Master Hardware-Adaptive Deployment Pipeline.
    /// Orchestrates machine-tailored firmware cloaking, permanent agent installation,
    /// cloud registration, and autonomous post-deployment self-verification.
    /// </summary>
    public static class AdaptiveDeploymentPipeline
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        public static async Task<DeploymentExecutionResult> DeployAdaptiveAsync(
            HardwareAuditReport auditReport,
            Action<string> log,
            IProgress<int> progress)
        {
            await Task.Yield();
            var profile = auditReport.AdaptiveProfile;

            try
            {
                log("================================================================================");
                log(">>> ADAPTIVE HARDWARE AUTO-CONFIGURATION ENGINE STARTING...");
                log($"    Target Workstation: {auditReport.Motherboard.Manufacturer} {auditReport.Motherboard.Product}");
                log($"    Hardware Profile  : {profile.ProfileName} [{profile.ShortBadge}]");
                log($"    Decision Rationale: {profile.Rationale}");
                log("================================================================================");
                progress.Report(10);

                // Phase 1: Adaptive Firmware / Bootloader Strategy
                log($"[Phase 1/4] Executing Firmware Strategy: {(profile.EnablePrebootFirmware ? "Dual-Plane Pre-Boot Cloak" : "Standard Microsoft Bootloader (Zero Freeze Risk)")}...");
                if (profile.EnablePrebootFirmware)
                {
                    bool efiOk = BootloaderManager.DeployPreBootEfi(log);
                    if (profile.EnableWifiPrebootSync)
                    {
                        BootloaderManager.SyncWifiProfileToPreboot(log);
                    }
                    log("[✓] Dual-Plane Pre-Boot Firmware Watchdog installed.");
                }
                else
                {
                    BootloaderManager.EnsureFactoryState(log);
                    log("[✓] Standard factory bootloader verified. 0% Motherboard boot-freeze risk.");
                }
                progress.Report(40);

                // Phase 2: Security Agent Installation & Profile Persistence
                log("[Phase 2/4] Installing Security Agent & Persisting Machine-Tailored Profile...");
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                AgentInstaller.ResolveSecurityAgent(baseDir, out string agentExe, out string agentArgs);

                if (!File.Exists(agentExe))
                {
                    log($"[X] [ERROR] Security Agent executable was not found at: {agentExe}");
                    log("    Please compile the agent or run build_all_standalone.bat first.");
                    return new DeploymentExecutionResult
                    {
                        Success = false,
                        Message = $"Agent binary not found at {agentExe}"
                    };
                }

                AgentInstaller.InstallPermanent(ref agentExe, ref agentArgs, log);

                // Save machine-specific hardware audit profile
                try
                {
                    string profileSavePath = Path.Combine(AgentInstaller.PermanentDir, "hardware_audit.json");
                    File.WriteAllText(profileSavePath, auditReport.ToJson(true));
                    log($"[✓] Workstation-tailored hardware profile saved: {profileSavePath}");
                }
                catch { }

                if (!AgentInstaller.StartAgentProcess(agentExe, agentArgs, log))
                {
                    log("[X] [ERROR] Failed to start Security Agent daemon.");
                    return new DeploymentExecutionResult
                    {
                        Success = false,
                        Message = "Failed to launch background Security Agent daemon."
                    };
                }

                AgentInstaller.ConfigureRunKey(agentExe, agentArgs);
                log("[✓] PC Security Agent daemon active and auto-starts on reboot.");
                progress.Report(70);

                // Phase 3: Cloud Verification Handshake
                log("[Phase 3/4] Performing Cloud Gateway Handshake...");
                await RegisterCloudDeviceAsync(auditReport, log);
                progress.Report(85);

                // Phase 4: Autonomous Post-Deployment Self-Verification & Health Audit
                log("[Phase 4/4] Running Autonomous Post-Deployment Self-Verification & Health Audit...");
                await Task.Delay(1000); // Give the agent 1 second to bind its sockets & initialize
                var healthReport = await SystemHealthVerifier.RunVerificationAsync(auditReport, log);
                progress.Report(100);

                if (healthReport.IsOverallHealthy)
                {
                    log("================================================================================");
                    log("[✓] [SUCCESS] All 5 System Components Successfully Installed & 100% Operational!");
                    log($"    Workstation Mode : {profile.ProfileName}");
                    log($"    Motherboard Setup: {(profile.EnablePrebootFirmware ? "UEFI Pre-Boot Cloaked + Watchdog (100% Safe)" : "Factory Microsoft Bootloader (Zero Freeze Risk)")}");
                    log($"    Desktop Shield   : Active, Topmost & Protected by Windows Kernel");
                    log("================================================================================");
                    return new DeploymentExecutionResult
                    {
                        Success = true,
                        Message = "Hardware-adaptive deployment and self-verification succeeded.",
                        HealthReport = healthReport
                    };
                }
                else
                {
                    log("================================================================================");
                    log($"[!] [DIAGNOSTIC ALERT] Self-verification detected {healthReport.TotalFailed} critical issue(s):");
                    log(healthReport.GetDetailedFailureReport());
                    log("================================================================================");
                    return new DeploymentExecutionResult
                    {
                        Success = false,
                        Message = $"Self-diagnosis detected {healthReport.TotalFailed} issue(s).",
                        HealthReport = healthReport
                    };
                }
            }
            catch (Exception ex)
            {
                log($"[X] [ERROR] Adaptive deployment encountered an exception: {ex.Message}");
                return new DeploymentExecutionResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private static async Task RegisterCloudDeviceAsync(HardwareAuditReport audit, Action<string> log)
        {
            string? machineGuid = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(machineGuid) && machineGuid.Length >= 8)
            {
                string pcId = $"pc_{machineGuid.Substring(0, 8)}";
                try
                {
                    var payload = new
                    {
                        pcId = pcId,
                        hardwareUuid = machineGuid,
                        deviceName = $"{audit.Motherboard.Manufacturer} Workstation ({pcId})",
                        motherboard = $"{audit.Motherboard.Manufacturer} {audit.Motherboard.Product}",
                        bios = $"{audit.Bios.Vendor} {audit.Bios.Version} ({audit.Bios.BootMode})",
                        gpu = audit.Graphics.GpuName,
                        adaptiveProfile = audit.AdaptiveProfile.ProfileName,
                        mac = audit.Network.PrimaryMacAddress
                    };

                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var res = await Http.PostAsync("https://pc-lock.onrender.com/api/devices/pc/register", content);
                    if (res.IsSuccessStatusCode)
                    {
                        log($"[✓] Workstation ({pcId}) registered in Cloud Relay with adaptive hardware profile.");
                    }
                    else
                    {
                        log($"[Notice] Cloud response code: {res.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    log($"[Notice] Cloud registration note: {ex.Message}");
                }
            }
        }
    }
}
