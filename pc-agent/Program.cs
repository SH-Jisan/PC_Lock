using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PC.SecurityAgent.Services;

namespace PC.SecurityAgent
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
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
