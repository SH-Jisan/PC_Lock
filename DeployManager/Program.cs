using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeployManager.Services;

namespace DeployManager
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("--audit-only", StringComparison.OrdinalIgnoreCase))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("\n==================================================================");
                Console.WriteLine("  PRE-FLIGHT HARDWARE AUDIT & COMPATIBILITY REPORT");
                Console.WriteLine("==================================================================");
                var report = HardwareAuditService.RunAudit();
                Console.WriteLine(report.ToJson(true));
                Console.WriteLine("==================================================================");
                Console.WriteLine($"[ASSESSMENT] Compatibility: {report.Assessment.CompatibilityScore}");
                Console.WriteLine($"[RECOMMENDED] {report.Assessment.RecommendedMode}");
                foreach (var note in report.Assessment.CompatibilityNotes)
                {
                    Console.WriteLine($"  {note}");
                }
                Console.WriteLine("==================================================================\n");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
