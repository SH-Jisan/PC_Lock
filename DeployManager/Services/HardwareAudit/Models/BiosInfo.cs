namespace DeployManager.Services
{
    public class BiosInfo
    {
        public string Vendor { get; set; } = "Unknown";
        public string Version { get; set; } = "Unknown";
        public string ReleaseDate { get; set; } = "Unknown";
        public string MajorRelease { get; set; } = "Unknown";
        public string MinorRelease { get; set; } = "Unknown";
        public string BootMode { get; set; } = "Unknown"; // UEFI Native, Legacy BIOS, Unknown
        public bool IsUefi { get; set; }
        public bool SecureBootEnabled { get; set; }
        public string SecureBootStatus { get; set; } = "Unknown";
    }
}
