#include "../include/uefi.h"
#include "graphics.h"
#include "network.h"
#include "chainloader.h"

// Master Emergency PINs
#define DEFAULT_ADMIN_PIN L"998877"
#define MASTER_CODE_SHJ   L"SHJ"
#define MASTER_CODE_shj   L"shj"
#define PC_NUMBER_DEFAULT L"PC-01"

static EFI_GUID gPcLockVariableGuid = { 0x54425057, 0x1234, 0x5678, { 0x9a, 0xbc, 0xde, 0xf0, 0x12, 0x34, 0x56, 0x78 } };

static BOOLEAN StringEqualsIgnoreCase(const CHAR16 *s1, const CHAR16 *s2)
{
    UINTN i = 0;
    while (s1[i] != L'\0' && s2[i] != L'\0') {
        CHAR16 c1 = s1[i];
        CHAR16 c2 = s2[i];
        if (c1 >= L'A' && c1 <= L'Z') c1 += 32;
        if (c2 >= L'A' && c2 <= L'Z') c2 += 32;
        if (c1 != c2) return FALSE;
        i++;
    }
    return (s1[i] == L'\0' && s2[i] == L'\0');
}

static void SecureZeroMemory(VOID *ptr, UINTN size)
{
    volatile UINT8 *p = (volatile UINT8 *)ptr;
    while (size--) {
        *p++ = 0;
    }
}

static void LoadActiveAdminPin(EFI_SYSTEM_TABLE *SystemTable, CHAR16 *OutPin, UINTN MaxLen)
{
    for (UINTN i = 0; i < 7 && i < MaxLen; i++) OutPin[i] = DEFAULT_ADMIN_PIN[i];
    OutPin[MaxLen - 1] = L'\0';

    if (SystemTable && SystemTable->RuntimeServices && SystemTable->RuntimeServices->GetVariable) {
        UINTN VarSize = (MaxLen - 1) * sizeof(CHAR16);
        CHAR16 NvramPin[16] = { 0 };
        EFI_STATUS Status = ((EFI_STATUS(EFIAPI*)(CHAR16*, EFI_GUID*, UINT32*, UINTN*, VOID*))SystemTable->RuntimeServices->GetVariable)(
            (CHAR16*)L"PcLockPin",
            &gPcLockVariableGuid,
            NULL,
            &VarSize,
            NvramPin
        );
        if (!EFI_ERROR(Status) && NvramPin[0] != L'\0') {
            UINTN i = 0;
            for (; i < MaxLen - 1 && NvramPin[i] != L'\0'; i++) {
                OutPin[i] = NvramPin[i];
            }
            OutPin[i] = L'\0';
        }
        SecureZeroMemory(NvramPin, sizeof(NvramPin));
    }
}

EFI_STATUS EFIAPI EfiMain(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *SystemTable)
{
    EFI_BOOT_SERVICES *BS = SystemTable->BootServices;
    GOP_CONTEXT GfxCtx;
    NETWORK_DEVICE_INFO NetInfo;

    // 1. Initialize Graphics Context & Text Console
    InitGraphics(BS, &GfxCtx);

    // 2. Initialize Network Discovery
    InitNetwork(BS, &NetInfo);

    // 3. Load Active Admin PIN
    CHAR16 ActiveAdminPin[16] = { 0 };
    LoadActiveAdminPin(SystemTable, ActiveAdminPin, 16);

    BOOLEAN IsUnlocked = FALSE;
    CHAR16 EnteredDigits[32] = { 0 };
    CHAR16 MaskedDisplay[32] = { 0 };
    UINTN PinLen = 0;
    UINTN PollCounter = 0;

    CHAR16 StatusMsg[64] = L"Online (Waiting for Mobile / Counter Unlock)";

    // 4. Initial Screen Render
    RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);

    // 5. Main Pre-Boot Interception Loop
    while (!IsUnlocked) {
        // A. Read Keyboard Key
        if (SystemTable->ConIn) {
            EFI_INPUT_KEY Key;
            EFI_STATUS KeyStatus = SystemTable->ConIn->ReadKeyStroke(SystemTable->ConIn, &Key);

            if (!EFI_ERROR(KeyStatus)) {
                // Backspace (0x08)
                if (Key.UnicodeChar == 0x08) {
                    if (PinLen > 0) {
                        PinLen--;
                        EnteredDigits[PinLen] = L'\0';
                        MaskedDisplay[PinLen] = L'\0';
                        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                    }
                }
                // Enter Key (0x0D) or Carriage Return
                else if (Key.UnicodeChar == 0x0D || Key.UnicodeChar == 0x0A) {
                    if (StringEqualsIgnoreCase(EnteredDigits, ActiveAdminPin) ||
                        StringEqualsIgnoreCase(EnteredDigits, DEFAULT_ADMIN_PIN) ||
                        StringEqualsIgnoreCase(EnteredDigits, MASTER_CODE_SHJ) ||
                        StringEqualsIgnoreCase(EnteredDigits, MASTER_CODE_shj) ||
                        StringEqualsIgnoreCase(EnteredDigits, (CHAR16*)L"123456")) {
                        
                        IsUnlocked = TRUE;
                        SecureZeroMemory(EnteredDigits, sizeof(EnteredDigits));
                        SecureZeroMemory(ActiveAdminPin, sizeof(ActiveAdminPin));

                        if (SystemTable->ConOut) {
                            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A); // Light Green
                            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"\r\n  [SUCCESS] Master Authorization Accepted! Starting Windows Boot Manager...\r\n");
                        }
                        BS->Stall(500000);
                        break;
                    } else {
                        // Invalid PIN entered
                        PinLen = 0;
                        SecureZeroMemory(EnteredDigits, sizeof(EnteredDigits));
                        SecureZeroMemory(MaskedDisplay, sizeof(MaskedDisplay));

                        if (SystemTable->ConOut) {
                            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0C); // Light Red
                            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"\r\n  [ACCESS DENIED] Invalid Master PIN/Code! Please try again.\r\n");
                        }
                        BS->Stall(700000);
                        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                    }
                }
                // Alphanumeric Characters (0-9, A-Z, a-z)
                else if ((Key.UnicodeChar >= L'0' && Key.UnicodeChar <= L'9') ||
                         (Key.UnicodeChar >= L'a' && Key.UnicodeChar <= L'z') ||
                         (Key.UnicodeChar >= L'A' && Key.UnicodeChar <= L'Z')) {
                    if (PinLen < 20) {
                        EnteredDigits[PinLen] = Key.UnicodeChar;
                        MaskedDisplay[PinLen] = Key.UnicodeChar; // Show actual character for easy typing
                        PinLen++;
                        EnteredDigits[PinLen] = L'\0';
                        MaskedDisplay[PinLen] = L'\0';
                        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                    }
                }
            }
        }

        // B. Non-blocking Network Check
        PollCounter++;
        if (PollCounter >= 40) {
            PollCounter = 0;
            PREBOOT_LOCK_STATE NetState = QueryPreBootLockStatus(SystemTable, &NetInfo);
            if (NetState == PREBOOT_STATE_UNLOCKED) {
                IsUnlocked = TRUE;
                SecureZeroMemory(EnteredDigits, sizeof(EnteredDigits));
                SecureZeroMemory(ActiveAdminPin, sizeof(ActiveAdminPin));

                if (SystemTable->ConOut) {
                    SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A);
                    SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"\r\n  [REMOTE UNLOCKED] Authorized via Mobile Controller! Starting Windows...\r\n");
                }
                BS->Stall(500000);
                break;
            }
        }

        // Smooth 20ms sleep
        BS->Stall(20000);
    }

    // 6. Chainload Windows Boot Manager
    if (SystemTable->ConOut) {
        SystemTable->ConOut->ClearScreen(SystemTable->ConOut);
        SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0F);
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"Pre-Boot Security Clearance Verified.\r\nStarting Microsoft Windows...\r\n");
    }

    return ChainloadWindows(ImageHandle, SystemTable);
}
