using PC.SecurityAgent.Controllers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PC.SecurityAgent.LockEngine;
using PC.SecurityAgent.Services;

namespace PC.SecurityAgent
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Contains("--test-lock"))
            {
                BootGuardHealer.IsPreBootEnabled = false; // Safe desktop GUI test mode
                Console.WriteLine("==================================================================");
                Console.WriteLine("  HYBRID CUSTOM LOCK ENGINE - SAFE WINDOW DESKTOP TEST MODE");
                Console.WriteLine("  (Pre-boot firmware modifications are 100% NEUTRALIZED)");
                Console.WriteLine("==================================================================");
                Console.WriteLine("[*] Launching Custom Cyber Lock Screen in 1 second...");
                Console.WriteLine("[*] Master PINs to unlock: 998877, SHJ, or 123456");
                Console.WriteLine("==================================================================");
                await Task.Delay(1000);
                LockEngineCoordinator.ShowLockScreen();

                // Keep process alive while lock screen is active
                while (LockEngineCoordinator.IsLocked)
                {
                    await Task.Delay(300);
                }

                Console.WriteLine("\n[SUCCESS] Custom Lock Screen unlocked and dismissed cleanly!");
                Console.WriteLine("Test completed successfully.");
                return;
            }

            IHost host = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "PCSecurityAgentService";
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<SecurityService>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
