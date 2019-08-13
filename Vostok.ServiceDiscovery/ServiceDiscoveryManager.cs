using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions;
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
    }
}