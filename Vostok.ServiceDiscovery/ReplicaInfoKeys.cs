using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    [PublicAPI]
    public static class ReplicaInfoKeys
    {
        public const string Environment = "Zone";
        public const string Application = "Service";
        public const string Replica = "Instance name";

        public const string Url = "Url";
        public const string Host = "Host";
        public const string Port = "Port";

        public const string ProcessName = "Process name";
        public const string ProcessId = "Process id";
        public const string BaseDirectory = "Directory";

        public const string CommitHash = "Commit hash";
        public const string ReleaseDate = "Release date";

        public const string Dependencies = "Dependencies";
    }
}