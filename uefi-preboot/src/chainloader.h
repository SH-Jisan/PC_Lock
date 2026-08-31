#ifndef _CHAINLOADER_H_
#define _CHAINLOADER_H_

#include "../include/uefi.h"

// Path to Cloaked / Hardened Windows Boot Manager (Bypasses BIOS F12 discovery)
#define WINDOWS_CLOAKED_BOOTMGR_PATH L"\\EFI\\Microsoft\\Boot\\bootmgfw_hidden.efi"
#define WINDOWS_BOOTMGR_PATH         L"\\EFI\\Microsoft\\Boot\\bootmgfw.efi"

// Locates the EFI System Partition and chainloads Windows Boot Manager
EFI_STATUS ChainloadWindows(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *ST);

#endif // _CHAINLOADER_H_


