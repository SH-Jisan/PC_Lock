using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PC.SecurityAgent.LockEngine
{
    public static class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE = 0x20;
        private const int VK_F4 = 0x73;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt key

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc? _proc;
        private static IntPtr _hookId = IntPtr.Zero;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static void InstallHook()
        {
            if (_hookId != IntPtr.Zero) return;

            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            string moduleName = curModule?.ModuleName ?? string.Empty;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(moduleName), 0);
            Console.WriteLine("[KeyboardHook] Low-level keyboard shield installed.");
        }

        public static void UninstallHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _proc = null;
                Console.WriteLine("[KeyboardHook] Low-level keyboard shield removed.");
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    bool isAlt = (kbd.flags & 0x20) != 0 || (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                    bool isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                    // 1. Block Left and Right Windows Keys
                    if (kbd.vkCode == VK_LWIN || kbd.vkCode == VK_RWIN)
                    {
                        return (IntPtr)1;
                    }

                    // 2. Block Alt + Tab
                    if (isAlt && kbd.vkCode == VK_TAB)
                    {
                        return (IntPtr)1;
                    }

                    // 3. Block Alt + Esc
                    if (isAlt && kbd.vkCode == VK_ESCAPE)
                    {
                        return (IntPtr)1;
                    }

                    // 4. Block Ctrl + Esc (Start Menu)
                    if (isCtrl && kbd.vkCode == VK_ESCAPE)
                    {
                        return (IntPtr)1;
                    }

                    // 5. Block Alt + F4 (Close Application)
                    if (isAlt && kbd.vkCode == VK_F4)
                    {
                        return (IntPtr)1;
                    }

                    // 6. Block Alt + Space (System Window Menu)
                    if (isAlt && kbd.vkCode == VK_SPACE)
                    {
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
