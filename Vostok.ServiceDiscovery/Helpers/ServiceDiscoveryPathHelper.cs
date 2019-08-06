using System;
using System.Text.RegularExpressions;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class ServiceDiscoveryPathHelper
    {
        private readonly string prefix;
        private readonly IZooKeeperPathEscaper pathEscaper;
        private readonly Regex pathRegex;

        public ServiceDiscoveryPathHelper(string prefix, IZooKeeperPathEscaper pathEscaper)
        {
            this.pathEscaper = pathEscaper ?? throw new ArgumentNullException(nameof(pathEscaper));

            if (string.IsNullOrEmpty(prefix) || prefix == ZooKeeperPath.Root)
                this.prefix = "";
            else
                this.prefix = $"/{prefix.Trim('/')}";

            pathRegex = new Regex(
                $@"^{this.prefix}(/(?<environment>[^/]+)(/(?<application>[^/]+)(/(?<replica>[^/]+))?)?)?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        }

        public string Escape(string segment) =>
            pathEscaper.Escape(segment);

        public string Unescape(string segment) =>
            pathEscaper.Unescape(segment);

        public string BuildEnvironmentPath(string environment) =>
            $"{prefix}/{Escape(environment.ToLowerInvariant())}";

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

        private string ExtractToken(Match match, string key)
        {
            var token = match.Groups[key].Value;
            return token == "" ? null : Unescape(token);
        }
    }
}