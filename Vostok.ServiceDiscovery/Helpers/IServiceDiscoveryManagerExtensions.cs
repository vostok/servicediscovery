using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class IServiceDiscoveryManagerExtensions
    {
        public static async Task<bool> SetNewReplicaTags(this IServiceDiscoveryManager serviceDiscoveryManager, string environment, string application, string replicaName, TagCollection tags)
        {
            return await serviceDiscoveryManager.TryUpdateApplicationPropertiesAsync(
                    environment,
                    application,
                    properties => properties.SetEphemeralReplicaTags(replicaName, tags))
                .ConfigureAwait(false);
        }
        
        [NotNull]
        private static IApplicationInfoProperties SetEphemeralReplicaTags([NotNull] this IApplicationInfoProperties properties, string replicaName, TagCollection tags)
            => tags?.Count > 0
                ? properties.Set(GetEphemeralReplicaTagsPropertyKey(replicaName), tags.ToString())
                : properties.Remove(GetEphemeralReplicaTagsPropertyKey(replicaName));

        [NotNull]
        private static string GetEphemeralReplicaTagsPropertyKey(string replicaName)
            => new TagPropertyKey(replicaName, "ephemeral").ToString();
    }
}