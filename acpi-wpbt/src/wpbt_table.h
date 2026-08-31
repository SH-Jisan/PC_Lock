#ifndef _WPBT_TABLE_H_
#define _WPBT_TABLE_H_

#include <stdint.h>

#pragma pack(push, 1)

// Standard ACPI Header (36 bytes)
typedef struct {
    uint32_t Signature;        // 'WPBT' (0x54425057)
    uint32_t Length;           // Total length of the table in bytes
    uint8_t  Revision;         // 1
    uint8_t  Checksum;         // Computed so that the entire table sums to 0 mod 256
    uint8_t  OemId[6];         // OEM Identifier string
    uint8_t  OemTableId[8];    // OEM Table Identifier string
    uint32_t OemRevision;      // OEM Revision number
    uint32_t CreatorId;        // ASL compiler or Creator ID
    uint32_t CreatorRevision;  // Creator Revision number
} ACPI_DESCRIPTION_HEADER;

// Microsoft Windows Platform Binary Table (WPBT) Definition
// Specification: Windows Platform Binary Table (WPBT) v1.0
typedef struct {
    ACPI_DESCRIPTION_HEADER Header;
    
    uint32_t HandoffMemorySize;     // Size of the binary payload in bytes
    uint64_t HandoffMemoryLocation; // 64-bit physical memory address of payload in RAM
    uint8_t  ContentLayout;         // 0 = PE Binary Executable
    uint8_t  ContentType;           // 1 = Native Windows Application
    uint16_t CommandLineLength;     // Length of command-line string in UTF-16 characters
    uint16_t CommandLine[1];        // Variable length UTF-16 argument string (e.g. L"")
} ACPI_WPBT_TABLE;

#pragma pack(pop)

#define ACPI_WPBT_SIGNATURE 0x54425057 // 'WPBT' in little-endian

static inline uint8_t CalculateAcpiChecksum(const uint8_t *Buffer, uint32_t Length)
{
    uint8_t Sum = 0;
    for (uint32_t i = 0; i < Length; i++) {
        Sum += Buffer[i];
    }
    return (uint8_t)(0x100 - Sum);
}

#endif // _WPBT_TABLE_H_
