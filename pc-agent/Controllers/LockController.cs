using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PC.SecurityAgent.Controllers
{
    public class LockController
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        private const string RegistrySecurityKey = @"SOFTWARE\PCSecuritySystem";
        private const string LockValueName = "RemoteLockState";

        public enum LockState
        {
            UNLOCKED,
            LOCKED
        }

        public static LockState CurrentState { get; private set; } = LockState.UNLOCKED;

        public static bool LockPC()
        {
            Console.WriteLine("[LockController] Remote LOCK Command Received. Executing Windows Lock...");
            
            // Set registry persistence state so Winlogon credential provider & pre-boot knows PC is locked
            SetRegistryLockState(LockState.LOCKED);
            CurrentState = LockState.LOCKED;

            // Run BootGuard Healer to enforce pre-boot firmware lock on reboot
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
            else
            {
                Console.WriteLine("[LockController] Windows Session Locked Successfully.");
            }

            return success;
        }

        public static bool UnlockPC()
        {
            Console.WriteLine("[LockController] Remote UNLOCK Command Received. Clearing Winlogon lock state...");

            SetRegistryLockState(LockState.UNLOCKED);
            CurrentState = LockState.UNLOCKED;

            Console.WriteLine("[LockController] Lock state set to UNLOCKED. Winlogon Credential Provider unblocked.");
            return true;
        }

        private static void SetRegistryLockState(LockState state)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistrySecurityKey);
                key.SetValue(LockValueName, state.ToString(), RegistryValueKind.String);
                key.SetValue("LastStateUpdate", DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LockController Warning] Failed to update lock state registry: {ex.Message}");
            }
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
