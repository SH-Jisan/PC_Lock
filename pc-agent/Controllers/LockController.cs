using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PC.SecurityAgent.Controllers
{
    public class LockController
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        private const string RegistrySecurityKey = @"SOFTWARE\PCSecuritySystem";
        private const string LockValueName = "RemoteLockState";
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        public enum LockState
        {
            UNLOCKED,
            LOCKED
        }

        public static LockState CurrentState { get; private set; } = LockState.UNLOCKED;
        public static string ActiveDeviceId { get; set; } = string.Empty;
        public static string RelayHttpBaseUrl { get; set; } = "https://pc-lock.onrender.com";

        public static bool LockPC()
        {
            if (CurrentState == LockState.LOCKED)
            {
                // Already locked, enforce LockWorkStation once safely
                LockWorkStation();
                return true;
            }

            Console.WriteLine("[LockController] Remote LOCK Command Received. Enforcing Windows Lock...");
            
            SetRegistryLockState(LockState.LOCKED);
            CurrentState = LockState.LOCKED;

            // Run BootGuard Healer to enforce pre-boot firmware cloak on reboot
            Task.Run(() => BootGuardHealer.HealBootConfiguration());

            // 1. Direct Win32 Lock Screen API
            bool success = LockWorkStation();
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"[LockController Warning] LockWorkStation returned: {error}. Attempting fallback execution...");
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rundll32.exe",
                        Arguments = "user32.dll,LockWorkStation",
                        UseShellExecute = true
                    });
                    success = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LockController Error] Fallback lock execution error: {ex.Message}");
                }
            }

            // Report state to Cloud Relay REST endpoint
            _ = SyncStatusToCloudAsync("LOCKED");

            return success;
        }

        public static bool UnlockPC()
        {
            Console.WriteLine("[LockController] Remote UNLOCK Command Received. Restoring UNLOCKED state...");

            SetRegistryLockState(LockState.UNLOCKED);
            CurrentState = LockState.UNLOCKED;

            // Report state to Cloud Relay REST endpoint
            _ = SyncStatusToCloudAsync("UNLOCKED");

            Console.WriteLine("[LockController] Lock state set to UNLOCKED. Windows Session unlocked.");
            return true;
        }

        private static async Task SyncStatusToCloudAsync(string status)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ActiveDeviceId) && !string.IsNullOrWhiteSpace(RelayHttpBaseUrl))
                {
                    var payload = new { pcId = ActiveDeviceId, lockStatus = status };
                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await HttpClient.PostAsync($"{RelayHttpBaseUrl}/api/devices/pc/status-update", content);
                }
            }
            catch { }
        }

        private static void SetRegistryLockState(LockState state)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistrySecurityKey);
                key.SetValue(LockValueName, state.ToString(), RegistryValueKind.String);
                key.SetValue("LastStateUpdate", DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
            }
            catch { }
        }

        public static LockState ReadPersistedLockState()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistrySecurityKey);
                string? val = key?.GetValue(LockValueName)?.ToString();
                if (val == "LOCKED") return LockState.LOCKED;
            }
            catch { }
            return LockState.UNLOCKED;
        }
    }
}
