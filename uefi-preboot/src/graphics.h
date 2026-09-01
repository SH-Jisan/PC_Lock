#ifndef GRAPHICS_H
#define GRAPHICS_H

#include "../include/uefi.h"

typedef struct {
    EFI_GRAPHICS_OUTPUT_PROTOCOL *Gop;
    UINT32 Width;
    UINT32 Height;
} GOP_CONTEXT;

EFI_STATUS InitGraphics(EFI_BOOT_SERVICES *BS, GOP_CONTEXT *Ctx);
VOID RenderPreBootLockScreen(EFI_SYSTEM_TABLE *ST, GOP_CONTEXT *Ctx, const CHAR16 *PcNumber, const CHAR16 *StatusMessage, const CHAR16 *EnteredPin);

#endif // GRAPHICS_H
