﻿using System;
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
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(settings.ZooKeeperNodesPrefix)).ConfigureAwait(false);
            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        // CR(kungurtsev): NodeNotFound -> ?.
        public async Task<IReadOnlyList<string>> GetAllApplicationsAsync([NotNull] string environment)
        {
            var data = await zooKeeperClient.GetChildrenAsync(new GetChildrenRequest(pathHelper.BuildEnvironmentPath(environment))).ConfigureAwait(false);
            data.EnsureSuccess();
            return data.ChildrenNames.Select(n => pathHelper.Unescape(n)).ToList();
        }

        // CR(kungurtsev): NodeNotFound -> null.
        public async Task<IEnvironmentInfo> GetEnvironmentAsync([NotNull] string environment)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildEnvironmentPath(environment))).ConfigureAwait(false);
            data.EnsureSuccess();
            var envData = EnvironmentNodeDataSerializer.Deserialize(environment, data.Data);
            return envData;
        }

        // CR(kungurtsev): NodeNotFound -> null.
        public async Task<IApplicationInfo> GetApplicationAsync([NotNull] string environment, [NotNull] string application)
        {
            var data = await zooKeeperClient.GetDataAsync(new GetDataRequest(pathHelper.BuildApplicationPath(environment, application))).ConfigureAwait(false);
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

            return (await zooKeeperClient.CreateAsync(createRequest).ConfigureAwait(false)).IsSuccessful;
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

            return (await zooKeeperClient.DeleteAsync(deleteRequest).ConfigureAwait(false)).IsSuccessful;
        }

        // CR(kungurtsev): GetEnvironmentAsync is null. Remove this.
        internal async Task<bool> CheckZoneExistenceAsync([NotNull] string path)
        {
            var result = await zooKeeperClient.ExistsAsync(path).ConfigureAwait(false);
            if (result.IsSuccessful)
                return result.Stat != null;

            return false;
        }

        // CR(kungurtsev): add helper that modify zookeeper node bytes. Possibly as extension to vostok.zookeeper.abstractions.
        public async Task<bool> TryUpdateApplicationPropertiesAsync([NotNull] string environment, [NotNull] string application, Func<IApplicationInfoProperties, IApplicationInfoProperties> updateFunc)
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

                var applicationInfo = ApplicationNodeDataSerializer.Deserialize(environment, application, readResult.Data);
                var newPropeties = updateFunc(applicationInfo.Properties);

                var data = ApplicationNodeDataSerializer.Serialize(new ApplicationInfo(environment, application, newPropeties));
                var request = new SetDataRequest(applicationPath, data)
                {
                    Version = readResult.Stat.Version
                };

                var updateResult = await zooKeeperClient.SetDataAsync(request).ConfigureAwait(false);

                if (updateResult.Status == ZooKeeperStatus.VersionsMismatch)
                {
                    continue;
                }

                return updateResult.IsSuccessful;
            }

            return false;
        }

        public async Task<bool> TryUpdateEnvironmentPropertiesAsync(string environment, Func<IEnvironmentInfoProperties, IEnvironmentInfoProperties> updateFunc)
        {
            var environmentPath = pathHelper.BuildEnvironmentPath(environment);

            const int updateAttempts = 5;

            for (var i = 0; i < updateAttempts; i++)
            {
                var readResult = zooKeeperClient.GetData(environmentPath);
                if (!readResult.IsSuccessful)
                    continue;

                var environmentInfo = EnvironmentNodeDataSerializer.Deserialize(environment, readResult.Data);
                var newProperties = updateFunc(environmentInfo.Properties);

                var data = EnvironmentNodeDataSerializer.Serialize(new EnvironmentInfo(environment, environmentInfo.ParentEnvironment, newProperties));
                var request = new SetDataRequest(environmentPath, data)
                {
                    Version = readResult.Stat.Version
                };

                var updateResult = await zooKeeperClient.SetDataAsync(request).ConfigureAwait(false);

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