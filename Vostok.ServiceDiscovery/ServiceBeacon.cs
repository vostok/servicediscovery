using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Threading;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ServiceDiscovery.Telemetry;
using Vostok.ServiceDiscovery.Telemetry.Event;
using Vostok.ServiceDiscovery.Telemetry.EventsBuilder;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc cref="IServiceBeacon" />
    [PublicAPI]
    public class ServiceBeacon : IServiceBeacon, IDisposable
    {
        private readonly ReplicaInfo replicaInfo;
        private readonly TagCollection tags;

        private readonly string environmentNodePath;
        private readonly string applicationNodePath;
        private readonly string replicaNodePath;
        private readonly byte[] replicaNodeData;
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly AdHocNodeWatcher nodeWatcher;

        private readonly ServiceBeaconSettings settings;
        private readonly ServiceDiscoveryManager serviceDiscoveryManager;

        private readonly ILog log;

        private readonly object startStopSync = new object();
        private readonly AtomicBoolean isRunning = false;
        private readonly AtomicBoolean clientDisposed = false;
        private readonly AtomicBoolean registrationAllowed = true;
        private readonly AtomicBoolean tagsCreated = false;
        private readonly AsyncManualResetEvent nodeCreatedOnceSignal = new AsyncManualResetEvent(false);
        private readonly AsyncManualResetEvent checkNodeSignal = new AsyncManualResetEvent(true);
        private long lastConnectedTimestamp;
        private volatile Task beaconTask;
        private volatile CancellationTokenSource stopCancellationToken;

        private readonly AtomicBoolean stopEventSent = false;
        private readonly AtomicBoolean registrationAllowedChanged = false;
        private IDisposable eventEventContextToken;

        public ServiceBeacon(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [CanBeNull] ReplicaInfoSetup replicaInfoSetup = null,
            [CanBeNull] ServiceBeaconSettings settings = null,
            [CanBeNull] ILog log = null)
            : this(zooKeeperClient, ReplicaInfoBuilder.Build(replicaInfoSetup, (settings ?? new ServiceBeaconSettings()).UseFQDN), settings, log)
        {
        }

        internal ServiceBeacon(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [NotNull] ServiceBeaconInfo serviceBeaconInfo,
            [CanBeNull] ServiceBeaconSettings settings,
            [CanBeNull] ILog log)
        {
            this.zooKeeperClient = zooKeeperClient ?? throw new ArgumentNullException(nameof(zooKeeperClient));
            if (settings?.CreateEnvironmentIfAbsent?.Environment != null && serviceBeaconInfo.ReplicaInfo.Environment != settings.CreateEnvironmentIfAbsent.Environment)
                throw new ArgumentException($"Provided {nameof(serviceBeaconInfo.ReplicaInfo.Environment)} and {settings.CreateEnvironmentIfAbsent.Environment} should not differ.");

            replicaInfo = serviceBeaconInfo.ReplicaInfo;
            tags = serviceBeaconInfo.Tags;
            this.settings = settings ?? new ServiceBeaconSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceBeacon>();
            var sdManagerSettings = new ServiceDiscoveryManagerSettings
            {
                ZooKeeperNodesPrefix = this.settings.ZooKeeperNodesPrefix,
                ZooKeeperNodesPathEscaper = this.settings.ZooKeeperNodesPathEscaper
            };
            serviceDiscoveryManager = new ServiceDiscoveryManager(zooKeeperClient, sdManagerSettings, log);
            var pathHelper = new ServiceDiscoveryPathHelper(this.settings.ZooKeeperNodesPrefix, this.settings.ZooKeeperNodesPathEscaper);
            environmentNodePath = pathHelper.BuildEnvironmentPath(replicaInfo.Environment);
            applicationNodePath = pathHelper.BuildApplicationPath(replicaInfo.Environment, replicaInfo.Application);
            replicaNodePath = pathHelper.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
            replicaNodeData = ReplicaNodeDataSerializer.Serialize(replicaInfo, FilterReplicaInfoProperties);

            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        public IReplicaInfo ReplicaInfo => replicaInfo;

        /// <inheritdoc />
        public void Start()
        {
            lock (startStopSync)
            {
                if (isRunning.TrySetTrue())
                {
                    eventEventContextToken = new ServiceDiscoveryEventsContextToken(builder =>
                        builder.SetEnvironment(replicaInfo.Environment)
                            .SetApplication(replicaInfo.Application)
                            .AddReplicas(replicaInfo.Replica));
                    stopCancellationToken = new CancellationTokenSource();
                    nodeCreatedOnceSignal.Reset();
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
                    log.Info("Stopped. Unregistering..");

                    stopCancellationToken.Cancel();
                    checkNodeSignal.Set();
                    beaconTask.GetAwaiter().GetResult();
                    stopCancellationToken.Dispose();
                    if (!DeleteNodeAsync().GetAwaiter().GetResult())
                    {
                        Task.Run(DeleteNodeTask);
                    }

                    RemoveTagsIfNeed().GetAwaiter().GetResult();
                    using (new ServiceDiscoveryEventsContextToken(builder => builder.SetDescription("Stop registration in service discovery")))
                        SendStopEventIfNeeded();
                }
            }
        }

        /// <summary>
        /// Calls <see cref="Stop" /> method.
        /// </summary>
        public void Dispose()
        {
            Stop();
            eventEventContextToken?.Dispose();
        }

        /// <summary>
        /// Waits for first registration after <see cref="Start" /> method call.
        /// </summary>
        [NotNull]
        public Task WaitForInitialRegistrationAsync() => nodeCreatedOnceSignal.WaitAsync();

        private bool FilterReplicaInfoProperties(string key, string value)
        {
            if (key == ReplicaInfoKeys.Dependencies && !settings.AddDependenciesToNodeData)
                return false;

            return true;
        }

        private async Task BeaconTask()
        {
            var observer = new AdHocConnectionStateObserver(OnConnectionStateChanged, OnCompleted);
            using (zooKeeperClient.OnConnectionStateChanged.Subscribe(observer))
            {
                log.Info(
                    "Registering an instance of application '{Application}' in environment '{Environment}' with id = '{Instance}'.",
                    replicaInfo.Application,
                    replicaInfo.Environment,
                    replicaInfo.Replica);

                while (isRunning)
                {
                    var budget = TimeBudget.StartNew(settings.MinimumTimeBetweenIterations);

                    await BeaconTaskIteration().ConfigureAwait(false);

                    if (!budget.HasExpired)
                        await Task.Delay(budget.Remaining, stopCancellationToken.Token).SilentlyContinue().ConfigureAwait(false);
                }
            }
        }

        private async Task BeaconTaskIteration()
        {
            try
            {
                await EnsureNodeExistsAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Failed iteration.");
            }

            var waitTimeout = nodeCreatedOnceSignal.IsCurrentlySet()
                ? settings.IterationPeriod
                : settings.InitialRegistrationIterationPeriod;
            await checkNodeSignal.WaitAsync().WaitAsync(waitTimeout).ConfigureAwait(false);
            checkNodeSignal.Reset();
        }

        private async Task DeleteNodeTask()
        {
            var observer = new AdHocConnectionStateObserver(null, OnCompleted);
            using (zooKeeperClient.OnConnectionStateChanged.Subscribe(observer))
            {
                while (!clientDisposed && !await DeleteNodeAsync().ConfigureAwait(false))
                {
                    await Task.Delay(settings.DeleteNodeIterationPeriod).ConfigureAwait(false);
                }
            }
        }

        private void OnCompleted()
        {
            log.Warn("Someone else has disposed ZooKeeper client.");

            clientDisposed.TrySetTrue();

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
            // Note(kungurtsev): even if we received modify data event, we should put new watchers on the node.
            checkNodeSignal.Set();
        }

        private async Task EnsureNodeExistsAsync()
        {
            if (!await EnvironmentExistsAsync().ConfigureAwait(false))
                return;

            var existsNode = await zooKeeperClient.ExistsAsync(new ExistsRequest(replicaNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);
            if (!existsNode.IsSuccessful)
                return;

            if (registrationAllowed.TrySet(settings.RegistrationAllowedProvider?.Invoke() ?? true))
            {
                registrationAllowedChanged.TrySetTrue();
                log.Info(registrationAllowed ? "Registration has been allowed." : "Registration has been denied.");
            }

            if (!registrationAllowed)
            {
                if (existsNode.Stat != null && await DeleteNodeAsync().ConfigureAwait(false))
                    using (new ServiceDiscoveryEventsContextToken(builder => builder.SetDescription("Registration has been denied by RegistrationAllowedProvider.")))
                        SendStopEventIfNeeded();
                return;
            }

            if (existsNode.Stat != null)
            {
                if (existsNode.Stat.EphemeralOwner == zooKeeperClient.SessionId)
                {
                    nodeCreatedOnceSignal.Set();
                    await TrySetTagsInNeeded().ConfigureAwait(false);
                    return;
                }

                var lastConnected = new DateTime(Interlocked.Read(ref lastConnectedTimestamp), DateTimeKind.Utc);
                var nodeCreationTime = existsNode.Stat.CreatedTime;
                if (nodeCreationTime > lastConnected)
                {
                    log.Warn(
                        "Node with path '{ReplicaNodePath}' already exists " +
                        "and has owner session id = {NodeEphemeralOwner:x16}, " +
                        "which differs from our id = {ClientSessionId:x16}. " +
                        "But it was created recently (at {NodeCreationTime}) so we won't touch it. " +
                        "This may indicate several beacons with same environment, application and replica exist.",
                        replicaNodePath,
                        existsNode.Stat.EphemeralOwner,
                        zooKeeperClient.SessionId,
                        nodeCreationTime);

                    return;
                }

                log.Warn(
                    "Node with path '{ReplicaNodePath}' already exists, " +
                    "but looks like a stale one from ourselves. " +
                    "It has owner session id = {NodeEphemeralOwner:x16}, " +
                    "which differs from our id = {ClientSessionId:x16}. " +
                    "It was created at {NodeCreationTime}. " +
                    "Will delete it and create a new one.",
                    replicaNodePath,
                    existsNode.Stat.EphemeralOwner,
                    zooKeeperClient.SessionId,
                    nodeCreationTime);

                if (!await DeleteNodeAsync().ConfigureAwait(false))
                    return;
            }

            if (await TryCreateReplicaNode().ConfigureAwait(false))
            {
                SendStartEventIfNeeded();
                nodeCreatedOnceSignal.Set();
                await TrySetTagsInNeeded().ConfigureAwait(false);
            }

            await zooKeeperClient.ExistsAsync(new ExistsRequest(replicaNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);
        }

        // NOTE (tsup): We do not use recurrent node creation because we want to avoid race with explicit environment deletion.
        private async Task<bool> TryCreateReplicaNode()
        {
            var createApplicationRequest = new CreateRequest(applicationNodePath, CreateMode.Persistent)
            {
                CreateParentsIfNeeded = false
            };
            var createReplicaNodeRequest = new CreateRequest(replicaNodePath, CreateMode.Ephemeral)
            {
                CreateParentsIfNeeded = false,
                Data = replicaNodeData
            };

            CreateResult create = null;

            foreach (var createRequest in new[] {createApplicationRequest, createReplicaNodeRequest})
            {
                create = await zooKeeperClient.CreateAsync(createRequest).ConfigureAwait(false);

                if (!create.IsSuccessful && create.Status != ZooKeeperStatus.NodeAlreadyExists)
                {
                    log.Error("Node creation has failed.");
                    return false;
                }
            }

            return create?.IsSuccessful ?? false;
        }

        private async Task<bool> EnvironmentExistsAsync()
        {
            var environmentExists = await zooKeeperClient.ExistsAsync(new ExistsRequest(environmentNodePath) {Watcher = nodeWatcher}).ConfigureAwait(false);

            if (!environmentExists.IsSuccessful)
            {
                return false;
            }

            if (!environmentExists.Exists)
            {
                if (settings.CreateEnvironmentIfAbsent != null && !nodeCreatedOnceSignal.IsCurrentlySet())
                {
                    log.Info("Environment at path `{Path}` doesn't exist. Trying to create with default settings `{DefaultSettings}`.", environmentNodePath, settings.CreateEnvironmentIfAbsent);
                    var isCreated = await serviceDiscoveryManager.TryCreateEnvironmentAsync(settings.CreateEnvironmentIfAbsent).ConfigureAwait(false);

                    if (!isCreated)
                    {
                        log.Warn("Node for current environment does not exist and creation attempt failed at path `{Path}`.", environmentNodePath);
                        return false;
                    }
                }
                else
                {
                    log.Warn("Node for current environment does not exist at path '{Path}'.", environmentNodePath);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> DeleteNodeAsync()
        {
            var deleteResult = await zooKeeperClient.DeleteAsync(replicaNodePath).ConfigureAwait(false);
            return deleteResult.IsSuccessful;
        }

        private async Task TrySetTagsInNeeded()
        {
            if (tagsCreated)
                return;
            if (await SetTags().ConfigureAwait(false))
                tagsCreated.TrySetTrue();
        }

        private async Task RemoveTagsIfNeed()
        {
            if (tags == null || tags.Count == 0)
                return;
            await serviceDiscoveryManager.ClearReplicaTags(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica).ConfigureAwait(false);
        }

        private Task<bool> SetTags()
            => serviceDiscoveryManager.SetReplicaTags(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica, tags);

        #region EventSending

        private void SendStartEventIfNeeded()
        {
            using (new ServiceDiscoveryEventsContextToken(builder => builder.SetTimestamp(DateTimeOffset.Now)
                .SetKind(ServiceDiscoveryEventKind.ReplicaStarted)))
            {
                if (!nodeCreatedOnceSignal.IsCurrentlySet())
                    settings.ServiceDiscoveryEventContext.SendFromContext(builder => builder.SetDescription("Start registration in service discovery").SetDependencies(replicaInfo.Properties[ReplicaInfoKeys.Dependencies]));
                else if (registrationAllowedChanged)
                    settings.ServiceDiscoveryEventContext.SendFromContext(builder => builder.SetDescription("Registration has been allowed by RegistrationAllowedProvider."));
            }

            registrationAllowedChanged.TrySetFalse();
            // NOTE (shumilin): For each sending start event, we must allow sending the stop event.
            stopEventSent.TrySetFalse();
        }

        private void SendStopEventIfNeeded()
        {
            if (stopEventSent)
                return;

            settings.ServiceDiscoveryEventContext.SendFromContext(builder => builder.SetTimestamp(DateTimeOffset.Now).SetKind(ServiceDiscoveryEventKind.ReplicaStopped));
            stopEventSent.TrySetTrue();
        }

        #endregion
    }
}