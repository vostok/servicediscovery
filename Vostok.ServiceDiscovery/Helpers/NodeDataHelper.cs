using System;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class NodeDataHelper
    {
        public static byte[] SetEnvironmentParent(string environment, string newParent, byte[] bytes)
        {
            var environmentInfo = EnvironmentNodeDataSerializer.Deserialize(environment, bytes);

            return EnvironmentNodeDataSerializer.Serialize(new EnvironmentInfo(environment, newParent, environmentInfo.Properties));
        }

        public static byte[] SetEnvironmentProperties(string environment, Func<IEnvironmentInfoProperties, IEnvironmentInfoProperties> update, byte[] bytes)
        {
            var environmentInfo = EnvironmentNodeDataSerializer.Deserialize(environment, bytes);

            var newProperties = update(environmentInfo.Properties);

            return EnvironmentNodeDataSerializer.Serialize(new EnvironmentInfo(environment, environmentInfo.ParentEnvironment, newProperties));
        }

        public static byte[] SetApplicationProperties(string environment, string application, Func<IApplicationInfoProperties, IApplicationInfoProperties> update, byte[] bytes)
        {
            var environmentInfo = ApplicationNodeDataSerializer.Deserialize(environment, application, bytes);

            var newProperties = update(environmentInfo.Properties);

            return ApplicationNodeDataSerializer.Serialize(new ApplicationInfo(environment, application, newProperties));
        }
    }
}