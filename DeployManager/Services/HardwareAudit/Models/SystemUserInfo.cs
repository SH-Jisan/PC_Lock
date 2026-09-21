namespace DeployManager.Services
{
    public class SystemUserInfo
    {
        public string UserName { get; set; } = "Unknown";
        public string UserDomain { get; set; } = "Unknown";
        public string MachineName { get; set; } = "Unknown";
        public string OsDescription { get; set; } = "Unknown";
        public string OsArchitecture { get; set; } = "Unknown";
        public string ProcessorName { get; set; } = "Unknown";
        public int ProcessorCount { get; set; }
        public string TotalPhysicalMemoryMb { get; set; } = "Unknown";
    }
}
