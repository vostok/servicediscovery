using System;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery
{
    internal class PathBuilder
    {
        private readonly string prefix;

        public PathBuilder(string prefix)
        {
            this.prefix = prefix ?? ZooKeeperPath.Root;
        }

        public string BuildEnvironmentPath(string environment) => 
            ZooKeeperPath.Combine(prefix, Escape(environment.ToLowerInvariant()));

        public string BuildServicePath(string environment, string service) => 
            ZooKeeperPath.Combine(BuildEnvironmentPath(environment), Escape(service));

        public string BuildReplicaPath(string environment, string service, string replica) => 
            ZooKeeperPath.Combine(BuildServicePath(environment, service), Escape(replica));

        private static string Escape(string segment) =>
            Uri.EscapeDataString(segment);

        private static string Unescape(string segment) =>
            Uri.UnescapeDataString(segment);
    }
}