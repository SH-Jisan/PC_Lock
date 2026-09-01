#ifndef _UEFI_H_
#define _UEFI_H_

#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>

// Calling convention for x86_64 UEFI
#if defined(_MSC_VER)
#define EFIAPI __cdecl
#elif defined(__GNUC__) || defined(__clang__)
#define EFIAPI __attribute__((ms_abi))
#else
#define EFIAPI
#endif

#define IN
#define OUT
#define OPTIONAL
#define CONST const

typedef void            VOID;
typedef uint8_t         BOOLEAN;
typedef int64_t         INTN;
typedef uint64_t        UINTN;
typedef int8_t          INT8;
typedef uint8_t         UINT8;
typedef int16_t         INT16;
typedef uint16_t        UINT16;
typedef int32_t         INT32;
typedef uint32_t        UINT32;
typedef int64_t         INT64;
typedef uint64_t        UINT64;
typedef uint8_t         CHAR8;
typedef uint16_t        CHAR16;

typedef UINTN           EFI_STATUS;
typedef VOID*           EFI_HANDLE;
typedef VOID*           EFI_EVENT;
typedef UINT64          EFI_PHYSICAL_ADDRESS;
typedef UINT64          EFI_VIRTUAL_ADDRESS;

#define TRUE  1
#define FALSE 0

#define EFI_SUCCESS               0
#define EFI_LOAD_ERROR            (1 | (1ULL << 63))
#define EFI_INVALID_PARAMETER     (2 | (1ULL << 63))
#define EFI_UNSUPPORTED           (3 | (1ULL << 63))
#define EFI_BAD_BUFFER_SIZE       (4 | (1ULL << 63))
#define EFI_BUFFER_TOO_SMALL      (5 | (1ULL << 63))
#define EFI_NOT_READY             (6 | (1ULL << 63))
#define EFI_DEVICE_ERROR          (7 | (1ULL << 63))
#define EFI_WRITE_PROTECTED       (8 | (1ULL << 63))
#define EFI_OUT_OF_RESOURCES      (9 | (1ULL << 63))
#define EFI_VOLUME_CORRUPTED      (10 | (1ULL << 63))
#define EFI_VOLUME_FULL           (11 | (1ULL << 63))
#define EFI_NO_MEDIA              (12 | (1ULL << 63))
#define EFI_MEDIA_CHANGED         (13 | (1ULL << 63))
#define EFI_NOT_FOUND             (14 | (1ULL << 63))
#define EFI_ACCESS_DENIED         (15 | (1ULL << 63))

#define EFI_ERROR(status) (((INTN)(status)) < 0)

typedef struct {
    UINT32 Data1;
    UINT16 Data2;
    UINT16 Data3;
    UINT8  Data4[8];
} EFI_GUID;

#define EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID \
    { 0x9042a9de, 0x23dc, 0x4a38, { 0x96, 0xfb, 0x7a, 0xde, 0xd0, 0x80, 0x51, 0x6a } }

#define EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID \
    { 0x964e5b22, 0x6459, 0x11d2, { 0x8e, 0x39, 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b } }

#define EFI_LOADED_IMAGE_PROTOCOL_GUID \
    { 0x5b1b31a1, 0x9562, 0x11d2, { 0x8e, 0x3f, 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b } }

#define EFI_DEVICE_PATH_PROTOCOL_GUID \
    { 0x09576e91, 0x6d3f, 0x11d2, { 0x8e, 0x39, 0x00, 0xa0, 0xc9, 0x69, 0x72, 0x3b } }

#define EFI_SIMPLE_NETWORK_PROTOCOL_GUID \
    { 0xA19832B9, 0xAC25, 0x11D3, { 0x9A, 0x2D, 0x00, 0x90, 0x27, 0x3F, 0xC1, 0x4D } }

// Reset Types
typedef enum {
    EfiResetCold,
    EfiResetWarm,
    EfiResetShutdown,
    EfiResetPlatformSpecific
} EFI_RESET_TYPE;

typedef struct _EFI_RUNTIME_SERVICES {
    char Reserved1[24];
    EFI_STATUS (EFIAPI *GetVariable)(CHAR16 *VariableName, EFI_GUID *VendorGuid, UINT32 *Attributes, UINTN *DataSize, VOID *Data);
    EFI_STATUS (EFIAPI *GetNextVariableName)(UINTN *VariableNameSize, CHAR16 *VariableName, EFI_GUID *VendorGuid);
    EFI_STATUS (EFIAPI *SetVariable)(CHAR16 *VariableName, EFI_GUID *VendorGuid, UINT32 Attributes, UINTN DataSize, VOID *Data);
    char Reserved2[32];
    VOID (EFIAPI *ResetSystem)(EFI_RESET_TYPE ResetType, EFI_STATUS ResetStatus, UINTN DataSize, CHAR16 *ResetData);
} EFI_RUNTIME_SERVICES;

typedef enum {
    AllocateAnyPages,
    AllocateMaxAddress,
    AllocateAddress,
    MaxAllocateType
} EFI_ALLOCATE_TYPE;

typedef enum {
    EfiReservedMemoryType,
    EfiLoaderCode,
    EfiLoaderData,
    EfiBootServicesCode,
    EfiBootServicesData,
    EfiRuntimeServicesCode,
    EfiRuntimeServicesData,
    EfiConventionalMemory,
    EfiUnusableMemory,
    EfiACPIReclaimMemory,
    EfiACPIMemoryNVS,
    EfiMemoryMappedIO,
    EfiMemoryMappedIOPortSpace,
    EfiPalCode,
    EfiPersistentMemory,
    EfiMaxMemoryType
} EFI_MEMORY_TYPE;

typedef struct _EFI_SIMPLE_TEXT_INPUT_PROTOCOL EFI_SIMPLE_TEXT_INPUT_PROTOCOL;
typedef struct _EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL;

typedef struct {
    UINT16 ScanCode;
    CHAR16 UnicodeChar;
} EFI_INPUT_KEY;

struct _EFI_SIMPLE_TEXT_INPUT_PROTOCOL {
    EFI_STATUS (EFIAPI *Reset)(EFI_SIMPLE_TEXT_INPUT_PROTOCOL *This, BOOLEAN ExtendedVerification);
    EFI_STATUS (EFIAPI *ReadKeyStroke)(EFI_SIMPLE_TEXT_INPUT_PROTOCOL *This, EFI_INPUT_KEY *Key);
    EFI_EVENT  WaitForKey;
};

struct _EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL {
    EFI_STATUS (EFIAPI *Reset)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, BOOLEAN ExtendedVerification);
    EFI_STATUS (EFIAPI *OutputString)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, CHAR16 *String);
    EFI_STATUS (EFIAPI *TestString)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, CHAR16 *String);
    EFI_STATUS (EFIAPI *QueryMode)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, UINTN ModeNumber, UINTN *Columns, UINTN *Rows);
    EFI_STATUS (EFIAPI *SetMode)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, UINTN ModeNumber);
    EFI_STATUS (EFIAPI *SetAttribute)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, UINTN Attribute);
    EFI_STATUS (EFIAPI *ClearScreen)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This);
    EFI_STATUS (EFIAPI *SetCursorPosition)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, UINTN Column, UINTN Row);
    EFI_STATUS (EFIAPI *EnableCursor)(EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *This, BOOLEAN Visible);
    VOID       *Mode;
};

// Graphics Output Protocol (GOP) Definitions
typedef struct {
    UINT8 Blue;
    UINT8 Green;
    UINT8 Red;
    UINT8 Reserved;
} EFI_GRAPHICS_OUTPUT_BLT_PIXEL;

typedef enum {
    EfiBltVideoFill,
    EfiBltVideoToBltBuffer,
    EfiBltBufferToVideo,
    EfiBltVideoToVideo,
    EfiGraphicsOutputBltOperationMax
} EFI_GRAPHICS_OUTPUT_BLT_OPERATION;

typedef struct {
    UINT32 Version;
    UINT32 HorizontalResolution;
    UINT32 VerticalResolution;
    UINT32 PixelFormat;
    UINT32 PixelInformation[4];
    UINT32 PixelsPerScanLine;
} EFI_GRAPHICS_OUTPUT_MODE_INFORMATION;

typedef struct {
    UINT32 MaxMode;
    UINT32 Mode;
    EFI_GRAPHICS_OUTPUT_MODE_INFORMATION *Info;
    UINTN SizeOfInfo;
    EFI_PHYSICAL_ADDRESS FrameBufferBase;
    UINTN FrameBufferSize;
} EFI_GRAPHICS_OUTPUT_PROTOCOL_MODE;

typedef struct _EFI_GRAPHICS_OUTPUT_PROTOCOL EFI_GRAPHICS_OUTPUT_PROTOCOL;

struct _EFI_GRAPHICS_OUTPUT_PROTOCOL {
    EFI_STATUS (EFIAPI *QueryMode)(EFI_GRAPHICS_OUTPUT_PROTOCOL *This, UINT32 ModeNumber, UINTN *SizeOfInfo, EFI_GRAPHICS_OUTPUT_MODE_INFORMATION **Info);
    EFI_STATUS (EFIAPI *SetMode)(EFI_GRAPHICS_OUTPUT_PROTOCOL *This, UINT32 ModeNumber);
    EFI_STATUS (EFIAPI *Blt)(EFI_GRAPHICS_OUTPUT_PROTOCOL *This, EFI_GRAPHICS_OUTPUT_BLT_PIXEL *BltBuffer, EFI_GRAPHICS_OUTPUT_BLT_OPERATION BltOperation, UINTN SourceX, UINTN SourceY, UINTN DestinationX, UINTN DestinationY, UINTN Width, UINTN Height, UINTN Delta);
    EFI_GRAPHICS_OUTPUT_PROTOCOL_MODE *Mode;
};

typedef struct _EFI_BOOT_SERVICES {
    char Header[24];
    EFI_STATUS (EFIAPI *RaiseTPL)(VOID);
    EFI_STATUS (EFIAPI *RestoreTPL)(VOID);
    EFI_STATUS (EFIAPI *AllocatePages)(EFI_ALLOCATE_TYPE Type, EFI_MEMORY_TYPE MemoryType, UINTN Pages, EFI_PHYSICAL_ADDRESS *Memory);
    EFI_STATUS (EFIAPI *FreePages)(EFI_PHYSICAL_ADDRESS Memory, UINTN Pages);
    EFI_STATUS (EFIAPI *GetMemoryMap)(VOID);
    EFI_STATUS (EFIAPI *AllocatePool)(EFI_MEMORY_TYPE PoolType, UINTN Size, VOID **Buffer);
    EFI_STATUS (EFIAPI *FreePool)(VOID *Buffer);
    char Reserved2[64];
    EFI_STATUS (EFIAPI *WaitForEvent)(UINTN NumberOfEvents, EFI_EVENT *Event, UINTN *Index);
    char Reserved3[24];
    EFI_STATUS (EFIAPI *InstallProtocolInterface)(VOID);
    EFI_STATUS (EFIAPI *ReinstallProtocolInterface)(VOID);
    EFI_STATUS (EFIAPI *UninstallProtocolInterface)(VOID);
    EFI_STATUS (EFIAPI *HandleProtocol)(EFI_HANDLE Handle, EFI_GUID *Protocol, VOID **Interface);
    VOID       *Reserved4;
    EFI_STATUS (EFIAPI *RegisterProtocolNotify)(VOID);
    EFI_STATUS (EFIAPI *LocateHandle)(VOID);
    EFI_STATUS (EFIAPI *LocateDevicePath)(VOID);
    EFI_STATUS (EFIAPI *InstallConfigurationTable)(VOID);
    EFI_STATUS (EFIAPI *LoadImage)(BOOLEAN BootPolicy, EFI_HANDLE ParentImageHandle, VOID *DevicePath, VOID *SourceBuffer, UINTN SourceSize, EFI_HANDLE *ImageHandle);
    EFI_STATUS (EFIAPI *StartImage)(EFI_HANDLE ImageHandle, UINTN *ExitDataSize, CHAR16 **ExitData);
    EFI_STATUS (EFIAPI *Exit)(VOID);
    EFI_STATUS (EFIAPI *UnloadImage)(EFI_HANDLE ImageHandle);
    EFI_STATUS (EFIAPI *ExitBootServices)(EFI_HANDLE ImageHandle, UINTN MapKey);
    char Reserved5[24];
    EFI_STATUS (EFIAPI *Stall)(UINTN Microseconds);
    EFI_STATUS (EFIAPI *SetWatchdogTimer)(UINTN Timeout, UINT64 WatchdogCode, UINTN DataSize, CHAR16 *WatchdogData);
    char Reserved6[16];
    EFI_STATUS (EFIAPI *LocateProtocol)(EFI_GUID *Protocol, VOID *Registration, VOID **Interface);
    EFI_STATUS (EFIAPI *InstallMultipleProtocolInterfaces)(VOID);
    EFI_STATUS (EFIAPI *UninstallMultipleProtocolInterfaces)(VOID);
    char Reserved7[16];
    EFI_STATUS (EFIAPI *LocateHandleBuffer)(UINT32 SearchType, EFI_GUID *Protocol, VOID *SearchKey, UINTN *NoHandles, EFI_HANDLE **Buffer);
} EFI_BOOT_SERVICES;

typedef struct _EFI_SYSTEM_TABLE {
    char Header[24];
    CHAR16 *FirmwareVendor;
    UINT32 FirmwareRevision;
    EFI_HANDLE ConsoleInHandle;
    EFI_SIMPLE_TEXT_INPUT_PROTOCOL *ConIn;
    EFI_HANDLE ConsoleOutHandle;
    EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *ConOut;
    EFI_HANDLE StandardErrorHandle;
    EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL *StdErr;
    EFI_RUNTIME_SERVICES *RuntimeServices;
    EFI_BOOT_SERVICES *BootServices;
    UINTN NumberOfTableEntries;
    VOID *ConfigurationTable;
} EFI_SYSTEM_TABLE;

// File Protocol Definitions
typedef struct _EFI_FILE_PROTOCOL EFI_FILE_PROTOCOL;

#define EFI_FILE_MODE_READ   0x0000000000000001ULL
#define EFI_FILE_MODE_WRITE  0x0000000000000002ULL
#define EFI_FILE_MODE_CREATE 0x8000000000000000ULL

struct _EFI_FILE_PROTOCOL {
    UINT64 Revision;
    EFI_STATUS (EFIAPI *Open)(EFI_FILE_PROTOCOL *This, EFI_FILE_PROTOCOL **NewHandle, CHAR16 *FileName, UINT64 OpenMode, UINT64 Attributes);
    EFI_STATUS (EFIAPI *Close)(EFI_FILE_PROTOCOL *This);
    EFI_STATUS (EFIAPI *Delete)(EFI_FILE_PROTOCOL *This);
    EFI_STATUS (EFIAPI *Read)(EFI_FILE_PROTOCOL *This, UINTN *BufferSize, VOID *Buffer);
    EFI_STATUS (EFIAPI *Write)(EFI_FILE_PROTOCOL *This, UINTN *BufferSize, VOID *Buffer);
    EFI_STATUS (EFIAPI *GetPosition)(EFI_FILE_PROTOCOL *This, UINT64 *Position);
    EFI_STATUS (EFIAPI *SetPosition)(EFI_FILE_PROTOCOL *This, UINT64 Position);
    EFI_STATUS (EFIAPI *GetInfo)(EFI_FILE_PROTOCOL *This, EFI_GUID *InformationType, UINTN *BufferSize, VOID *Buffer);
    EFI_STATUS (EFIAPI *SetInfo)(EFI_FILE_PROTOCOL *This, EFI_GUID *InformationType, UINTN BufferSize, VOID *Buffer);
    EFI_STATUS (EFIAPI *Flush)(EFI_FILE_PROTOCOL *This);
};

typedef struct _EFI_SIMPLE_FILE_SYSTEM_PROTOCOL {
    UINT64 Revision;
    EFI_STATUS (EFIAPI *OpenVolume)(struct _EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *This, EFI_FILE_PROTOCOL **Root);
} EFI_SIMPLE_FILE_SYSTEM_PROTOCOL;

#endif // _UEFI_H_

// Builtin freestanding memory routines required by Clang / MSVC
void *memset(void *s, int c, size_t n);
void *memcpy(void *dest, const void *src, size_t n);