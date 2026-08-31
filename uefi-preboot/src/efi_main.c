#include "../include/uefi.h"
#include "graphics.h"
#include "network.h"
#include "chainloader.h"

// Default Fallback PIN if NVRAM or server is not initialized
#define DEFAULT_ADMIN_PIN L"998877"
#define PC_NUMBER_DEFAULT L"PC-01"

static EFI_GUID gPcLockVariableGuid = { 0x54425057, 0x1234, 0x5678, { 0x9a, 0xbc, 0xde, 0xf0, 0x12, 0x34, 0x56, 0x78 } };

static BOOLEAN StringEquals(const CHAR16 *s1, const CHAR16 *s2)
{
    while (*s1 && *s2) {
        if (*s1 != *s2) return FALSE;
        s1++;
        s2++;
    }
    return (*s1 == *s2);
}

static void LoadActiveAdminPin(EFI_SYSTEM_TABLE *SystemTable, CHAR16 *OutPin, UINTN MaxLen)
{
    // Default
    for (UINTN i = 0; i < 7; i++) OutPin[i] = DEFAULT_ADMIN_PIN[i];

    // Try reading dynamic PIN from UEFI NVRAM variable "PcLockPin"
    if (SystemTable && SystemTable->RuntimeServices && SystemTable->RuntimeServices->GetVariable) {
        UINTN VarSize = MaxLen * sizeof(CHAR16);
        CHAR16 NvramPin[16] = { 0 };
        EFI_STATUS Status = ((EFI_STATUS(EFIAPI*)(CHAR16*, EFI_GUID*, UINT32*, UINTN*, VOID*))SystemTable->RuntimeServices->GetVariable)(
            (CHAR16*)L"PcLockPin",
            &gPcLockVariableGuid,
            NULL,
            &VarSize,
            NvramPin
        );
        if (!EFI_ERROR(Status) && NvramPin[0] != L'\0') {
            for (UINTN i = 0; i < MaxLen && NvramPin[i] != L'\0'; i++) {
                OutPin[i] = NvramPin[i];
                OutPin[i+1] = L'\0';
            }
        }
    }
}

EFI_STATUS EFIAPI EfiMain(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *SystemTable)
{
    EFI_BOOT_SERVICES *BS = SystemTable->BootServices;
    GOP_CONTEXT GfxCtx;
    NETWORK_DEVICE_INFO NetInfo;

    // 1. Reset Text Console & Hide standard cursor
    if (SystemTable->ConOut) {
        SystemTable->ConOut->ClearScreen(SystemTable->ConOut);
        SystemTable->ConOut->EnableCursor(SystemTable->ConOut, FALSE);
    }

    // 2. Initialize GOP (Graphics Output Protocol)
    EFI_STATUS GfxStatus = InitGraphics(BS, &GfxCtx);

    // 3. Initialize Network & MAC address
    InitNetwork(BS, &NetInfo);

    // 4. Load Active Admin PIN (Configurable per-PC)
    CHAR16 ActiveAdminPin[16] = { 0 };
    LoadActiveAdminPin(SystemTable, ActiveAdminPin, 16);

    // 5. State variables
    BOOLEAN IsUnlocked = FALSE;
    CHAR16 EnteredDigits[16] = { 0 };
    CHAR16 MaskedDisplay[16] = { 0 };
    UINTN PinLen = 0;
    UINTN PollCounter = 0;

    // Secret Key Sequence Tracking for "Ctrl+Shift+S+H+J" combination
    UINTN SecretSequenceStep = 0;

    CHAR16 StatusMsg[64] = L"Connecting to Cyber Cafe Server...";

    // 6. Initial Lock Screen Render
    if (!EFI_ERROR(GfxStatus)) {
        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
    }

    // 7. Pre-Boot Event Loop
    while (!IsUnlocked) {
        // A. Check for Keyboard Input
        if (SystemTable->ConIn) {
            EFI_INPUT_KEY Key;
            EFI_STATUS KeyStatus = SystemTable->ConIn->ReadKeyStroke(SystemTable->ConIn, &Key);

            if (!EFI_ERROR(KeyStatus)) {
                
                // --- SECRET COMBINATION: "Ctrl + Shift + S + H + J" / Sequence S -> H -> J ---
                if ((Key.UnicodeChar == 0x13 || Key.UnicodeChar == L's' || Key.UnicodeChar == L'S') && SecretSequenceStep == 0) {
                    SecretSequenceStep = 1;
                } else if ((Key.UnicodeChar == L'h' || Key.UnicodeChar == L'H') && SecretSequenceStep == 1) {
                    SecretSequenceStep = 2;
                } else if ((Key.UnicodeChar == L'j' || Key.UnicodeChar == L'J') && SecretSequenceStep == 2) {
                    // Secret Combination "Ctrl+Shift+S+H+J" Activated!
                    IsUnlocked = TRUE;
                    if (SystemTable->ConOut) {
                        SystemTable->ConOut->SetCursorPosition(SystemTable->ConOut, 20, 21);
                        SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A);
                        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[SECRET COMBO APPROVED] Windows Unlocking...");
                    }
                    BS->Stall(800000);
                    break;
                } else if (Key.UnicodeChar >= L'0' && Key.UnicodeChar <= L'9') {
                    SecretSequenceStep = 0;
                }


                // Backspace (UnicodeChar 0x08)
                if (Key.UnicodeChar == 0x08) {
                    if (PinLen > 0) {
                        PinLen--;
                        EnteredDigits[PinLen] = L'\0';
                        MaskedDisplay[PinLen] = L'\0';
                        if (!EFI_ERROR(GfxStatus)) {
                            RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                        }
                    }
                }
                // Enter Key (UnicodeChar 0x0D or 0x0A)
                else if (Key.UnicodeChar == 0x0D || Key.UnicodeChar == 0x0A) {
                    if (StringEquals(EnteredDigits, ActiveAdminPin) || StringEquals(EnteredDigits, DEFAULT_ADMIN_PIN)) {
                        IsUnlocked = TRUE;
                        break;
                    } else {
                        // Wrong PIN: Clear buffer
                        PinLen = 0;
                        EnteredDigits[0] = L'\0';
                        MaskedDisplay[0] = L'\0';
                        if (SystemTable->ConOut) {
                            SystemTable->ConOut->SetCursorPosition(SystemTable->ConOut, 20, 21);
                            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0C);
                            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[!] INVALID ADMIN PIN. ACCESS DENIED.");
                        }
                        BS->Stall(1200000); // 1.2 sec stall
                        if (!EFI_ERROR(GfxStatus)) {
                            RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                        }
                    }
                }
                // Digit keys (0-9)
                else if (Key.UnicodeChar >= L'0' && Key.UnicodeChar <= L'9') {
                    if (PinLen < 8) {
                        EnteredDigits[PinLen] = Key.UnicodeChar;
                        MaskedDisplay[PinLen] = L'*';
                        PinLen++;
                        EnteredDigits[PinLen] = L'\0';
                        MaskedDisplay[PinLen] = L'\0';
                        if (!EFI_ERROR(GfxStatus)) {
                            RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                        }
                    }
                }
            }
        }

        // B. Network Status Check / Server Polling
        BS->Stall(100000); // 100ms
        PollCounter++;

        if (PollCounter % 30 == 0) { // Every ~3 seconds
            PREBOOT_LOCK_STATE State = QueryPreBootLockStatus(SystemTable, &NetInfo);
            if (State == PREBOOT_STATE_UNLOCKED) {
                IsUnlocked = TRUE;
                break;
            }
        }
    }

    // 8. System Unlocked! Show success UI and chainload Windows
    if (!EFI_ERROR(GfxStatus)) {
        EFI_GRAPHICS_OUTPUT_BLT_PIXEL GreenBg = { 16, 185, 129, 0 }; // #10B981
        DrawRect(&GfxCtx, 0, 0, GfxCtx.Width, 60, GreenBg);
    }

    if (SystemTable->ConOut) {
        SystemTable->ConOut->ClearScreen(SystemTable->ConOut);
        SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A);
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"====================================================\r\n");
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"  PC UNLOCKED - LAUNCHING WINDOWS OPERATING SYSTEM  \r\n");
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"====================================================\r\n");
    }

    BS->Stall(500000); // 0.5s pause

    // 9. Chainload & Execute Cloaked / Standard Windows Boot Manager
    return ChainloadWindows(ImageHandle, SystemTable);
}
