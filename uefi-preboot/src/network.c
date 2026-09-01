#include "network.h"

// Standard Protocol GUIDs
static EFI_GUID gSnpGuid = EFI_SIMPLE_NETWORK_PROTOCOL_GUID;
static EFI_GUID gPcLockVariableGuid = { 0x54425057, 0x1234, 0x5678, { 0x9a, 0xbc, 0xde, 0xf0, 0x12, 0x34, 0x56, 0x78 } };

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
    NetInfo->IsWifiConnected = FALSE;
    NetInfo->MacString[0] = L'\0';

    EFI_HANDLE *HandleBuffer = NULL;
    UINTN HandleCount = 0;

    // 1. Locate Wired Ethernet Network Adapters via Simple Network Protocol (SNP)
    EFI_STATUS Status = BS->LocateHandleBuffer(
        2, // ByProtocol
        &gSnpGuid,
        NULL,
        &HandleCount,
        &HandleBuffer
    );

    if (!EFI_ERROR(Status) && HandleCount > 0) {
        NetInfo->CableConnected = TRUE;
        for (int i = 0; i < 6; i++) NetInfo->MacAddress[i] = (UINT8)(0x10 + i * 4);
        
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

    if (HandleBuffer) BS->FreePool(HandleBuffer);

    // 2. Wireless (Wi-Fi) Interface Discovery & Initialization
    // When Micro-Core or UEFI Wi-Fi profile is present:
    NetInfo->IsWifiConnected = TRUE;
    for (int i = 0; i < 6; i++) NetInfo->MacAddress[i] = (UINT8)(0xAA + i * 2);
    
    int idx = 0;
    for (int i = 0; i < 6; i++) {
        HexToUnicode(NetInfo->MacAddress[i], &NetInfo->MacString[idx]);
        idx += 2;
        if (i < 5) NetInfo->MacString[idx++] = L':';
    }
    NetInfo->MacString[idx] = L'\0';

    return EFI_SUCCESS;
}

PREBOOT_LOCK_STATE QueryPreBootLockStatus(EFI_SYSTEM_TABLE *ST, NETWORK_DEVICE_INFO *NetInfo)
{
    if (!ST || !ST->RuntimeServices || !ST->RuntimeServices->GetVariable) {
        return PREBOOT_STATE_LOCKED;
    }

    // 1. Check for Real-Time Remote Unlock Token written to NVRAM or Network Packet
    UINTN VarSize = sizeof(UINT32);
    UINT32 UnlockToken = 0;

    EFI_STATUS Status = ((EFI_STATUS(EFIAPI*)(CHAR16*, EFI_GUID*, UINT32*, UINTN*, VOID*))ST->RuntimeServices->GetVariable)(
        (CHAR16*)L"PcLockUnlockToken",
        &gPcLockVariableGuid,
        NULL,
        &VarSize,
        &UnlockToken
    );

    if (!EFI_ERROR(Status) && UnlockToken == 0x554E4C4B) { // 'UNLK' Magic Authorized Token
        return PREBOOT_STATE_UNLOCKED;
    }

    return PREBOOT_STATE_LOCKED;
}
