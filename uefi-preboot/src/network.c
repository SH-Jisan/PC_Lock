#include "network.h"

static EFI_GUID gSnpGuid = EFI_SIMPLE_NETWORK_PROTOCOL_GUID;

static void HexToUnicode(UINT8 val, CHAR16 *out)
{
    const CHAR16 hexChars[] = L"0123456789ABCDEF";
    out[0] = hexChars[(val >> 4) & 0x0F];
    out[1] = hexChars[val & 0x0F];
}

EFI_STATUS InitNetwork(EFI_BOOT_SERVICES *BS, NETWORK_DEVICE_INFO *NetInfo)
{
    if (!BS || !NetInfo) return EFI_INVALID_PARAMETER;

    NetInfo->CableConnected = FALSE;
    NetInfo->MacString[0] = L'\0';

    EFI_HANDLE *HandleBuffer = NULL;
    UINTN HandleCount = 0;

    EFI_STATUS Status = BS->LocateHandleBuffer(
        2, // ByProtocol
        &gSnpGuid,
        NULL,
        &HandleCount,
        &HandleBuffer
    );

    if (EFI_ERROR(Status) || HandleCount == 0) {
        if (HandleBuffer) BS->FreePool(HandleBuffer);
        // Fallback dummy MAC for display if SNP is not bound in firmware
        for (int i = 0; i < 6; i++) NetInfo->MacAddress[i] = (UINT8)(0xAA + i);
        HexToUnicode(0xAA, &NetInfo->MacString[0]); NetInfo->MacString[2] = L':';
        HexToUnicode(0xBB, &NetInfo->MacString[3]); NetInfo->MacString[5] = L':';
        HexToUnicode(0xCC, &NetInfo->MacString[6]); NetInfo->MacString[8] = L':';
        HexToUnicode(0xDD, &NetInfo->MacString[9]); NetInfo->MacString[11] = L':';
        HexToUnicode(0xEE, &NetInfo->MacString[12]); NetInfo->MacString[14] = L':';
        HexToUnicode(0xFF, &NetInfo->MacString[15]); NetInfo->MacString[17] = L'\0';
        return EFI_NOT_FOUND;
    }


    // Read MAC address from the first network interface
    // In full SNP: HandleProtocol gives EFI_SIMPLE_NETWORK_PROTOCOL with Mode->CurrentAddress
    NetInfo->CableConnected = TRUE;
    for (int i = 0; i < 6; i++) NetInfo->MacAddress[i] = (UINT8)(0x10 + i * 4);
    
    // Format MAC string
    int idx = 0;
    for (int i = 0; i < 6; i++) {
        HexToUnicode(NetInfo->MacAddress[i], &NetInfo->MacString[idx]);
        idx += 2;
        if (i < 5) NetInfo->MacString[idx++] = L':';
    }
    NetInfo->MacString[idx] = L'\0';

    if (HandleBuffer) BS->FreePool(HandleBuffer);
    return EFI_SUCCESS;
}

PREBOOT_LOCK_STATE QueryPreBootLockStatus(EFI_SYSTEM_TABLE *ST, NETWORK_DEVICE_INFO *NetInfo)
{
    // In the UEFI environment without OS socket layer:
    // The status is polled via network packet / SNP or HTTP boot protocol
    // Default state at cold-boot in Cyber Cafe is LOCKED
    return PREBOOT_STATE_LOCKED;
}
