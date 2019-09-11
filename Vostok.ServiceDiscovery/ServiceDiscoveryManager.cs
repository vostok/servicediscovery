using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
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

        public async Task<IReadOnlyList<string>> GetAllEnvironmentsAsync()
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(settings.ZooKeeperNodesPrefix)).ConfigureAwait(false);

            if (data.Status == ZooKeeperStatus.NodeNotFound)
                return new string[0];

            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        public async Task<IReadOnlyList<string>> GetAllApplicationsAsync(string environment)
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(pathHelper.BuildEnvironmentPath(environment))).ConfigureAwait(false);

            if (data.Status == ZooKeeperStatus.NodeNotFound)
                return new string[0];

            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        public async Task<IReadOnlyList<string>> GetAllReplicasAsync(string environment, string application)
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(pathHelper.BuildApplicationPath(environment, application))).ConfigureAwait(false);

            if (data.Status == ZooKeeperStatus.NodeNotFound)
                return new string[0];

            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        public async Task<IEnvironmentInfo> GetEnvironmentAsync(string environment)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildEnvironmentPath(environment))).ConfigureAwait(false);

            if (data.Status == ZooKeeperStatus.NodeNotFound)
                return null;

            data.EnsureSuccess();
            var envData = EnvironmentNodeDataSerializer.Deserialize(environment, data.Data);
            return envData;
        }

        public async Task<IApplicationInfo> GetApplicationAsync(string environment, string application)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildApplicationPath(environment, application))).ConfigureAwait(false);

            if (data.Status == ZooKeeperStatus.NodeNotFound)
                return null;

            data.EnsureSuccess();
            var appData = ApplicationNodeDataSerializer.Deserialize(environment, application, data.Data);
            return appData;
        }

        public async Task<IReplicaInfo> GetReplicaAsync(string environment, string application, string replica)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildApplicationPath(environment, application))).ConfigureAwait(false);

            if (data.Status == ZooKeeperStatus.NodeNotFound)
                return null;

            data.EnsureSuccess();
            var envData = ReplicaNodeDataSerializer.Deserialize(environment, application, replica, data.Data);
            return envData;
        }

        public async Task<bool> TryCreateEnvironmentAsync(IEnvironmentInfo environmentInfo)
        {
            var createRequest = new CreateRequest(pathHelper.BuildEnvironmentPath(environmentInfo.Environment), CreateMode.Persistent)
            {
                Data = EnvironmentNodeDataSerializer.Serialize(environmentInfo)
            };

            return (await zooKeeperClient.CreateAsync(createRequest).ConfigureAwait(false)).IsSuccessful;
        }

        public async Task<bool> TryDeleteEnvironmentAsync(string environment)
        {
            var path = pathHelper.BuildEnvironmentPath(environment);

            var deleteRequest = new DeleteRequest(path)
            {
                DeleteChildrenIfNeeded = true
            };

            var deleteResult = await zooKeeperClient.DeleteAsync(deleteRequest).ConfigureAwait(false);

            return deleteResult.IsSuccessful;
        }

        public async Task<bool> TryUpdateEnvironmentPropertiesAsync(string environment, Func<IEnvironmentInfoProperties, IEnvironmentInfoProperties> updateFunc)
        {
            var environmentPath = pathHelper.BuildEnvironmentPath(environment);

            var updateDataRequest = new UpdateDataRequest(
                environmentPath,
                bytes => NodeDataHelper.SetEnvironmentProperties(environment, updateFunc, bytes),
                settings.ZooKeeperNodeUpdateAttempts);

            return (await zooKeeperClient.UpdateDataAsync(updateDataRequest).ConfigureAwait(false)).IsSuccessful;
        }

        public async Task<bool> TryUpdateApplicationPropertiesAsync(string environment, string application, Func<IApplicationInfoProperties, IApplicationInfoProperties> updateFunc)
        {
            var applicationPath = pathHelper.BuildApplicationPath(environment, application);
            var updateDataRequest = new UpdateDataRequest(
                applicationPath,
                bytes => NodeDataHelper.SetApplicationProperties(environment, application, updateFunc, bytes),
                settings.ZooKeeperNodeUpdateAttempts);

            return (await zooKeeperClient.UpdateDataAsync(updateDataRequest).ConfigureAwait(false)).IsSuccessful;
        }

        public async Task<bool> TryUpdateEnvironmentParentAsync(string environment, string newParent)
        {
            var environmentPath = pathHelper.BuildEnvironmentPath(environment);
            var updateDataRequest = new UpdateDataRequest(
                environmentPath,
                bytes => NodeDataHelper.SetEnvironmentParent(environment, newParent, bytes),
                settings.ZooKeeperNodeUpdateAttempts);

            return (await zooKeeperClient.UpdateDataAsync(updateDataRequest).ConfigureAwait(false)).IsSuccessful;
        }

        public async Task<bool> TryCreatePermanentReplicaAsync(IReplicaInfo replica)
        {
            var replicaPath = pathHelper.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);

            var createRequest = new CreateRequest(replicaPath, CreateMode.Persistent)
            {
                Data = ReplicaNodeDataSerializer.Serialize(replica)
            };

            return (await zooKeeperClient.CreateAsync(createRequest).ConfigureAwait(false)).IsSuccessful;
        }

        public async Task<bool> TryDeletePermanentReplicaAsync(string environment, string application, string replica)
        {
            var path = pathHelper.BuildReplicaPath(environment, application, replica);

            var existsResult = await zooKeeperClient.ExistsAsync(new ExistsRequest(path)).ConfigureAwait(false);

            if (!existsResult.IsSuccessful)
                return false;

            if (existsResult.Stat == null)
                return true;

            if (existsResult.Stat.EphemeralOwner != 0)
                return false;

            var deleteRequest = new DeleteRequest(path)
            {
                DeleteChildrenIfNeeded = true,
                Version = existsResult.Stat.Version
            };

            var deleteResult = await zooKeeperClient.DeleteAsync(deleteRequest).ConfigureAwait(false);

            return deleteResult.IsSuccessful;
        }
    }
}