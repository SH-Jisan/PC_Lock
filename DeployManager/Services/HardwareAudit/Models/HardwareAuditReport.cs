using System;
using System.Text.Json;

namespace DeployManager.Services
{
    public class HardwareAuditReport
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public MotherboardInfo Motherboard { get; set; } = new();
        public BiosInfo Bios { get; set; } = new();
        public SystemUserInfo SystemUser { get; set; } = new();
        public GraphicsHardwareInfo Graphics { get; set; } = new();
        public NetworkConnectivityInfo Network { get; set; } = new();
        public StorageHardwareInfo Storage { get; set; } = new();
        public EfiPrebootEnvironmentInfo EfiPreboot { get; set; } = new();
        public PrebootAssetsDiagnostics PrebootDiagnostics { get; set; } = new();
        public CompatibilityAssessment Assessment { get; set; } = new();

        public string ToJson(bool indented = true)
        {
            var options = new JsonSerializerOptions { WriteIndented = indented };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
