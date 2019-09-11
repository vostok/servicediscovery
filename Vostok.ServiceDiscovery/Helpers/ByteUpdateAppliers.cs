using System;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;

namespace Vostok.ServiceDiscovery.Helpers
{
    public class ByteUpdateAppliers
    {
        public static byte[] ApplyEnvironmentParentUpdate(string environment, string newParent, byte[] bytes)
        {
            var environmentInfo = EnvironmentNodeDataSerializer.Deserialize(environment, bytes);

            return EnvironmentNodeDataSerializer.Serialize(new EnvironmentInfo(environment, newParent, environmentInfo.Properties));
        }

        public static byte[] ApplyEnvironmentPropertiesUpdate(string environment, Func<IEnvironmentInfoProperties, IEnvironmentInfoProperties> updateFunc, byte[] bytes)
        {
            var environmentInfo = EnvironmentNodeDataSerializer.Deserialize(environment, bytes);

            var newProperties = updateFunc(environmentInfo.Properties);

            return EnvironmentNodeDataSerializer.Serialize(new EnvironmentInfo(environment, environmentInfo.ParentEnvironment, newProperties));
        }

        public static byte[] ApplyApplicationPropertiesUpdate(string environment, string application, Func<IApplicationInfoProperties, IApplicationInfoProperties> updateFunc, byte[] bytes)
        {
            var environmentInfo = ApplicationNodeDataSerializer.Deserialize(environment, application, bytes);

            var newProperties = updateFunc(environmentInfo.Properties);

            return ApplicationNodeDataSerializer.Serialize(new ApplicationInfo(environment, application, newProperties));
        }
    }
}