using System;
using Microsoft.Win32;

namespace PC.SecurityAgent.LockEngine
{
    public static class TaskManagerPolicy
    {
        private const string SystemPolicySubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string ExplorerPolicySubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";

        public static void DisableTaskManager()
        {
            try
            {
                using var sysKey = Registry.CurrentUser.CreateSubKey(SystemPolicySubKey);
                sysKey?.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                sysKey?.SetValue("DisableChangePassword", 1, RegistryValueKind.DWord);
                sysKey?.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);

                using var expKey = Registry.CurrentUser.CreateSubKey(ExplorerPolicySubKey);
                expKey?.SetValue("NoLogoff", 1, RegistryValueKind.DWord);
                expKey?.SetValue("NoClose", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void EnableTaskManager()
        {
            try
            {
                using var sysKey = Registry.CurrentUser.OpenSubKey(SystemPolicySubKey, true);
                sysKey?.DeleteValue("DisableTaskMgr", false);
                sysKey?.DeleteValue("DisableChangePassword", false);
                sysKey?.DeleteValue("DisableLockWorkstation", false);

                using var expKey = Registry.CurrentUser.OpenSubKey(ExplorerPolicySubKey, true);
                expKey?.DeleteValue("NoLogoff", false);
                expKey?.DeleteValue("NoClose", false);
            }
            catch { }
        }
    }
}
