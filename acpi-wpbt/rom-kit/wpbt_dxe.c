/**
 * @file wpbt_dxe.c
 * @brief Native UEFI DXE Driver for ACPI WPBT Hardware Flash ROM Injection.
 * 
 * Target: Embedded in Motherboard SPI Flash BIOS ROM (DXE Volume / Firmware Volume).
 * Standard: Microsoft Windows Platform Binary Table (WPBT) v1.0 Specification.
 */

#include <Uefi.h>
#include <Library/UefiBootServicesTableLib.h>
#include <Library/UefiRuntimeServicesTableLib.h>
#include <Library/DebugLib.h>
#include <Library/BaseMemoryLib.h>
#include <Library/MemoryAllocationLib.h>
#include <Protocol/AcpiTable.h>
#include "../src/wpbt_table.h"

// Hardcoded sample embedded payload byte array for compilation
static const UINT8 gEmbeddedWpbbinAgent[] = {
    0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00,
    0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x50, 0x45, 0x00, 0x00, 0x64, 0x86, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
};

EFI_STATUS
EFIAPI
WpbtDxeDriverEntryPoint (
    IN EFI_HANDLE        ImageHandle,
    IN EFI_SYSTEM_TABLE  *SystemTable
)
{
    EFI_STATUS              Status;
    EFI_ACPI_TABLE_PROTOCOL *AcpiTableProtocol = NULL;
    UINTN                   TableKey = 0;

    DEBUG ((DEBUG_INFO, "[WPBT DXE] Initializing Hardware ACPI WPBT Table Installation...\n"));

    // 1. Locate EFI_ACPI_TABLE_PROTOCOL
    Status = gBS->LocateProtocol (
        &gEfiAcpiTableProtocolGuid,
        NULL,
        (VOID **)&AcpiTableProtocol
    );

    if (EFI_ERROR (Status) || AcpiTableProtocol == NULL) {
        DEBUG ((DEBUG_ERROR, "[WPBT DXE Error] EFI_ACPI_TABLE_PROTOCOL unavailable (%r)\n", Status));
        return Status;
    }

    // 2. Allocate Physical ACPI Reclaim Memory for the Embedded Agent
    UINTN PayloadSize = sizeof(gEmbeddedWpbbinAgent);
    UINTN PagesNeeded = EFI_SIZE_TO_PAGES (PayloadSize);
    EFI_PHYSICAL_ADDRESS HandoffMemoryAddress = 0;

    Status = gBS->AllocatePages (
        AllocateAnyPages,
        EfiACPIReclaimMemory,
        PagesNeeded,
        &HandoffMemoryAddress
    );

    if (EFI_ERROR (Status)) {
        DEBUG ((DEBUG_ERROR, "[WPBT DXE Error] Failed to allocate ACPI Reclaim memory (%r)\n", Status));
        return Status;
    }

    // Copy agent binary into physical ACPI memory
    CopyMem ((VOID *)(UINTN)HandoffMemoryAddress, gEmbeddedWpbbinAgent, PayloadSize);

    // 3. Construct Standard Microsoft ACPI_WPBT_TABLE in Memory
    UINT32 TableLength = sizeof (ACPI_WPBT_TABLE);
    ACPI_WPBT_TABLE *WpbtTable = AllocateZeroPool (TableLength);
    if (WpbtTable == NULL) {
        return EFI_OUT_OF_RESOURCES;
    }

    // ACPI Header
    WpbtTable->Header.Signature = ACPI_WPBT_SIGNATURE; // 'WPBT'
    WpbtTable->Header.Length = TableLength;
    WpbtTable->Header.Revision = 1;
    CopyMem (WpbtTable->Header.OemId, "PCLOCK", 6);
    CopyMem (WpbtTable->Header.OemTableId, "CYBERSEC", 8);
    WpbtTable->Header.OemRevision = 0x00000001;
    WpbtTable->Header.CreatorId = 0x54425057; // 'WPBT'
    WpbtTable->Header.CreatorRevision = 0x00000001;

    // WPBT Specific Fields
    WpbtTable->HandoffMemorySize = (UINT32)PayloadSize;
    WpbtTable->HandoffMemoryLocation = (UINT64)HandoffMemoryAddress;
    WpbtTable->ContentLayout = 0; // PE Binary Executable
    WpbtTable->ContentType = 1;   // Native Application
    WpbtTable->CommandLineLength = 0;
    WpbtTable->CommandLine[0] = L'\0';

    // Calculate Table Checksum
    WpbtTable->Header.Checksum = CalculateAcpiChecksum ((UINT8 *)WpbtTable, TableLength);

    // 4. Install WPBT Table into Motherboard ACPI Root Table (RSDT / XSDT)
    Status = AcpiTableProtocol->InstallAcpiTable (
        AcpiTableProtocol,
        WpbtTable,
        TableLength,
        &TableKey
    );

    if (EFI_ERROR (Status)) {
        DEBUG ((DEBUG_ERROR, "[WPBT DXE Error] InstallAcpiTable failed (%r)\n", Status));
        FreePool (WpbtTable);
        return Status;
    }

    DEBUG ((DEBUG_INFO, "[WPBT DXE Success] ACPI WPBT Table permanently registered with Key: %d\n", TableKey));
    FreePool (WpbtTable);
    return EFI_SUCCESS;
}
