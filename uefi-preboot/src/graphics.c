#include "graphics.h"

static EFI_GUID gGopGuid = EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID;

EFI_STATUS InitGraphics(EFI_BOOT_SERVICES *BS, GOP_CONTEXT *Ctx)
{
    EFI_STATUS Status;
    EFI_GRAPHICS_OUTPUT_PROTOCOL *Gop = NULL;

    Status = BS->LocateProtocol(&gGopGuid, NULL, (VOID**)&Gop);
    if (EFI_ERROR(Status) || Gop == NULL) {
        return EFI_UNSUPPORTED;
    }

    Ctx->Gop = Gop;
    Ctx->Width = Gop->Mode->Info->HorizontalResolution;
    Ctx->Height = Gop->Mode->Info->VerticalResolution;

    return EFI_SUCCESS;
}

VOID ClearScreen(GOP_CONTEXT *Ctx, EFI_GRAPHICS_OUTPUT_BLT_PIXEL Color)
{
    if (!Ctx || !Ctx->Gop) return;

    Ctx->Gop->Blt(
        Ctx->Gop,
        &Color,
        EfiBltVideoFill,
        0, 0,
        0, 0,
        Ctx->Width, Ctx->Height,
        0
    );
}

VOID DrawRect(GOP_CONTEXT *Ctx, UINTN X, UINTN Y, UINTN Width, UINTN Height, EFI_GRAPHICS_OUTPUT_BLT_PIXEL Color)
{
    if (!Ctx || !Ctx->Gop) return;

    Ctx->Gop->Blt(
        Ctx->Gop,
        &Color,
        EfiBltVideoFill,
        0, 0,
        X, Y,
        Width, Height,
        0
    );
}

VOID DrawCard(GOP_CONTEXT *Ctx, UINTN X, UINTN Y, UINTN Width, UINTN Height, EFI_GRAPHICS_OUTPUT_BLT_PIXEL BgColor, EFI_GRAPHICS_OUTPUT_BLT_PIXEL BorderColor)
{
    // Outer border (2px)
    DrawRect(Ctx, X, Y, Width, Height, BorderColor);
    // Inner background
    DrawRect(Ctx, X + 2, Y + 2, Width - 4, Height - 4, BgColor);
}

VOID RenderPreBootLockScreen(EFI_SYSTEM_TABLE *ST, GOP_CONTEXT *Ctx, const CHAR16 *PcNumber, const CHAR16 *StatusMessage, const CHAR16 *EnteredPin)
{
    // Palette definition (BGR format)
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL BgDark      = { 25, 15, 11, 0 };    // #0B0F19
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL CardBg      = { 46, 30, 22, 0 };    // #161E2E
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL CardBorder  = { 85, 65, 51, 0 };    // #334155
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL CyanAccent  = { 248, 189, 56, 0 };  // #38BDF8
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL RedLocked   = { 68, 68, 239, 0 };   // #EF4444
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL BarTop      = { 30, 20, 15, 0 };    // Header bar

    // Fill background
    ClearScreen(Ctx, BgDark);

    // Top Header Banner (Height 50px)
    DrawRect(Ctx, 0, 0, Ctx->Width, 50, BarTop);
    DrawRect(Ctx, 0, 48, Ctx->Width, 2, CyanAccent);

    // Central Card
    UINTN CardW = 620;
    UINTN CardH = 380;
    UINTN CardX = (Ctx->Width > CardW) ? (Ctx->Width - CardW) / 2 : 20;
    UINTN CardY = (Ctx->Height > CardH) ? (Ctx->Height - CardH) / 2 : 60;

    DrawCard(Ctx, CardX, CardY, CardW, CardH, CardBg, CardBorder);

    // Status Header Box inside Card
    DrawRect(Ctx, CardX + 20, CardY + 20, CardW - 40, 50, RedLocked);

    // PIN Entry Box inside Card
    EFI_GRAPHICS_OUTPUT_BLT_PIXEL InputBg = { 20, 15, 10, 0 };
    DrawRect(Ctx, CardX + 40, CardY + 260, CardW - 80, 45, InputBg);
    DrawRect(Ctx, CardX + 40, CardY + 303, CardW - 80, 2, CyanAccent);

    // Use UEFI Text output positioned on screen
    if (ST && ST->ConOut) {
        ST->ConOut->SetAttribute(ST->ConOut, 0x0F); // White on Black
        
        // Print Top Bar text
        ST->ConOut->SetCursorPosition(ST->ConOut, 2, 1);
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[ CYBER CAFE SECURE PRE-BOOT CONTROLLER ]");

        // Print PC Number
        ST->ConOut->SetCursorPosition(ST->ConOut, 25, 6);
        ST->ConOut->SetAttribute(ST->ConOut, 0x0E); // Yellow
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"TERMINAL IDENTIFIER: ");
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)PcNumber);

        // Status text
        ST->ConOut->SetCursorPosition(ST->ConOut, 25, 9);
        ST->ConOut->SetAttribute(ST->ConOut, 0x0C); // Light Red
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"STATUS: [ SYSTEM LOCKED BEFORE BOOT ]");

        // Message
        ST->ConOut->SetCursorPosition(ST->ConOut, 20, 12);
        ST->ConOut->SetAttribute(ST->ConOut, 0x07); // Light Gray
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"Please purchase a session from the Counter / Reception.");

        ST->ConOut->SetCursorPosition(ST->ConOut, 20, 14);
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"Windows will automatically boot once unlocked.");

        // Polling info
        ST->ConOut->SetCursorPosition(ST->ConOut, 20, 16);
        ST->ConOut->SetAttribute(ST->ConOut, 0x0B); // Light Cyan
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)StatusMessage);

        // Emergency Admin PIN prompt
        ST->ConOut->SetCursorPosition(ST->ConOut, 20, 19);
        ST->ConOut->SetAttribute(ST->ConOut, 0x0A); // Light Green
        ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"Emergency Admin Master PIN: ");
        if (EnteredPin && EnteredPin[0] != L'\0') {
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)EnteredPin);
        } else {
            ST->ConOut->SetAttribute(ST->ConOut, 0x08); // Dark Gray
            ST->ConOut->OutputString(ST->ConOut, (CHAR16*)L"[Type 6-digit PIN on keyboard]");
        }
    }
}
