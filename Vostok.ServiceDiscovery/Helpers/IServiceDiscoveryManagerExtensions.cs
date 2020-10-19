using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class IServiceDiscoveryManagerExtensions
    {
        public static async Task<bool> SetNewReplicaTags(this IServiceDiscoveryManager serviceDiscoveryManager, string environment, string application, string replicaName, ITag[] tags)
        {
            return await serviceDiscoveryManager.TryUpdateApplicationPropertiesAsync(
                    environment,
                    application,
                    properties => properties.SetEphemeralReplicaTags(replicaName, tags))
                .ConfigureAwait(false);
        }
        
        [NotNull]
        private static IApplicationInfoProperties SetEphemeralReplicaTags([NotNull] this IApplicationInfoProperties properties, string replicaName, ITag[] tags)
        {
            var propertyName = GetEphemeralReplicaTagsPropertyKey(replicaName);
            return tags.Length == 0 
                ? properties.Remove(propertyName) 
                : properties.Set(propertyName, ReplicaTagsHelpers.Serialize(tags));
        }

        [NotNull]
        private static string GetEphemeralReplicaTagsPropertyKey(string replicaName)
            => ReplicaTagsHelpers.GetReplicaTagsPropertyKey(replicaName + ":" + "ephemeral");
    }
}