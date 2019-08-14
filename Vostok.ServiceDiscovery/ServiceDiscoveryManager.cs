using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Extensions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery
{
    [PublicAPI]
    public class ServiceDiscoveryManager : IServiceDiscoveryManager
    {
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceDiscoveryManagerSettings settings;
        private readonly ILog log;

        private readonly ServiceDiscoveryPathHelper pathHelper;

        public ServiceDiscoveryManager(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [CanBeNull] ServiceDiscoveryManagerSettings settings = null,
            [CanBeNull] ILog log = null)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.settings = settings ?? new ServiceDiscoveryManagerSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceDiscoveryManager>();

            pathHelper = new ServiceDiscoveryPathHelper(this.settings.ZooKeeperNodesPrefix, this.settings.ZooKeeperNodesPathEscaper);
        }

        public async Task<IReadOnlyList<string>> GetAllEnvironmentsAsync()
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(settings.ZooKeeperNodesPrefix));
            data.EnsureSuccess();
            return data.ChildrenNames;
        }

        public async Task<IReadOnlyList<string>> GetAllApplicationsAsync(string environment)
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(pathHelper.BuildEnvironmentPath(environment)));
            data.EnsureSuccess();
            return data.ChildrenNames;
        }

        public async Task<string> GetParentZoneAsync(string environment)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildEnvironmentPath(environment)));
            data.EnsureSuccess();
            var envData = EnvironmentNodeDataSerializer.Deserialize(data.Data);
            return envData.ParentEnvironment;
        }

        public async Task<bool> TryAddNode(string environment, string parent)
        {
            var environmentInfo = new EnvironmentInfo(parent, null);
            var createRequest = new CreateRequest(pathHelper.BuildEnvironmentPath(environment), CreateMode.Persistent)
            {
                Data = EnvironmentNodeDataSerializer.Serialize(environmentInfo)
            };

            return (await zooKeeperClient.CreateAsync(createRequest)).IsSuccessful;
        }

        public async Task<bool> TryDeleteNode(string environment)
        {
            var path = pathHelper.BuildEnvironmentPath(environment);
            if(!(await CheckZoneExistence(path)))
                return false;

            var deleteRequest = new DeleteRequest(path)
            {
                DeleteChildrenIfNeeded = true
            };

            return (await zooKeeperClient.DeleteAsync(deleteRequest)).IsSuccessful;
        }

        public async Task<bool> AddToBlacklist(string environment, string application, Uri replicaUri)
        {
            return await UpdateTopologyData(
                environment,
                application,
                properties =>
                {
                    var blacklist = properties.GetBlacklist();
                    if(blacklist.Contains(replicaUri))
                        return properties;

                    var newBlackList = blacklist.Concat(new[] { replicaUri });
                    return properties.SetBlacklist(newBlackList);
                });
        }

        public async Task<bool> RemoveFromBlacklist(string environment, string application, Uri replicaUri)
        {
            return await UpdateTopologyData(
                environment,
                application,
                properties =>
                {
                    var blacklist = properties.GetBlacklist();
                    if(!blacklist.Contains(replicaUri))
                        return properties;

                    var newBlackList = blacklist.Where(x => x != replicaUri);
                    return properties.SetBlacklist(newBlackList);
                });
        }

        public async Task<bool> SetExternalUrl(string environment, string application, Uri externalUrl)
        {
            return await UpdateTopologyData(
                environment,
                application,
                properties =>
                {
                    return properties.SetExternalUrl(externalUrl);
                });
        }

        public async Task<bool> CheckZoneExistence(string path)
        {
            var result = await zooKeeperClient.ExistsAsync(path);
            if(result.IsSuccessful)
                return result.Stat != null;

            return result.IsSuccessful;
        }

        private async Task<bool> UpdateTopologyData(string environment, string application, Func<IServiceTopologyProperties, IServiceTopologyProperties> updateFunc)
        {
            var topologyPath = pathHelper.BuildApplicationPath(environment, application);

            const int topologyUpdateAttempts = 5;

            for(var i = 0; i < topologyUpdateAttempts; i++)
            {
                var readResult = zooKeeperClient.GetData(topologyPath);
                if(!readResult.IsSuccessful)
                   continue;

                var topologyData = ApplicationNodeDataSerializer.Deserialize(readResult.Data);
                IServiceTopologyProperties properties = new ServiceTopologyProperties(topologyData.Properties);

                properties = updateFunc(properties);
                var data = ApplicationNodeDataSerializer.Serialize(new ApplicationInfo(properties.ToDictionary(x => x.Key, y => y.Value)));
                var request = new SetDataRequest(topologyPath, data)
                {
                    Version = readResult.Stat.Version
                };

                var updateResult = await zooKeeperClient.SetDataAsync(request);

                if(updateResult.Status == ZooKeeperStatus.VersionsMismatch)
                {
                    continue;
                }

                return updateResult.IsSuccessful;
            }
            return false;
        }
    }
}