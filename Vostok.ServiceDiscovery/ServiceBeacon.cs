using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;
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
    /// <inheritdoc cref="IServiceBeacon"/>
    [PublicAPI]
    public class ServiceBeacon : IServiceBeacon, IDisposable
    {
        private readonly string environmentNodePath;
        private readonly string applicationNodePath;
        private readonly string replicaNodePath;
        private readonly byte[] replicaNodeData;
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ServiceBeaconSettings settings;
        private readonly AsyncManualResetEvent checkNodeSignal = new AsyncManualResetEvent(true);
        private readonly ILog log;
        private readonly object startStopSync = new object();
        private long lastConnectedTimestamp;
        private volatile Task beaconTask;
        private volatile AtomicBoolean isRunning = false;
        private volatile AsyncManualResetEvent nodeCreatedOnceSignal = new AsyncManualResetEvent(false);

        public ServiceBeacon(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [CanBeNull] ReplicaInfoBuilderSetup replicaInfoBuilderSetup = null,
            [CanBeNull] ServiceBeaconSettings settings = null,
            [CanBeNull] ILog log = null)
            : this(zooKeeperClient, ReplicaInfoBuilder.Build(replicaInfoBuilderSetup), settings, log)
        {
        }

        internal ServiceBeacon(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [NotNull] ReplicaInfo replicaInfo,
            [CanBeNull] ServiceBeaconSettings settings,
            [CanBeNull] ILog log)
        {
            this.zooKeeperClient = zooKeeperClient ?? throw new ArgumentNullException(nameof(settings));
            replicaInfo = replicaInfo ?? throw new ArgumentNullException(nameof(settings));
            this.settings = settings ?? new ServiceBeaconSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceBeacon>();

            var serviceDiscoveryPath = new ServiceDiscoveryPath(this.settings.ZooKeeperNodePath);
            environmentNodePath = serviceDiscoveryPath.BuildEnvironmentPath(replicaInfo.Environment);
            applicationNodePath = serviceDiscoveryPath.BuildApplicationPath(replicaInfo.Environment, replicaInfo.Application);
            replicaNodePath = serviceDiscoveryPath.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
            replicaNodeData = ReplicaNodeDataSerializer.Serialize(replicaInfo.Properties);

            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        /// <inheritdoc />
        public void Start()
        {
            lock (startStopSync)
            {
                if (isRunning.TrySetTrue())
                {
                    nodeCreatedOnceSignal.Reset();
                    checkNodeSignal.Set();
                    beaconTask = Task.Run(BeaconTask);
                }
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            lock (startStopSync)
            {
                if (isRunning.TrySetFalse())
                {
                    checkNodeSignal.Set();
                    beaconTask.Wait();
                    DropNodeIfExists();
                }
            }
        }

        /// <summary>
        /// Calls <see cref="Stop"/> method.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Waits for first registration after <see cref="Start"/> method call.
        /// </summary>
        [NotNull]
        public Task WaitForRegistration() => nodeCreatedOnceSignal.WaitAsync();

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
                checkNodeSignal.Reset();
                await EnsureNodeExists().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Failed ServiceBeacon iteration.");
            }

            var waitTimeout = nodeCreatedOnceSignal.IsCurrentlySet()
                ? settings.IterationPeriod
                : 1.Seconds();
            await checkNodeSignal.WaitAsync().WaitAsync(waitTimeout).ConfigureAwait(false);
        }

        private void OnCompleted()
        {
            log.Warn("Someone else has disposed ZooKeeper client.");
            if (isRunning.TrySetFalse())
            {
                checkNodeSignal.Set();
                // Note(kungurtsev): does not wait beaconTask, because it will deadlock CachingObservable.
            }
        }

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.Connected)
            {
                Interlocked.Exchange(ref lastConnectedTimestamp, DateTime.UtcNow.Ticks);
            }

            // Note(kungurtsev): sometimes need to perform some operation to force ZooKeeperClient reconnect.
            checkNodeSignal.Set();
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            // Note(kungurtsev): even if we received modify data event, we should put new watchers on the node
            checkNodeSignal.Set();
        }

        private async Task EnsureNodeExists()
        {
            if (!await EnvironmentExists().ConfigureAwait(false))
                return;

            var exists = await zooKeeperClient.ExistsAsync(new ExistsRequest(replicaNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);
            if (exists.IsSuccessful && exists.Stat != null)
            {
                if (exists.Stat.EphemeralOwner == zooKeeperClient.SessionId)
                {
                    nodeCreatedOnceSignal.Set();
                    return;
                }

                var lastConnected = new DateTime(Interlocked.Read(ref lastConnectedTimestamp), DateTimeKind.Utc);
                var nodeCreationTime = exists.Stat.CreatedTime;
                if (nodeCreationTime > lastConnected)
                {
                    log.Warn(
                        $"Node with path '{replicaNodePath}' already exists " +
                        $"and has owner session id = {exists.Stat.EphemeralOwner:x16}, " +
                        $"which differs from our id = {zooKeeperClient.SessionId:x16}. " +
                        $"But it was created recently (at {nodeCreationTime}) so we won't touch it. " +
                        "This may indicate several beacons with same environment, application and replica exist.");

                    return;
                }

                log.Warn(
                    $"Node with path '{replicaNodePath}' already exists, " +
                    "but looks like a stale one from ourselves. " +
                    $"It has owner session id = {exists.Stat.EphemeralOwner:x16}, " +
                    $"which differs from our id = {zooKeeperClient.SessionId:x16}. " +
                    $"It was created at {nodeCreationTime}. " +
                    "Will delete it and create a new one.");

                DropNodeIfExists();
            }

            var createRequest = new CreateRequest(replicaNodePath, CreateMode.Ephemeral)
            {
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

        private async Task<bool> EnvironmentExists()
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
            for (var attempt = 0; attempt < settings.DeleteNodeAttempts; attempt++)
            {
                var deleteResult = zooKeeperClient.Delete(replicaNodePath);
                if (deleteResult.IsSuccessful)
                    return;
            }

            log.Error($"Node removal has failed {settings.DeleteNodeAttempts} times.");
        }
    }
}