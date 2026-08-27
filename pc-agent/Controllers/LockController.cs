using System;
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
            Console.WriteLine("[LockController] Remote LOCK Command Received. Executing Win32 LockWorkStation()...");
            
            // Set registry persistence state so Winlogon credential provider knows PC is locked remotely
            SetRegistryLockState(LockState.LOCKED);
            CurrentState = LockState.LOCKED;

            // Trigger Win32 Lock Screen
            bool success = LockWorkStation();
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"[LockController Error] LockWorkStation failed with Win32 Error Code: {error}");
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
