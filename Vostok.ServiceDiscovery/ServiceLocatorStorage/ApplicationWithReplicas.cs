using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Url;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage
{
    internal class ApplicationWithReplicas
    {
        [CanBeNull]
        public volatile ServiceTopology ServiceTopology;

        private readonly string environmentName;
        private readonly string applicationName;
        private readonly string applicationNodePath;
        private readonly VersionedContainer<ApplicationInfo> applicationContainer;
        private readonly VersionedContainer<Uri[]> replicasContainer;

        private readonly IZooKeeperClient zooKeeperClient;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ILog log;
        private readonly AtomicBoolean isDisposed;

        public ApplicationWithReplicas(
            string environmentName,
            string applicationName,
            string applicationNodePath,
            IZooKeeperClient zooKeeperClient,
            ILog log,
            AtomicBoolean isDisposed)
        {
            this.environmentName = environmentName;
            this.applicationName = applicationName;
            this.applicationNodePath = applicationNodePath;
            this.zooKeeperClient = zooKeeperClient;
            this.log = log;
            this.isDisposed = isDisposed;

            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
            applicationContainer = new VersionedContainer<ApplicationInfo>();
            replicasContainer = new VersionedContainer<Uri[]>();
        }

        public void Update()
        {
            if (isDisposed)
                return;

            try
            {
                var applicationExists = zooKeeperClient.Exists(new ExistsRequest(applicationNodePath) {Watcher = nodeWatcher});
                if (!applicationExists.IsSuccessful)
                {
                    return;
                }

                if (applicationExists.Stat == null)
                {
                    Clear();
                    return;
                }

                if (applicationContainer.NeedUpdate(applicationExists.Stat.ModifiedZxId))
                {
                    var applicationData = zooKeeperClient.GetData(new GetDataRequest(applicationNodePath) {Watcher = nodeWatcher});
                    if (applicationData.Status == ZooKeeperStatus.NodeNotFound)
                        Clear();
                    if (!applicationData.IsSuccessful)
                        return;

                    var info = ApplicationNodeDataSerializer.Deserialize(applicationData.Data);
                    if (applicationContainer.Update(applicationData.Stat.ModifiedZxId, info))
                        UpdateServiceTopology();
                }

                if (replicasContainer.NeedUpdate(applicationExists.Stat.ModifiedChildrenZxId))
                {
                    var applicationChildren = zooKeeperClient.GetChildren(new GetChildrenRequest(applicationNodePath) {Watcher = nodeWatcher});
                    if (applicationChildren.Status == ZooKeeperStatus.NodeNotFound)
                        Clear();
                    if (!applicationChildren.IsSuccessful)
                        return;

                    var replicas = UrlParser.Parse(applicationChildren.ChildrenNames.Select(ServiceDiscoveryPathHelper.Unescape));
                    if (replicasContainer.Update(applicationChildren.Stat.ModifiedChildrenZxId, replicas))
                        UpdateServiceTopology();
                }
            }
            catch (Exception error)
            {
                log.Error(error, "Failed to update '{Application} application in '{Environment}' environment.", applicationName, environmentName);
            }
        }
        
        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            if (isDisposed)
                return;

            Task.Run(() => Update());
        }

        private void Clear()
        {
            applicationContainer.Clear();
            replicasContainer.Clear();

            UpdateServiceTopology();
        }

        private void UpdateServiceTopology()
        {
            ServiceTopology = ServiceTopology.Build(replicasContainer.Value, applicationContainer.Value?.Properties);
        }
    }
}
