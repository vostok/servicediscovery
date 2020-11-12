using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class IServiceDiscoveryManagerExtensions
    {
        public static async Task<bool> RemoveReplicaTags(this IServiceDiscoveryManager serviceDiscoveryManager, string environment, string application, string replicaName)
            => await serviceDiscoveryManager.SetReplicaTags(environment, application, replicaName, new TagCollection()).ConfigureAwait(false);
        
        public static async Task<bool> SetReplicaTags(this IServiceDiscoveryManager serviceDiscoveryManager, string environment, string application, string replicaName, TagCollection tags)
        {
            return await serviceDiscoveryManager.TryUpdateApplicationPropertiesAsync(
                    environment,
                    application,
                    properties => properties.SetEphemeralReplicaTags(replicaName, tags))
                .ConfigureAwait(false);
        }

        [NotNull]
        private static IApplicationInfoProperties SetEphemeralReplicaTags([NotNull] this IApplicationInfoProperties properties, string replicaName, TagCollection tags)
        {
            var propertyKey = GetEphemeralReplicaTagsPropertyKey(replicaName);
            if (properties.GetEphemeralReplicaTags(propertyKey).Equals(tags))
                return properties;

            return tags?.Count > 0
                ? properties.Set(propertyKey, tags.ToString())
                : properties.Remove(propertyKey);
        }

        [NotNull]
        private static TagCollection GetEphemeralReplicaTags([NotNull] this IReadOnlyDictionary<string, string> properties, [NotNull] string propertyKey)
            => properties.TryGetValue(propertyKey, out var value)
               && TagCollection.TryParse(value, out var tagCollection)
                ? tagCollection
                : new TagCollection();

        [NotNull]
        private static string GetEphemeralReplicaTagsPropertyKey(string replicaName)
            => new TagsPropertyKey(replicaName, "ephemeral").ToString();
    }
}