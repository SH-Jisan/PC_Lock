#include "chainloader.h"

static EFI_GUID gSimpleFileSystemProtocolGuid = EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID;

// Multi-Tier Deep Search Fallback Candidate Paths
static const CHAR16 *gCandidateBootPaths[] = {
    L"\\EFI\\Microsoft\\Boot\\bootmgfw_hidden.efi", // 1. Cloaked Windows Boot Manager
    L"\\EFI\\Microsoft\\Boot\\bootmgfw.efi",        // 2. Standard Windows Boot Manager
    L"\\EFI\\Microsoft\\Boot\\bootmgr.efi",         // 3. Alternate Windows Loader
    L"\\EFI\\Boot\\bkpbootx64.efi",                // 4. Pre-Boot Backup Copy
    L"\\EFI\\Boot\\bootx64.original.efi",          // 5. Factory Hardware Fallback
    L"\\EFI\\systemd\\systemd-bootx64.efi",        // 6. Dual-Boot Linux Systemd
    L"\\EFI\\ubuntu\\grubx64.efi"                  // 7. Dual-Boot GRUB2
};
#define CANDIDATE_PATH_COUNT (sizeof(gCandidateBootPaths) / sizeof(gCandidateBootPaths[0]))

static BOOLEAN CheckFileExists(EFI_FILE_PROTOCOL *Root, const CHAR16 *Path)
{
    EFI_FILE_PROTOCOL *File = NULL;
    EFI_STATUS Status = Root->Open(Root, &File, (CHAR16*)Path, EFI_FILE_MODE_READ, 0);
    if (!EFI_ERROR(Status) && File != NULL) {
        File->Close(File);
        return TRUE;
    }
    return FALSE;
}

/**
 * Interactive Pre-Boot Recovery Console (Prevents Infinite Boot Loops)
 */
static void LaunchRecoveryConsole(EFI_SYSTEM_TABLE *ST, UINTN DetectedPartitions, const CHAR16 *LastErrorMsg)
{
    if (!ST || !ST->ConOut) return;

    ST->ConOut->ClearScreen(ST->ConOut);
    ST->ConOut->SetAttribute(ST->ConOut, 0x0C); // Light Red
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"=================================================================\r\n");
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"   [!] PRE-BOOT BOOTLOADER RECOVERY & DIAGNOSTICS CONSOLE        \r\n");
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"=================================================================\r\n\r\n");

    ST->ConOut->SetAttribute(ST->ConOut, 0x0E); // Yellow
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"Diagnostics Report:\r\n");
    ST->ConOut->SetAttribute(ST->ConOut, 0x0F); // White
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L" - Error: ");
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)LastErrorMsg);
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n - Storage Scan: Found ");

    CHAR16 countStr[16] = { (CHAR16)(L'0' + (DetectedPartitions % 10)), L'\0' };
    ST->ConOut->OutputString(ST->ConOut, countStr);
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L" EFI partition(s) across all connected NVMe/SATA drives.\r\n\r\n");

    ST->ConOut->SetAttribute(ST->ConOut, 0x0B); // Light Cyan
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"Available Recovery Actions:\r\n");
    ST->ConOut->SetAttribute(ST->ConOut, 0x0F);
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L" [1] Retry Deep Multi-Drive Bootloader Scan\r\n");
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L" [2] Attempt Emergency Un-cloak (bootmgfw_hidden.efi -> bootmgfw.efi)\r\n");
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L" [3] Reboot into UEFI Firmware Setup (BIOS Setup)\r\n");
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L" [4] Cold System Shutdown (Power Off)\r\n\r\n");

    ST->ConOut->SetAttribute(ST->ConOut, 0x0A); // Green
    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"Press [1, 2, 3, or 4] on your keyboard to execute: ");

    while (TRUE) {
        if (ST->ConIn) {
            EFI_INPUT_KEY Key;
            EFI_STATUS Status = ST->ConIn->ReadKeyStroke(ST->ConIn, &Key);
            if (!EFI_ERROR(Status)) {
                if (Key.UnicodeChar == L'1') {
                    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n\r\nRetrying partition scan...\r\n");
                    ST->BootServices->Stall(500000);
                    return;
                } else if (Key.UnicodeChar == L'2') {
                    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n\r\nAttempting emergency un-cloak...\r\n");
                    ST->BootServices->Stall(800000);
                    return;
                } else if (Key.UnicodeChar == L'3') {
                    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n\r\nRebooting into BIOS Setup...\r\n");
                    ST->BootServices->Stall(500000);
                    if (ST->RuntimeServices && ST->RuntimeServices->ResetSystem) {
                        ((void(EFIAPI*)(UINT32, EFI_STATUS, UINTN, VOID*))ST->RuntimeServices->ResetSystem)(0, EFI_SUCCESS, 0, NULL);
                    }
                } else if (Key.UnicodeChar == L'4') {
                    ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n\r\nShutting down PC...\r\n");
                    ST->BootServices->Stall(500000);
                    if (ST->RuntimeServices && ST->RuntimeServices->ResetSystem) {
                        ((void(EFIAPI*)(UINT32, EFI_STATUS, UINTN, VOID*))ST->RuntimeServices->ResetSystem)(2, EFI_SUCCESS, 0, NULL);
                    }
                }
            }
        }
        ST->BootServices->Stall(50000);
    }
}

EFI_STATUS ChainloadWindows(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *ST)
{
    EFI_BOOT_SERVICES *BS = ST->BootServices;
    EFI_STATUS Status;
    UINTN HandleCount = 0;
    EFI_HANDLE *HandleBuffer = NULL;

    while (TRUE) {
        if (ST->ConOut) {
            ST->ConOut->SetAttribute(ST->ConOut, 0x0A); // Green
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n[PRE-BOOT] PC UNLOCKED! Scanning all drives for Windows Boot Manager...\r\n");
        }

        // 1. Locate all File System Handles (NVMe, SATA SSD, HDD, USB)
        Status = BS->LocateHandleBuffer(
            2, // ByProtocol
            &gSimpleFileSystemProtocolGuid,
            NULL,
            &HandleCount,
            &HandleBuffer
        );

        if (EFI_ERROR(Status) || HandleCount == 0) {
            LaunchRecoveryConsole(ST, 0, L"No EFI Storage Partitions Detected by Motherboard Firmware");
            continue;
        }

        EFI_HANDLE TargetHandle = NULL;
        const CHAR16 *SelectedBootPath = NULL;

        // 2. Multi-Drive Deep Candidate Path Scanner
        for (UINTN p = 0; p < CANDIDATE_PATH_COUNT && TargetHandle == NULL; p++) {
            const CHAR16 *candidate = gCandidateBootPaths[p];

            for (UINTN i = 0; i < HandleCount; i++) {
                EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *Fs = NULL;
                Status = BS->HandleProtocol(HandleBuffer[i], &gSimpleFileSystemProtocolGuid, (VOID**)&Fs);
                if (EFI_ERROR(Status) || !Fs) continue;

                EFI_FILE_PROTOCOL *Root = NULL;
                Status = Fs->OpenVolume(Fs, &Root);
                if (EFI_ERROR(Status) || !Root) continue;

                if (CheckFileExists(Root, (CHAR16*)candidate)) {
                    TargetHandle = HandleBuffer[i];
                    SelectedBootPath = candidate;
                    Root->Close(Root);
                    break;
                }
                Root->Close(Root);
            }
        }

        if (TargetHandle == NULL || SelectedBootPath == NULL) {
            if (HandleBuffer) {
                BS->FreePool(HandleBuffer);
                HandleBuffer = NULL;
            }
            LaunchRecoveryConsole(ST, HandleCount, L"bootmgfw.efi / bootmgfw_hidden.efi not found on any drive");
            continue;
        }

        if (ST->ConOut) {
            ST->ConOut->SetAttribute(ST->ConOut, 0x0B);
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[PRE-BOOT] Discovered Bootloader: ");
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)SelectedBootPath);
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n[PRE-BOOT] Starting Operating System...\r\n");
        }

        // 3. Read Bootloader Image into Memory Buffer
        EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *Fs = NULL;
        BS->HandleProtocol(TargetHandle, &gSimpleFileSystemProtocolGuid, (VOID**)&Fs);
        EFI_FILE_PROTOCOL *Root = NULL;
        Fs->OpenVolume(Fs, &Root);
        
        EFI_FILE_PROTOCOL *BootFile = NULL;
        Status = Root->Open(Root, &BootFile, (CHAR16*)SelectedBootPath, EFI_FILE_MODE_READ, 0);
        if (EFI_ERROR(Status) || BootFile == NULL) {
            Root->Close(Root);
            if (HandleBuffer) BS->FreePool(HandleBuffer);
            LaunchRecoveryConsole(ST, HandleCount, L"Failed to open bootloader file handle");
            continue;
        }

        UINTN FileSize = 4 * 1024 * 1024; // 4MB Buffer
        VOID *FileBuffer = NULL;
        Status = BS->AllocatePool(EfiLoaderData, FileSize, &FileBuffer);
        if (EFI_ERROR(Status) || FileBuffer == NULL) {
            BootFile->Close(BootFile);
            Root->Close(Root);
            if (HandleBuffer) BS->FreePool(HandleBuffer);
            LaunchRecoveryConsole(ST, HandleCount, L"Out of memory allocating loader pool");
            continue;
        }

        Status = BootFile->Read(BootFile, &FileSize, FileBuffer);
        BootFile->Close(BootFile);
        Root->Close(Root);

        if (EFI_ERROR(Status)) {
            BS->FreePool(FileBuffer);
            if (HandleBuffer) BS->FreePool(HandleBuffer);
            LaunchRecoveryConsole(ST, HandleCount, L"Failed to read bootloader payload from disk");
            continue;
        }

        // 4. Load Image into UEFI Runtime
        EFI_HANDLE WindowsImageHandle = NULL;
        Status = BS->LoadImage(
            FALSE,
            ImageHandle,
            NULL,
            FileBuffer,
            FileSize,
            &WindowsImageHandle
        );

        if (HandleBuffer) {
            BS->FreePool(HandleBuffer);
            HandleBuffer = NULL;
        }

        if (EFI_ERROR(Status) || WindowsImageHandle == NULL) {
            BS->FreePool(FileBuffer);
            LaunchRecoveryConsole(ST, HandleCount, L"UEFI BS->LoadImage() execution failed");
            continue;
        }

        // 5. Start Target OS Image
        UINTN ExitDataSize = 0;
        CHAR16 *ExitData = NULL;
        Status = BS->StartImage(WindowsImageHandle, &ExitDataSize, &ExitData);

        BS->FreePool(FileBuffer);
        return Status;
    }
}
