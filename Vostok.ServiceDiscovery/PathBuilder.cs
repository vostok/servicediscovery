using System;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery
{
    internal class PathBuilder
    {
        public readonly string Prefix;

        public PathBuilder(string prefix)
        {
            Prefix = prefix ?? ZooKeeperPath.Root;
        }
        
        public string BuildEnvironmentPath(string environment) =>
            ZooKeeperPath.Combine(Prefix, Escape(environment.ToLowerInvariant()));

        public string BuildApplicationPath(string environment, string application) =>
            ZooKeeperPath.Combine(BuildEnvironmentPath(environment), Escape(application));

        public string BuildReplicaPath(string environment, string application, string replica) =>
            ZooKeeperPath.Combine(BuildApplicationPath(environment, application), Escape(replica));

        private static string Escape(string segment) =>
            Uri.EscapeDataString(segment);

        private static string Unescape(string segment) =>
            Uri.UnescapeDataString(segment);
    }
}