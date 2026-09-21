namespace DeployManager.Services
{
    public class GraphicsHardwareInfo
    {
        public string GpuName { get; set; } = "Unknown GPU";
        public string ProviderName { get; set; } = "Unknown";
        public string DriverVersion { get; set; } = "Unknown";
        public string DriverDate { get; set; } = "Unknown";
        public string DriverPath { get; set; } = "Unknown";
        public string InfPath { get; set; } = "Unknown";
        public string MatchingDeviceId { get; set; } = "Unknown";
        public bool UefiGopSupported { get; set; }
        public string UefiGopStatus { get; set; } = "Undetermined";
    }
}
