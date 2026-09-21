using System.Collections.Generic;

namespace DeployManager.Services
{
    public class PrebootAssetsDiagnostics
    {
        public bool GraphicsGopReady { get; set; }
        public string GraphicsGopDetail { get; set; } = "";
        public bool NetworkUndiReady { get; set; }
        public string NetworkUndiDetail { get; set; } = "";
        public bool StorageBlockIoReady { get; set; }
        public string StorageBlockIoDetail { get; set; } = "";
        public bool EfiPartitionReady { get; set; }
        public string EfiPartitionDetail { get; set; } = "";
        public bool AllPrebootAssetsReady { get; set; }
        public string ReadinessScore { get; set; } = "Pending";
        public List<string> DiagnosticChecklist { get; set; } = new();
    }
}
