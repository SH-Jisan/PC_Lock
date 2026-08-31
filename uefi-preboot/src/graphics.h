#ifndef _GRAPHICS_H_
#define _GRAPHICS_H_

#include "../include/uefi.h"

typedef struct {
    UINT32 Width;
    UINT32 Height;
    EFI_GRAPHICS_OUTPUT_PROTOCOL *Gop;
} GOP_CONTEXT;

// Initializes UEFI Graphics Output Protocol
EFI_STATUS InitGraphics(EFI_BOOT_SERVICES *BS, GOP_CONTEXT *Ctx);

// Clears the screen with a dark Cyber Cafe themed color
VOID ClearScreen(GOP_CONTEXT *Ctx, EFI_GRAPHICS_OUTPUT_BLT_PIXEL Color);

// Draws a filled rectangle
VOID DrawRect(GOP_CONTEXT *Ctx, UINTN X, UINTN Y, UINTN Width, UINTN Height, EFI_GRAPHICS_OUTPUT_BLT_PIXEL Color);

// Draws a stylish bordered card / panel
VOID DrawCard(GOP_CONTEXT *Ctx, UINTN X, UINTN Y, UINTN Width, UINTN Height, EFI_GRAPHICS_OUTPUT_BLT_PIXEL BgColor, EFI_GRAPHICS_OUTPUT_BLT_PIXEL BorderColor);

// Draws a status badge (e.g. LOCKED / UNLOCKED)
VOID DrawStatusBadge(GOP_CONTEXT *Ctx, UINTN X, UINTN Y, const CHAR16 *Text, BOOLEAN IsLocked);

// Renders the Cyber Cafe Pre-Boot Locked Screen
VOID RenderPreBootLockScreen(EFI_SYSTEM_TABLE *ST, GOP_CONTEXT *Ctx, const CHAR16 *PcNumber, const CHAR16 *StatusMessage, const CHAR16 *EnteredPin);

#endif // _GRAPHICS_H_
