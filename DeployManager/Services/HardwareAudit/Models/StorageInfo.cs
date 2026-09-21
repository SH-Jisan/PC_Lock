namespace DeployManager.Services
{
    public class StorageHardwareInfo
    {
        public string PrimaryControllerName { get; set; } = "Unknown Controller";
        public string ControllerType { get; set; } = "Unknown"; // NVMe, SATA AHCI, RAID
        public string DriverVersion { get; set; } = "Unknown";
        public string DriverPath { get; set; } = "Unknown";
        public string PrimaryDiskName { get; set; } = "Unknown Disk";
        public string PartitionStyle { get; set; } = "GPT (GUID Partition Table)"; // GPT, MBR
        public string BusType { get; set; } = "NVMe / SATA";
        public bool PrebootStorageSupported { get; set; }
        public string PrebootStorageStatus { get; set; } = "Undetermined";
    }
}
