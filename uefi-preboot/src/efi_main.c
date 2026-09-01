#include "../include/uefi.h"
#include "graphics.h"
#include "network.h"
#include "chainloader.h"

// Default Fallback PIN if NVRAM or server is not initialized
#define DEFAULT_ADMIN_PIN L"998877"
#define PC_NUMBER_DEFAULT L"PC-01"

static EFI_GUID gPcLockVariableGuid = { 0x54425057, 0x1234, 0x5678, { 0x9a, 0xbc, 0xde, 0xf0, 0x12, 0x34, 0x56, 0x78 } };

/**
 * Constant-Time String Comparison Algorithm (Prevents Side-Channel Timing Attacks)
 */
static BOOLEAN ConstantTimeEquals(const CHAR16 *s1, const CHAR16 *s2, UINTN maxLen)
{
    UINT32 result = 0;
    UINT32 lengthMismatch = 0;

    for (UINTN i = 0; i < maxLen; i++) {
        CHAR16 c1 = s1[i];
        CHAR16 c2 = s2[i];

        result |= (UINT32)(c1 ^ c2);

        if (c1 == L'\0' || c2 == L'\0') {
            if (c1 != c2) lengthMismatch = 1;
            break;
        }
    }

    return (result == 0 && lengthMismatch == 0);
}

/**
 * Cryptographic Memory Zeroization (Prevents Cold-Boot RAM Forensics)
 */
static void SecureZeroMemory(VOID *ptr, UINTN size)
{
    volatile UINT8 *p = (volatile UINT8 *)ptr;
    while (size--) {
        *p++ = 0;
    }
}

/**
 * Bounded Memory Clamping with Guaranteed Null-Termination
 */
static void LoadActiveAdminPin(EFI_SYSTEM_TABLE *SystemTable, CHAR16 *OutPin, UINTN MaxLen)
{
    // Default safe initialization
    for (UINTN i = 0; i < 7 && i < MaxLen; i++) OutPin[i] = DEFAULT_ADMIN_PIN[i];
    OutPin[MaxLen - 1] = L'\0';

    // Try reading dynamic PIN from UEFI NVRAM variable "PcLockPin"
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

    // 1. Reset Text Console & Hide standard cursor
    if (SystemTable->ConOut) {
        SystemTable->ConOut->ClearScreen(SystemTable->ConOut);
        SystemTable->ConOut->EnableCursor(SystemTable->ConOut, FALSE);
    }

    // 2. Initialize GOP (Graphics Output Protocol)
    EFI_STATUS GfxStatus = InitGraphics(BS, &GfxCtx);

    // 3. Initialize Universal Network Stack (Wired & Wireless)
    InitNetwork(BS, &NetInfo);

    // 4. Load Active Admin PIN
    CHAR16 ActiveAdminPin[16] = { 0 };
    LoadActiveAdminPin(SystemTable, ActiveAdminPin, 16);

    // 5. State variables
    BOOLEAN IsUnlocked = FALSE;
    CHAR16 EnteredDigits[16] = { 0 };
    CHAR16 MaskedDisplay[16] = { 0 };
    UINTN PinLen = 0;
    UINTN PollCounter = 0;

    CHAR16 StatusMsg[64] = L"Connecting (Wired/Wi-Fi)...";

    // 6. Initial Lock Screen Render
    if (!EFI_ERROR(GfxStatus)) {
        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
    }

    // 7. Pre-Boot Event Loop (Constant-Time Verification & Memory Zeroization)
    while (!IsUnlocked) {
        // A. Check for Keyboard PIN Input
        if (SystemTable->ConIn) {
            EFI_INPUT_KEY Key;
            EFI_STATUS KeyStatus = SystemTable->ConIn->ReadKeyStroke(SystemTable->ConIn, &Key);

            if (!EFI_ERROR(KeyStatus)) {
                // Backspace (UnicodeChar 0x08)
                if (Key.UnicodeChar == 0x08) {
                    if (PinLen > 0) {
                        PinLen--;
                        EnteredDigits[PinLen] = L'\0';
                        MaskedDisplay[PinLen] = L'\0';
                        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                    }
                }
                // Enter Key (UnicodeChar 0x0D) -> Constant-Time Verification
                else if (Key.UnicodeChar == 0x0D) {
                    if (ConstantTimeEquals(EnteredDigits, ActiveAdminPin, 16)) {
                        IsUnlocked = TRUE;
                        SecureZeroMemory(EnteredDigits, sizeof(EnteredDigits));
                        SecureZeroMemory(ActiveAdminPin, sizeof(ActiveAdminPin));

                        if (SystemTable->ConOut) {
                            SystemTable->ConOut->SetCursorPosition(SystemTable->ConOut, 20, 21);
                            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A);
                            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[AUTHORIZED] Emergency PIN verified. Unlocking...");
                        }
                        BS->Stall(600000);
                        break;
                    } else {
                        // Securely zero out buffer on invalid attempt
                        PinLen = 0;
                        SecureZeroMemory(EnteredDigits, sizeof(EnteredDigits));
                        SecureZeroMemory(MaskedDisplay, sizeof(MaskedDisplay));

                        if (SystemTable->ConOut) {
                            SystemTable->ConOut->SetCursorPosition(SystemTable->ConOut, 20, 21);
                            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0C);
                            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[ACCESS DENIED] Invalid Emergency PIN!");
                        }
                        BS->Stall(800000);
                        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                    }
                }
                // Numeric Digits (0 - 9)
                else if (Key.UnicodeChar >= L'0' && Key.UnicodeChar <= L'9') {
                    if (PinLen < 15) {
                        EnteredDigits[PinLen] = Key.UnicodeChar;
                        MaskedDisplay[PinLen] = L'*';
                        PinLen++;
                        EnteredDigits[PinLen] = L'\0';
                        MaskedDisplay[PinLen] = L'\0';
                        RenderPreBootLockScreen(SystemTable, &GfxCtx, PC_NUMBER_DEFAULT, StatusMsg, MaskedDisplay);
                    }
                }
            }
        }

        // B. Periodically Poll Universal Network Gateway (Wired + Wi-Fi)
        PollCounter++;
        if (PollCounter >= 40) {
            PollCounter = 0;
            PREBOOT_LOCK_STATE NetState = QueryPreBootLockStatus(SystemTable, &NetInfo);
            if (NetState == PREBOOT_STATE_UNLOCKED) {
                IsUnlocked = TRUE;
                SecureZeroMemory(EnteredDigits, sizeof(EnteredDigits));
                SecureZeroMemory(ActiveAdminPin, sizeof(ActiveAdminPin));

                if (SystemTable->ConOut) {
                    SystemTable->ConOut->SetCursorPosition(SystemTable->ConOut, 20, 21);
                    SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A);
                    SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[REMOTE UNLOCKED] Authorized via Mobile App!");
                }
                BS->Stall(600000);
                break;
            }
        }

        // Sleep 25ms to reduce CPU power consumption
        BS->Stall(25000);
    }

    // 8. Authorization Succeeded -> Chainload Windows Boot Manager
    if (SystemTable->ConOut) {
        SystemTable->ConOut->ClearScreen(SystemTable->ConOut);
        SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0F);
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"Pre-Boot Security Verification Succeeded.\r\nStarting Windows...\r\n");
    }

    return ChainloadWindows(ImageHandle, SystemTable);
}
