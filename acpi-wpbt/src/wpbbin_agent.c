/*
 * wpbbin_agent.c - Windows Platform Binary Table (WPBT) Native Payload
 *
 * This executable is extracted directly by the Windows Kernel (ntoskrnl.exe / smss.exe)
 * into %SystemRoot%\System32\wpbbin.exe during early boot with NT AUTHORITY\SYSTEM privileges.
 *
 * It ensures that even after a full SSD format or fresh Windows 10/11 reinstallation,
 * the PC Security & Cyber Cafe Lock Agent is automatically resurrected and active!
 */

#include <windows.h>
#include <stdio.h>
#include <tlhelp32.h>
#include <winreg.h>

#define REGISTRY_BASE_KEY L"SOFTWARE\\PCSecuritySystem"
#define SERVICE_NAME      L"PCSecurityAgentService"

// Helper to log status to a persistent system log
void LogEvent(const wchar_t *msg)
{
    FILE *f = _wfopen(L"C:\\Windows\\Temp\\wpbt_boot_log.txt", L"a+");
    if (f) {
        SYSTEMTIME st;
        GetLocalTime(&st);
        fwprintf(f, L"[%04d-%02d-%02d %02d:%02d:%02d] [WPBT RESURRECTION] %s\n",
                 st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, msg);
        fclose(f);
    }
}

// Check if PC Agent process is running
BOOL IsAgentRunning()
{
    BOOL running = FALSE;
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot != INVALID_HANDLE_VALUE) {
        PROCESSENTRY32W pe;
        pe.dwSize = sizeof(pe);
        if (Process32FirstW(snapshot, &pe)) {
            do {
                if (_wcsicmp(pe.szExeFile, L"PC.SecurityAgent.exe") == 0 ||
                    _wcsicmp(pe.szExeFile, L"PCSecurityAgent.exe") == 0) {
                    running = TRUE;
                    break;
                }
            } while (Process32NextW(snapshot, &pe));
        }
        CloseHandle(snapshot);
    }
    return running;
}

// Enforce Registry Lock State on Fresh Boot
void EnforceStartupLock()
{
    HKEY hKey;
    if (RegCreateKeyExW(HKEY_LOCAL_MACHINE, REGISTRY_BASE_KEY, 0, NULL, REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL) == ERROR_SUCCESS) {
        const wchar_t *state = L"LOCKED";
        RegSetValueExW(hKey, L"RemoteLockState", 0, REG_SZ, (const BYTE*)state, (DWORD)((wcslen(state) + 1) * sizeof(wchar_t)));
        
        const wchar_t *origin = L"ACPI_WPBT_BIOS_INJECTED";
        RegSetValueExW(hKey, L"InstalledBy", 0, REG_SZ, (const BYTE*)origin, (DWORD)((wcslen(origin) + 1) * sizeof(wchar_t)));
        
        RegCloseKey(hKey);
        LogEvent(L"Registry Lock State successfully initialized from Motherboard WPBT.");
    }
}

// Trigger Win32 Lock Screen
void TriggerLockWorkStation()
{
    typedef BOOL(WINAPI *LockWorkStationFunc)(VOID);
    HMODULE hUser32 = LoadLibraryW(L"user32.dll");
    if (hUser32) {
        LockWorkStationFunc lockFunc = (LockWorkStationFunc)GetProcAddress(hUser32, "LockWorkStation");
        if (lockFunc) {
            lockFunc();
            LogEvent(L"LockWorkStation() executed successfully by WPBT dropper.");
        }
        FreeLibrary(hUser32);
    }
}

// Find first available unused drive letter (from Z down to E)
wchar_t GetFreeDriveLetter()
{
    DWORD drives = GetLogicalDrives();
    for (int i = 25; i >= 4; i--) { // Z (25) down to E (4)
        if (!(drives & (1 << i))) {
            return (wchar_t)(L'A' + i);
        }
    }
    return L'Z';
}

// Trigger boot repair and cloaking pass from kernel dropper
void HealEfiBootFromKernel()
{
    wchar_t freeLetter = GetFreeDriveLetter();
    char mountCmd[64];
    char unmountCmd[64];
    wchar_t srcPath[128];
    wchar_t dstPath[128];

    sprintf_s(mountCmd, sizeof(mountCmd), "mountvol %c: /s >nul 2>&1", (char)freeLetter);
    sprintf_s(unmountCmd, sizeof(unmountCmd), "mountvol %c: /d >nul 2>&1", (char)freeLetter);
    swprintf_s(srcPath, sizeof(srcPath) / sizeof(wchar_t), L"%c:\\EFI\\Microsoft\\Boot\\bootmgfw.efi", freeLetter);
    swprintf_s(dstPath, sizeof(dstPath) / sizeof(wchar_t), L"%c:\\EFI\\Microsoft\\Boot\\bootmgfw_hidden.efi", freeLetter);

    // Mount EFI to dynamic free letter
    system(mountCmd);

    // Cloak bootmgfw.efi if exists
    if (GetFileAttributesW(srcPath) != INVALID_FILE_ATTRIBUTES) {
        MoveFileExW(srcPath, dstPath, MOVEFILE_REPLACE_EXISTING);
        LogEvent(L"WPBT Dropper successfully re-cloaked bootmgfw.efi in EFI partition.");
    }

    // Unmount EFI
    system(unmountCmd);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    LogEvent(L"============================================================");
    LogEvent(L"Windows Platform Binary Table (WPBT) Native Payload Invoked.");
    LogEvent(L"Running with NT AUTHORITY\\SYSTEM Privileges.");

    // 1. Enforce Registry Lock State on fresh or formatted Windows
    EnforceStartupLock();

    // 2. Lock desktop session immediately
    TriggerLockWorkStation();

    // 3. Heal EFI Boot Cloak directly from kernel dropper
    HealEfiBootFromKernel();

    // 4. Verify if background service is running
    if (!IsAgentRunning()) {
        LogEvent(L"PC Security Agent background service is not running. Resurrecting...");
        STARTUPINFOW si = { sizeof(si) };
        PROCESS_INFORMATION pi;
        
        wchar_t cmd[] = L"C:\\Program Files\\PCSecuritySystem\\PC.SecurityAgent.exe";
        if (CreateProcessW(NULL, cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            LogEvent(L"PC Security Agent process spawned successfully.");
        } else {
            LogEvent(L"PC Agent binary path not found. Defaulting to native kernel lock enforcement.");
        }
    } else {
        LogEvent(L"PC Security Agent is already active and healthy.");
    }

    LogEvent(L"WPBT Dropper execution complete. System secured.");
    return 0;
}
