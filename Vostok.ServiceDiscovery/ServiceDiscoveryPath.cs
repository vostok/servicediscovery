using System;
using System.Text.RegularExpressions;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery
{
    internal class ServiceDiscoveryPath
    {
        public readonly string Prefix;
        private readonly Regex pathRegex;

        public ServiceDiscoveryPath(string prefix)
        {
            Prefix = ZooKeeperPath.Combine(ZooKeeperPath.Root, prefix?.TrimEnd('/') ?? "");

            pathRegex = new Regex(
                $@"^{Prefix}(/(?<environment>[^/]+)(/(?<application>[^/]+)(/(?<replica>[^/]+))?)?)?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        }

        public static string Escape(string segment) =>
            Uri.EscapeDataString(segment);

        public static string Unescape(string segment) =>
            Uri.UnescapeDataString(segment);

        public string BuildEnvironmentPath(string environment) =>
            ZooKeeperPath.Combine(Prefix, Escape(environment.ToLowerInvariant()));

        public string BuildApplicationPath(string environment, string application) =>
            ZooKeeperPath.Combine(BuildEnvironmentPath(environment), Escape(application));

        public string BuildReplicaPath(string environment, string application, string replica) =>
            ZooKeeperPath.Combine(BuildApplicationPath(environment, application), Escape(replica));

        public (string environment, string application, string replica)? TryParse(string path)
        {
            var match = pathRegex.Match(path);

            if (!match.Success)
                return null;

            return (ExtractToken(match, "environment"), ExtractToken(match, "application"), ExtractToken(match, "replica"));
        }

        private static string ExtractToken(Match match, string key)
        {
            var token = match.Groups[key].Value;
            return token == "" ? null : Unescape(token);
        }
    }
}