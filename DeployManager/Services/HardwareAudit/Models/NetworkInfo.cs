using System.Collections.Generic;

namespace DeployManager.Services
{
    public class NetworkAdapterDetails
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string InterfaceType { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string OperationalStatus { get; set; } = "";
        public bool IsPhysical { get; set; }
        public string DriverProvider { get; set; } = "Unknown";
        public string DriverVersion { get; set; } = "Unknown";
        public string DriverDate { get; set; } = "Unknown";
        public string DriverPath { get; set; } = "Unknown";
        public string MatchingDeviceId { get; set; } = "Unknown";
        public string ServiceName { get; set; } = "Unknown";
        public bool PrebootUndiSupported { get; set; }
        public string PrebootNetworkStatus { get; set; } = "Undetermined";
        public List<string> IpAddresses { get; set; } = new();
        public List<string> Gateways { get; set; } = new();
        public List<string> DnsServers { get; set; } = new();
    }

    public class NetworkConnectivityInfo
    {
        public bool HasActiveNetwork { get; set; }
        public string PrimaryMacAddress { get; set; } = "Unknown";
        public string PrimaryIpAddress { get; set; } = "Unknown";
        public string PrimaryGateway { get; set; } = "Unknown";
        public string PrimaryDns { get; set; } = "Unknown";
        public string PrimaryAdapterName { get; set; } = "Unknown";
        public string PrimaryDriverPath { get; set; } = "Unknown";
        public bool InternetReachable { get; set; }
        public string InternetStatus { get; set; } = "Checking...";
        public List<NetworkAdapterDetails> AllAdapters { get; set; } = new();
    }
}
