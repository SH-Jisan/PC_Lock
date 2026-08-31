#include "chainloader.h"

static EFI_GUID gSimpleFileSystemProtocolGuid = EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID;
static EFI_GUID gLoadedImageProtocolGuid = EFI_LOADED_IMAGE_PROTOCOL_GUID;
static EFI_GUID gDevicePathProtocolGuid = EFI_DEVICE_PATH_PROTOCOL_GUID;

static BOOLEAN CheckFileExists(EFI_FILE_PROTOCOL *Root, CHAR16 *Path)
{
    EFI_FILE_PROTOCOL *File = NULL;
    EFI_STATUS Status = Root->Open(Root, &File, Path, EFI_FILE_MODE_READ, 0);
    if (!EFI_ERROR(Status) && File != NULL) {
        File->Close(File);
        return TRUE;
    }
    return FALSE;
}

EFI_STATUS ChainloadWindows(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *ST)
{
    EFI_BOOT_SERVICES *BS = ST->BootServices;
    EFI_STATUS Status;
    UINTN HandleCount = 0;
    EFI_HANDLE *HandleBuffer = NULL;

    if (ST->ConOut) {
        ST->ConOut->SetAttribute(ST->ConOut, 0x0A); // Green
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"\r\n[PRE-BOOT] PC UNLOCKED! Locating Windows Boot Manager...\r\n");
    }

    // 1. Locate all File System Handles (partitions)
    Status = BS->LocateHandleBuffer(
        2, // ByProtocol
        &gSimpleFileSystemProtocolGuid,
        NULL,
        &HandleCount,
        &HandleBuffer
    );

    if (EFI_ERROR(Status) || HandleCount == 0) {
        if (ST->ConOut) {
            ST->ConOut->SetAttribute(ST->ConOut, 0x0C); // Red
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[ERROR] No EFI System Partitions found!\r\n");
        }
        return Status;
    }

    EFI_HANDLE TargetHandle = NULL;
    CHAR16 *SelectedBootPath = WINDOWS_BOOTMGR_PATH;

    // 2. Iterate through partitions to find Windows Boot Manager
    for (UINTN i = 0; i < HandleCount; i++) {
        EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *Fs = NULL;
        Status = BS->HandleProtocol(HandleBuffer[i], &gSimpleFileSystemProtocolGuid, (VOID**)&Fs);
        if (EFI_ERROR(Status) || !Fs) continue;

        EFI_FILE_PROTOCOL *Root = NULL;
        Status = Fs->OpenVolume(Fs, &Root);
        if (EFI_ERROR(Status) || !Root) continue;

        if (CheckFileExists(Root, WINDOWS_CLOAKED_BOOTMGR_PATH)) {
            TargetHandle = HandleBuffer[i];
            SelectedBootPath = WINDOWS_CLOAKED_BOOTMGR_PATH;
            Root->Close(Root);
            break;
        } else if (CheckFileExists(Root, WINDOWS_BOOTMGR_PATH)) {
            TargetHandle = HandleBuffer[i];
            SelectedBootPath = WINDOWS_BOOTMGR_PATH;
            Root->Close(Root);
            break;
        } else {
            Root->Close(Root);
        }
    }



    if (TargetHandle == NULL) {
        if (ST->ConOut) {
            ST->ConOut->SetAttribute(ST->ConOut, 0x0C);
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[ERROR] bootmgfw.efi was not found on any EFI partition.\r\n");
        }
        if (HandleBuffer) BS->FreePool(HandleBuffer);
        return EFI_NOT_FOUND;
    }

    if (ST->ConOut) {
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[PRE-BOOT] Starting Windows OS...\r\n");
    }

    // 3. Load the Windows Boot Manager Image into memory
    EFI_HANDLE WindowsImageHandle = NULL;
    
    // Read the bootmgfw.efi file into memory buffer
    EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *Fs = NULL;
    BS->HandleProtocol(TargetHandle, &gSimpleFileSystemProtocolGuid, (VOID**)&Fs);
    EFI_FILE_PROTOCOL *Root = NULL;
    Fs->OpenVolume(Fs, &Root);
    
    EFI_FILE_PROTOCOL *BootFile = NULL;
    Status = Root->Open(Root, &BootFile, SelectedBootPath, EFI_FILE_MODE_READ, 0);
    if (EFI_ERROR(Status)) {
        Root->Close(Root);
        if (HandleBuffer) BS->FreePool(HandleBuffer);
        return Status;
    }

    // Get file size
    UINT8 InfoBuf[128];
    UINTN InfoSize = sizeof(InfoBuf);
    EFI_GUID FileInfoGuid = { 0x09576e92, 0x6d3f, 0x11d2, { 0x8e, 0x39, 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b } };
    
    // Allocate buffer for bootloader file
    UINTN FileSize = 4 * 1024 * 1024; // 4MB buffer is sufficient for bootmgfw.efi
    VOID *FileBuffer = NULL;
    Status = BS->AllocatePool(EfiLoaderData, FileSize, &FileBuffer);
    if (EFI_ERROR(Status) || FileBuffer == NULL) {
        BootFile->Close(BootFile);
        Root->Close(Root);
        if (HandleBuffer) BS->FreePool(HandleBuffer);
        return Status;
    }

    Status = BootFile->Read(BootFile, &FileSize, FileBuffer);
    BootFile->Close(BootFile);
    Root->Close(Root);

    if (EFI_ERROR(Status)) {
        BS->FreePool(FileBuffer);
        if (HandleBuffer) BS->FreePool(HandleBuffer);
        return Status;
    }


    // Load Image from memory buffer
    Status = BS->LoadImage(
        FALSE,
        ImageHandle,
        NULL,
        FileBuffer,
        FileSize,
        &WindowsImageHandle
    );

    if (HandleBuffer) BS->FreePool(HandleBuffer);

    if (EFI_ERROR(Status)) {
        if (ST->ConOut) {
            ST->ConOut->SetAttribute(ST->ConOut, 0x0C);
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[ERROR] BS->LoadImage() failed for Windows Boot Manager!\r\n");
        }
        BS->FreePool(FileBuffer);
        return Status;
    }

    // 4. Start Windows Boot Manager
    UINTN ExitDataSize = 0;
    CHAR16 *ExitData = NULL;
    Status = BS->StartImage(WindowsImageHandle, &ExitDataSize, &ExitData);

    BS->FreePool(FileBuffer);
    return Status;
}
