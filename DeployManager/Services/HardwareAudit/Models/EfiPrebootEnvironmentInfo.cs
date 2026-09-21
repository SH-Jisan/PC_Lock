namespace DeployManager.Services
{
    public class EfiPrebootEnvironmentInfo
    {
        public string EspVolumeGuid { get; set; } = "Undetected";
        public string FirmwareBootDevice { get; set; } = "Unknown";
        public string SystemBootDevice { get; set; } = "Unknown";
        public bool HasStandardBootloader { get; set; }
        public string StandardBootloaderPath { get; set; } = @"\EFI\Microsoft\Boot\bootmgfw.efi";
        public bool HasHiddenBootloader { get; set; }
        public string HiddenBootloaderPath { get; set; } = @"\EFI\Microsoft\Boot\bootmgfw_hidden.efi";
        public bool HasFallbackBootx64 { get; set; }
        public string FallbackBootx64Path { get; set; } = @"\EFI\Boot\bootx64.efi";
        public bool HasPrebootEfi { get; set; }
        public string PrebootEfiPath { get; set; } = @"\EFI\PCLock\pc_lock_preboot.efi";
        public bool NvramVariablesSupported { get; set; }
        public bool PrebootReady { get; set; }
        public string ReadinessSummary { get; set; } = "Checking...";
    }
}
