using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
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
            this.zooKeeperClient = zooKeeperClient ?? throw new ArgumentNullException(nameof(zooKeeperClient));
            this.settings = settings ?? new ServiceDiscoveryManagerSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceDiscoveryManager>();

            pathHelper = new ServiceDiscoveryPathHelper(this.settings.ZooKeeperNodesPrefix, this.settings.ZooKeeperNodesPathEscaper);
        }

        // CR(kungurtsev): NodeNotFound -> empty list.
        public async Task<IReadOnlyList<string>> GetAllEnvironmentsAsync()
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(settings.ZooKeeperNodesPrefix));
            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        // CR(kungurtsev): NodeNotFound -> ?.
        public async Task<IReadOnlyList<string>> GetAllApplicationsAsync([NotNull] string environment)
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(pathHelper.BuildEnvironmentPath(environment)));
            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        // CR(kungurtsev): NodeNotFound -> null.
        public async Task<IEnvironmentInfo> GetEnvironmentAsync([NotNull] string environment)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildEnvironmentPath(environment)));
            data.EnsureSuccess();
            var envData = EnvironmentNodeDataSerializer.Deserialize(environment, data.Data);
            return envData;
        }

        // CR(kungurtsev): NodeNotFound -> null.
        public async Task<IApplicationInfo> GetApplicationAsync([NotNull] string environment, [NotNull] string application)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildApplicationPath(environment, application)));
            data.EnsureSuccess();
            var appData = ApplicationNodeDataSerializer.Deserialize(environment, application, data.Data);
            return appData;
        }

        public async Task<bool> TryAddEnvironmentAsync(IEnvironmentInfo environmentInfo)
        {
            var createRequest = new CreateRequest(pathHelper.BuildEnvironmentPath(environmentInfo.Environment), CreateMode.Persistent)
            {
                Data = EnvironmentNodeDataSerializer.Serialize(environmentInfo)
            };

            return (await zooKeeperClient.CreateAsync(createRequest)).IsSuccessful;
        }

        public async Task<bool> TryDeleteEnvironmentAsync([NotNull] string environment)
        {
            var path = pathHelper.BuildEnvironmentPath(environment);
            // CR(kungurtsev): why should we check first?
            if (!(await CheckZoneExistenceAsync(path)))
                return false;

            var deleteRequest = new DeleteRequest(path)
            {
                DeleteChildrenIfNeeded = true
            };

            return (await zooKeeperClient.DeleteAsync(deleteRequest)).IsSuccessful;
        }

        // CR(kungurtsev): GetEnvironmentAsync is null. Remove this.
        internal async Task<bool> CheckZoneExistenceAsync([NotNull] string path)
        {
            var result = await zooKeeperClient.ExistsAsync(path);
            if (result.IsSuccessful)
                return result.Stat != null;

            return false;
        }

        // CR(kungurtsev): add helper that modify zookeeper node bytes. Possibly as extension to vostok.zookeeper.abstractions.
        public async Task<bool> TryUpdateApplicationPropertiesAsync([NotNull] string environment, [NotNull] string application, Func<IServiceTopologyProperties, IServiceTopologyProperties> updateFunc)
        {
            var applicationPath = pathHelper.BuildApplicationPath(environment, application);

            // CR(kungurtsev): move to settings.
            const int updateAttempts = 5;

            for (var i = 0; i < updateAttempts; i++)
            {
                var readResult = zooKeeperClient.GetData(applicationPath);
                // CR(kungurtsev): should we break?
                if (!readResult.IsSuccessful)
                    continue;

                var topologyData = ApplicationNodeDataSerializer.Deserialize(environment, application, readResult.Data);
                IServiceTopologyProperties properties = new ServiceTopologyProperties(topologyData.Properties);

                properties = updateFunc(properties);
                // CR(kungurtsev): extra ToDictionary.
                var data = ApplicationNodeDataSerializer.Serialize(new ApplicationInfo(environment, application, properties.ToDictionary(x => x.Key, y => y.Value)));
                var request = new SetDataRequest(applicationPath, data)
                {
                    Version = readResult.Stat.Version
                };

                var updateResult = await zooKeeperClient.SetDataAsync(request);

                if (updateResult.Status == ZooKeeperStatus.VersionsMismatch)
                {
                    continue;
                }

                return updateResult.IsSuccessful;
            }

            return false;
        }

        public async Task<bool> TryUpdateEnvironmentPropertiesAsync(string environment, Func<IServiceTopologyProperties, IServiceTopologyProperties> updateFunc)
        {
            var environmentPath = pathHelper.BuildEnvironmentPath(environment);

            const int updateAttempts = 5;

            for (var i = 0; i < updateAttempts; i++)
            {
                var readResult = zooKeeperClient.GetData(environmentPath);
                if (!readResult.IsSuccessful)
                    continue;

                var environmentInfo = EnvironmentNodeDataSerializer.Deserialize(environment, readResult.Data);
                IServiceTopologyProperties properties = new ServiceTopologyProperties(environmentInfo.Properties);
                properties = updateFunc(properties);
                var data = EnvironmentNodeDataSerializer.Serialize(new EnvironmentInfo(environment, environmentInfo.ParentEnvironment, properties.ToDictionary(x => x.Key, y => y.Value)));
                var request = new SetDataRequest(environmentPath, data)
                {
                    Version = readResult.Stat.Version
                };

                var updateResult = await zooKeeperClient.SetDataAsync(request);

                if (updateResult.Status == ZooKeeperStatus.VersionsMismatch)
                {
                    continue;
                }

                return updateResult.IsSuccessful;
            }

            return false;
        }
    }
}