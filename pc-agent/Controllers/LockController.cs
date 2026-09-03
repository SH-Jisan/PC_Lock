using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using PC.SecurityAgent.LockEngine;

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

        static LockController()
        {
            // Register callback so on-screen PIN unlock propagates to cloud and controller
            LockEngineCoordinator.RegisterExternalUnlockCallback(() =>
            {
                Console.WriteLine("[LockController] On-screen PIN authentication passed. Notifying cloud...");
                UnlockPC(true);
            });
        }

        public static bool LockPC(bool notifyCloud = false)
        {
            if (CurrentState == LockState.LOCKED)
            {
                // Ensure lock screen is active even if duplicate lock arrives
                LockEngineCoordinator.ShowLockScreen();
                return true;
            }

            Console.WriteLine("[LockController] Remote LOCK Command Received. Enforcing Hybrid Dual-Plane Lock...");
            
            SetRegistryLockState(LockState.LOCKED);
            CurrentState = LockState.LOCKED;

            // Epoch 1: Pre-Boot Plane - Enforce firmware pre-boot cloak so reboot intercepts before Windows
            Task.Run(() => BootGuardHealer.HealBootConfiguration());

            // Epoch 2: Post-Boot Plane - Activate Isolated Desktop + Low-Level Keyboard Shield + Cyber UI
            LockEngineCoordinator.ShowLockScreen();

            if (notifyCloud)
            {
                _ = SyncStatusToCloudAsync("LOCKED");
            }

            return true;
        }

        public static bool UnlockPC(bool notifyCloud = false)
        {
            if (CurrentState == LockState.UNLOCKED)
            {
                LockEngineCoordinator.HideLockScreen();
                return true;
            }

            Console.WriteLine("[LockController] Remote UNLOCK Command Received. Deactivating Lock Engine...");

            SetRegistryLockState(LockState.UNLOCKED);
            CurrentState = LockState.UNLOCKED;

            // Epoch 2: Post-Boot Plane - Restore original desktop and remove keyboard hooks
            LockEngineCoordinator.HideLockScreen();

            if (notifyCloud)
            {
                _ = SyncStatusToCloudAsync("UNLOCKED");
            }

            Console.WriteLine("[LockController] Windows Session restored to normal state.");
            return true;
        }

        public static async Task SyncStatusToCloudAsync(string status)
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
