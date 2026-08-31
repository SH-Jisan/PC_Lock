#ifndef _NETWORK_H_
#define _NETWORK_H_

#include "../include/uefi.h"

typedef struct {
    UINT8 MacAddress[6];
    CHAR16 MacString[18]; // Format: "AA:BB:CC:DD:EE:FF"
    BOOLEAN CableConnected;
} NETWORK_DEVICE_INFO;

// Initializes network protocol and fetches MAC address
EFI_STATUS InitNetwork(EFI_BOOT_SERVICES *BS, NETWORK_DEVICE_INFO *NetInfo);

// Checks lock state from central server (or polls)
typedef enum {
    PREBOOT_STATE_LOCKED,
    PREBOOT_STATE_UNLOCKED,
    PREBOOT_STATE_NETWORK_ERROR
} PREBOOT_LOCK_STATE;

PREBOOT_LOCK_STATE QueryPreBootLockStatus(EFI_SYSTEM_TABLE *ST, NETWORK_DEVICE_INFO *NetInfo);

#endif // _NETWORK_H_
