using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class IServiceDiscoveryManagerExtensions
    {
        public static async Task<bool> ClearReplicaTags(this IServiceDiscoveryManager serviceDiscoveryManager, string environment, string application, string replicaName)
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
        private static IApplicationInfoProperties SetEphemeralReplicaTags([NotNull] this IApplicationInfoProperties properties, [NotNull] string replicaName, TagCollection tags)
        {
            var propertyKey = new TagsPropertyKey(replicaName, "Ephemeral");
            if (properties.GetReplicaKindTags(propertyKey).Equals(tags))
                return properties;

            return tags?.Count > 0
                ? properties.Set(propertyKey.ToString(), tags.ToString())
                : properties.Remove(propertyKey.ToString());
        }

        [NotNull]
        private static TagCollection GetReplicaKindTags([NotNull] this IReadOnlyDictionary<string, string> properties, [NotNull] TagsPropertyKey tagsPropertyKey)
            => properties.TryGetValue(tagsPropertyKey.ToString(), out var collectionString)
                ? TagCollection.TryParse(collectionString, out var collection)
                    ? collection
                    : new TagCollection()
                : new TagCollection();
    }
}