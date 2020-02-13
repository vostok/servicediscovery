using System;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
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
                $@"^{this.prefix}(/(?<{PathTokens.Environment}>[^/]+)(/(?<{PathTokens.Application}>[^/]+)(/(?<{PathTokens.Replica}>[^/]+))?)?)?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        }

        public string Escape(string segment) =>
            pathEscaper.Escape(segment);

        public string Unescape(string segment) =>
            pathEscaper.Unescape(segment);

        public string BuildEnvironmentPath([NotNull] string environment) =>
            $"{prefix}/{Escape(environment.ToLowerInvariant())}";

        public string BuildApplicationPath([NotNull] string environment, [NotNull] string application) =>
            ZooKeeperPath.Combine(BuildEnvironmentPath(environment), Escape(application));

        public string BuildReplicaPath([NotNull] string environment, [NotNull] string application, [NotNull] string replica) =>
            ZooKeeperPath.Combine(BuildApplicationPath(environment, application), Escape(replica));

        public (string environment, string application, string replica)? TryParse(string path)
        {
            if (path == null)
                return null;

            var match = pathRegex.Match(path);

            if (!match.Success)
                return null;

            return (ExtractToken(match, PathTokens.Environment), ExtractToken(match, PathTokens.Application), ExtractToken(match, PathTokens.Replica));
        }

        private string ExtractToken(Match match, string key)
        {
            var token = match.Groups[key].Value;
            return token == "" ? null : Unescape(token);
        }

        private static class PathTokens
        {
            public const string Environment = "environment";
            public const string Application = "application";
            public const string Replica = "replica";
        }
    }
}