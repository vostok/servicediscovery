using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc cref="ServiceLocator"/>
    [PublicAPI]
    public class ServiceLocator : IServiceLocator, IDisposable
    {
        private const int NotStarted = 0;
        private const int Running = 1;
        private const int Disposed = 2;
        private readonly AtomicInt state = new AtomicInt(NotStarted);
        private readonly AsyncManualResetEvent updateCacheSignal = new AsyncManualResetEvent(true);

        private readonly ConcurrentDictionary<string, ApplicationEnvironments> applications = new ConcurrentDictionary<string, ApplicationEnvironments>();

        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceLocatorSettings settings;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ILog log;
        private volatile Task updateCacheTask;

        public ServiceLocator(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [CanBeNull] ServiceLocatorSettings settings = null,
            [CanBeNull] ILog log = null)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.settings = settings ?? new ServiceLocatorSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceLocator>();

            pathHelper = new ServiceDiscoveryPathHelper(this.settings.ZooKeeperNodesPrefix);
            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        /// <inheritdoc />
        public IServiceTopology Locate(string environment, string application)
        {
            try
            {
                StartUpdateCacheTask();

                var environments = applications.GetOrAdd(application, a => new ApplicationEnvironments(a, zooKeeperClient, nodeWatcher, pathHelper, log));
                return environments.Locate(environment);
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to locate '{Application}' application in '{Environment}' environment.", application, environment);
                return null;
            }
        }

        public void Dispose()
        {
            StopUpdateCacheTask();
        }

        private void StartUpdateCacheTask()
        {
            if (state != NotStarted)
                return;

            if (state.TryIncreaseTo(Running))
            {
                updateCacheTask = Task.Run(UpdateCacheTask);
            }
        }

        private void StopUpdateCacheTask()
        {
            if (state.TryIncreaseTo(Disposed))
            {
                updateCacheSignal.Set();
            }
        }

        private async Task UpdateCacheTask()
        {
            var observer = new AdHocConnectionStateObserver(OnConnectionStateChanged, OnCompleted);
            using (zooKeeperClient.OnConnectionStateChanged.Subscribe(observer))
            {
                while (state == Running)
                {
                    await UpdateCacheTaskIteration().ConfigureAwait(false);
                }
            }
        }

        private async Task UpdateCacheTaskIteration()
        {
            try
            {
                updateCacheSignal.Reset();

                foreach (var kvp in applications)
                {
                    kvp.Value.UpdateCache();
                }
            }
            catch (Exception exception)
            {
                log.Error(exception, "Failed update cache iteration.");
            }

            await updateCacheSignal.WaitAsync().WaitAsync(settings.IterationPeriod).ConfigureAwait(false);
        }

        private void UpdateCache(string environment, string application)
        {
            if (!applications.TryGetValue(application, out var environments))
            {
                log.Warn("Failed to update unexisting '{Application}'", application);
                return;
            }

            environments.UpdateCache(environment);
        }

        private void OnCompleted()
        {
            log.Warn("Someone else has disposed ZooKeeper client.");
            if (state.TryIncreaseTo(Disposed))
            {
                updateCacheSignal.Set();
                // Note(kungurtsev): does not wait updateCacheTask, because it will deadlock CachingObservable.
            }
        }

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.Connected)
                updateCacheSignal.Set();
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            var parsedPath = pathHelper.TryParse(path);

            if (parsedPath?.environment == null || parsedPath.Value.application == null || parsedPath.Value.replica != null)
            {
                log.Warn("Recieved node event of type '{NodeEventType}' on path '{NodePath}': not an application node.", type, path);
                return;
            }

            // Note(kungurtsev): run in new thread, because we shouldn't block ZooKeeperClient.
            Task.Run(() => UpdateCache(parsedPath.Value.environment, parsedPath.Value.application));
        }
    }
}