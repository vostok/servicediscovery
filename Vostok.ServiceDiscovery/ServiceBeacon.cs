using System;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery
{
    public class ServiceBeacon : IServiceBeacon
    {
        private readonly string environmentNodePath;
        private readonly string serviceNodePath;
        private readonly string replicaNodePath;
        private readonly byte[] replicaNodeData;

        private readonly IZooKeeperClient zooKeeperClient;
        private readonly AdHocNodeWatcher nodeWatcher;
        private long lastConnectedTimestamp;

        private readonly ServiceBeaconSettings settings;
        private volatile Task beaconTask;
        private readonly ReplicaInfo replicaInfo;
        private volatile AtomicBoolean isRunning = false;
        private volatile AsyncManualResetEvent nodeCreatedOnceSignal = new AsyncManualResetEvent(false);
        private readonly AsyncManualResetEvent checkNodeSignal = new AsyncManualResetEvent(true);
        private readonly ILog log;

        public ServiceBeacon(ReplicaInfo replicaInfo, IZooKeeperClient zooKeeperClient, ILog log, ServiceBeaconSettings settings)
        {
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceBeacon>();
            this.replicaInfo = replicaInfo;
            this.zooKeeperClient = zooKeeperClient;
            this.settings = settings;

            var pathBuilder = new PathBuilder(settings.ZooKeeperNodePath);
            environmentNodePath = pathBuilder.BuildEnvironmentPath(replicaInfo.Envoronment);
            serviceNodePath = pathBuilder.BuildServicePath(replicaInfo.Envoronment, replicaInfo.Service);
            replicaNodePath = pathBuilder.BuildReplicaPath(replicaInfo.Envoronment, replicaInfo.Service, replicaInfo.Replica);
            replicaNodeData = NodeDataSerializer.Serialize(replicaInfo.ToDictionary());

            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        public void Start()
        {
            if (isRunning.TrySetTrue())
            {
                nodeCreatedOnceSignal.Reset();
                checkNodeSignal.Set();
                beaconTask = BeaconTask();
            }
        }

        public void Stop()
        {
            if (isRunning.TrySetFalse())
            {
                checkNodeSignal.Set();
                beaconTask.Wait();
                DropNodeIfExists();
            }
        }

        public Task WaitForRegistration(TimeSpan timeout) => nodeCreatedOnceSignal.WaitAsync();

        private async Task BeaconTask()
        {
            var observer = new AdHocConnectionStateObserver(OnConnectionStateChanged, OnCompleted);
            using (zooKeeperClient.OnConnectionStateChanged.Subscribe(observer))
            {
                while (isRunning)
                {
                    var budget = TimeBudget.StartNew(settings.MinimumTimeBetweenIterations);

                    await BeaconTaskIteration().ConfigureAwait(false);

                    if (!budget.HasExpired)
                        await Task.Delay(budget.Remaining).ConfigureAwait(false);
                }
            }
        }

        private async Task BeaconTaskIteration()
        {
            try
            {
                await EnsureNodeExists().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Failed beacon iteration.");
            }

            var waitTimeout = nodeCreatedOnceSignal.Get() ? settings.IterationTimeSpan : 1.Seconds();
            await checkNodeSignal.WaitAsync().WaitAsync(waitTimeout).ConfigureAwait(false);
        }

        private void OnCompleted()
        {
            if (isRunning.TrySetFalse())
            {
                checkNodeSignal.Set();
                beaconTask.Wait();
            }
        }

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.Connected)
            {
                Interlocked.Exchange(ref lastConnectedTimestamp, DateTime.UtcNow.Ticks);
                checkNodeSignal.Set();
            }
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            if (!isRunning)
                return;

            if (type == NodeChangedEventType.Created && path.Equals(environmentNodePath)
                || type == NodeChangedEventType.Deleted && path.Equals(replicaNodePath))
                checkNodeSignal.Set();
        }

        private async Task EnsureNodeExists()
        {
            if (!await EnsureEnvironmentExists().ConfigureAwait(false))
                return;

            var exists = await zooKeeperClient.ExistsAsync(new ExistsRequest(replicaNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);
            if (exists.IsSuccessful && exists.Stat != null)
            {
                if (exists.Stat.EphemeralOwner == zooKeeperClient.SessionId)
                    return;

                var lastConnected = new DateTime(Interlocked.Read(ref lastConnectedTimestamp), DateTimeKind.Utc);
                var nodeCreationTime = exists.Stat.CreatedTime;
                if (nodeCreationTime > lastConnected)
                {
                    log.Warn(
                        $"Node with path '{replicaNodePath}' already exists " +
                        $"and has owner session id = {exists.Stat.EphemeralOwner:x16}, " +
                        $"which differs from our id = {zooKeeperClient.SessionId:x16}. " +
                        $"But it was created recently (at {nodeCreationTime}) so we won't touch it. " +
                        $"This may indicate several beacons with same zone, service and URL!");

                    return;
                }

                log.Warn(
                    $"Node with path '{replicaNodePath}' already exists, " +
                    $"but looks like a stale one from ourselves. " +
                    $"It has owner session id = {exists.Stat.EphemeralOwner:x16}, " +
                    $"which differs from our id = {zooKeeperClient.SessionId:x16}. " +
                    $"It was created at {nodeCreationTime}. " +
                    $"Will delete it and create a new one.");

                DropNodeIfExists();
            }

            var createRequest = new CreateRequest(replicaNodePath, CreateMode.Ephemeral)
            {
                CreateParentsIfNeeded = false,
                Data = replicaNodeData
            };
            var create = await zooKeeperClient.CreateAsync(createRequest).ConfigureAwait(false);

            await zooKeeperClient.ExistsAsync(new ExistsRequest(replicaNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);

            if (create.Status == ZooKeeperStatus.Ok || create.Status == ZooKeeperStatus.NodeAlreadyExists)
            {
                nodeCreatedOnceSignal.Set();
                return;
            }

            log.Error("Node creation has failed.");
        }

        private async Task<bool> EnsureEnvironmentExists()
        {
            var environmentExists = await zooKeeperClient.ExistsAsync(new ExistsRequest(environmentNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);
            if (!environmentExists.IsSuccessful)
                return false;
            if (!environmentExists.Exists)
            {
                log.Warn("Node for current environment does not exist at path '{Path}'.", environmentNodePath);
                return false;
            }

            return true;
        }

        private void DropNodeIfExists()
        {
            for (var times = 0; times < 10; times++)
            {
                var deleteResult = zooKeeperClient.Delete(replicaNodePath);
                if (deleteResult.IsSuccessful)
                    return;
            }

            log.Error("Node removal has failed.");
        }
    }
}