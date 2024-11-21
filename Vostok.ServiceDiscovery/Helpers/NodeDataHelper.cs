using System;
using System.Collections.Generic;
using System.Linq;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;
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
            var applicationInfo = ApplicationNodeDataSerializer.Deserialize(environment, application, bytes);

            var newProperties = update(applicationInfo.Properties);

            return ApplicationNodeDataSerializer.Serialize(new ApplicationInfo(environment, application, newProperties));
        }
        
        public static byte[] SetReplicaProperties(string environment, string application, string replica, Func<Dictionary<string, string>, Dictionary<string, string>> update,
                                                  byte[] bytes)
        {
            var applicationInfo = ReplicaNodeDataSerializer.Deserialize(environment, application, replica, bytes);
            var propertiesCopy = new Dictionary<string, string>(applicationInfo.Properties.ToDictionary(x => x.Key, x => x.Value));
            var newProperties = update(propertiesCopy);
            
            return ReplicaNodeDataSerializer.Serialize(new ReplicaInfo(environment, application, replica, newProperties));
        }

        public static byte[] SetReplicaProperties(string environment, string application, string replica, Dictionary<string, string> update)
        {
            return ReplicaNodeDataSerializer.Serialize(new ReplicaInfo(environment, application, replica, update));
        }
    }
}