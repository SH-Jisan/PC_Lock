using System;
using Microsoft.Win32;

namespace PC.SecurityAgent.LockEngine
{
    public static class TaskManagerPolicy
    {
        private const string PolicySubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string PolicyValue = "DisableTaskMgr";

        public static void DisableTaskManager()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(PolicySubKey);
                key?.SetValue(PolicyValue, 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void EnableTaskManager()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PolicySubKey, true);
                key?.DeleteValue(PolicyValue, false);
            }
            catch { }
        }
    }
}
