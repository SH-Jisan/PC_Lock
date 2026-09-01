#include "graphics.h"

EFI_STATUS InitGraphics(EFI_BOOT_SERVICES *BS, GOP_CONTEXT *Ctx)
{
    if (!Ctx) return EFI_INVALID_PARAMETER;
    Ctx->Gop = NULL;
    Ctx->Width = 80;
    Ctx->Height = 25;
    return EFI_SUCCESS;
}

VOID RenderPreBootLockScreen(EFI_SYSTEM_TABLE *ST, GOP_CONTEXT *Ctx, const CHAR16 *PcNumber, const CHAR16 *StatusMessage, const CHAR16 *EnteredPin)
{
    if (!ST || !ST->ConOut) return;

    EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *Out = ST->ConOut;

    // 1. Clear Screen
    Out->ClearScreen(Out);
    Out->EnableCursor(Out, FALSE);

    // 2. Cyber Header Banner (Cyan / White)
    Out->SetAttribute(Out, 0x0B); // Light Cyan
    Out->SetCursorPosition(Out, 0, 1);
    Out->OutputString(Out, (CHAR16*)L" ==============================================================================\r\n");
    Out->SetAttribute(Out, 0x0F); // Bright White
    Out->OutputString(Out, (CHAR16*)L"                   CYBER CAFE SECURE PRE-BOOT CONTROLLER                       \r\n");
    Out->SetAttribute(Out, 0x0B);
    Out->OutputString(Out, (CHAR16*)L" ==============================================================================\r\n\r\n");

    // 3. Security Status Box (Red Alert)
    Out->SetAttribute(Out, 0x0C); // Light Red
    Out->OutputString(Out, (CHAR16*)L"  [!] SECURITY NOTICE: Workstation is currently LOCKED before Windows boot.\r\n\r\n");

    // 4. Terminal Info
    Out->SetAttribute(Out, 0x0E); // Yellow
    Out->OutputString(Out, (CHAR16*)L"  [*] Terminal Identifier : ");
    Out->SetAttribute(Out, 0x0F);
    Out->OutputString(Out, (CHAR16*)PcNumber);
    Out->OutputString(Out, (CHAR16*)L"\r\n");

    Out->SetAttribute(Out, 0x0E);
    Out->OutputString(Out, (CHAR16*)L"  [*] Security Status     : ");
    Out->SetAttribute(Out, 0x0C);
    Out->OutputString(Out, (CHAR16*)L"LOCKED (Firmware Enforced)\r\n");

    Out->SetAttribute(Out, 0x0E);
    Out->OutputString(Out, (CHAR16*)L"  [*] Network Gateway     : ");
    Out->SetAttribute(Out, 0x0B);
    Out->OutputString(Out, (CHAR16*)StatusMessage);
    Out->OutputString(Out, (CHAR16*)L"\r\n\r\n");

    // 5. User Instruction
    Out->SetAttribute(Out, 0x07); // Light Gray
    Out->OutputString(Out, (CHAR16*)L"  ----------------------------------------------------------------------------\r\n");
    Out->OutputString(Out, (CHAR16*)L"  Please unlock this workstation from the Counter / Mobile Controller App.\r\n");
    Out->OutputString(Out, (CHAR16*)L"  Windows will automatically start as soon as an unlock signal is received.\r\n");
    Out->OutputString(Out, (CHAR16*)L"  ----------------------------------------------------------------------------\r\n\r\n");

    // 6. Emergency PIN Prompt
    Out->SetAttribute(Out, 0x0A); // Light Green
    Out->OutputString(Out, (CHAR16*)L"  >> EMERGENCY MASTER PIN : [ ");
    
    if (EnteredPin && EnteredPin[0] != L'\0') {
        Out->SetAttribute(Out, 0x0F); // White
        Out->OutputString(Out, (CHAR16*)EnteredPin);
    } else {
        Out->SetAttribute(Out, 0x08); // Dark Gray
        Out->OutputString(Out, (CHAR16*)L"Type PIN or Master Code & press ENTER");
    }

    Out->SetAttribute(Out, 0x0A);
    Out->OutputString(Out, (CHAR16*)L" ]\r\n\r\n");

    Out->SetAttribute(Out, 0x08); // Dark Gray
    Out->OutputString(Out, (CHAR16*)L"  (Supported Master Codes: 998877, SHJ, shj | Backspace to edit | Enter to verify)\r\n");
}
