using System;
using System.Collections.Generic;

namespace DeployManager.Services.Diagnostics
{
    public class HealthCheckItem
    {
        public string ComponentName { get; set; } = "";
        public bool Passed { get; set; }
        public string Details { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public string RemediationAdvice { get; set; } = "";
    }

    public class SystemHealthReport
    {
        public bool IsOverallHealthy => TotalFailed == 0;
        public int TotalPassed { get; set; }
        public int TotalFailed { get; set; }
        public int TotalWarnings { get; set; }
        public List<HealthCheckItem> Checks { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string GetDiagnosticSummary()
        {
            if (IsOverallHealthy)
            {
                return "ALL SYSTEMS OPERATIONAL (100% HEALTHY)";
            }
            return $"{TotalFailed} CRITICAL ISSUE(S) DETECTED";
        }

        public string GetDetailedFailureReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== SYSTEM DIAGNOSTIC ANALYSIS ===");
            foreach (var check in Checks)
            {
                if (!check.Passed)
                {
                    sb.AppendLine($"[!] FAILED: {check.ComponentName}");
                    sb.AppendLine($"    Cause : {check.ErrorMessage}");
                    sb.AppendLine($"    Action: {check.RemediationAdvice}");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
