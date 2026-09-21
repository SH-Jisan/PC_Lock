using System.Collections.Generic;

namespace DeployManager.Services
{
    public class CompatibilityAssessment
    {
        public bool Is64BitCompatible { get; set; }
        public bool IsBootModeCompatible { get; set; }
        public bool IsNetworkCompatible { get; set; }
        public bool IsTpmPresent { get; set; }
        public string RecommendedMode { get; set; } = "Enterprise Zero-Risk (Recommended)";
        public string CompatibilityScore { get; set; } = "100% COMPATIBLE";
        public bool IsFullyCompatible { get; set; }
        public List<string> CompatibilityNotes { get; set; } = new();
    }
}
