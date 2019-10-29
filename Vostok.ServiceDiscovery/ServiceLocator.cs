using System;
using System.Diagnostics;
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
    /// <inheritdoc cref="IServiceLocator"/>
    [PublicAPI]
    public class ServiceLocator : IServiceLocator, IDisposable
    {
        private const int NotStarted = 0;
        private const int Running = 1;
        private const int Disposed = 2;
        private readonly AtomicInt state = new AtomicInt(NotStarted);
        private readonly AsyncManualResetEvent updateCacheSignal = new AsyncManualResetEvent(true);
        private readonly EnvironmentsStorage environmentsStorage;
        private readonly ApplicationsStorage applicationsStorage;

        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceLocatorSettings settings;
        private readonly ServiceDiscoveryPathHelper pathHelper;
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

            pathHelper = new ServiceDiscoveryPathHelper(this.settings.ZooKeeperNodesPrefix, this.settings.ZooKeeperNodesPathEscaper);

            environmentsStorage = new EnvironmentsStorage(zooKeeperClient, pathHelper, log);
            applicationsStorage = new ApplicationsStorage(zooKeeperClient, pathHelper, log);
        }

        /// <inheritdoc />
        public IServiceTopology Locate(string environment, string application)
        {
            try
            {
                StartUpdateCacheTask();

                return LocateInner(environment, application);
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to locate '{Application}' application in '{Environment}' environment.", application, environment);
                return null;
            }
        }

        public void Dispose()
        {
            if (state.TryIncreaseTo(Disposed))
            {
                environmentsStorage.Dispose();
                applicationsStorage.Dispose();

                updateCacheSignal.Set();

                updateCacheTask?.GetAwaiter().GetResult();
            }
        }

        private IServiceTopology LocateInner(string environmentName, string applicationName)
        {
            var currentEnvironmentName = environmentName;

            // Note(kungurtsev): not return null, if application was found in some skipped environment.
            IServiceTopology firstResolved = null;

            for (var depth = 0; depth < settings.MaximumEnvironmentsDepth; depth++)
            {
                var environment = environmentsStorage.Get(currentEnvironmentName);
                if (environment == null)
                    return firstResolved;

                var topology = applicationsStorage.Get(currentEnvironmentName, applicationName).ServiceTopology;
                firstResolved = firstResolved ?? topology;

                var parentEnvironment = environment.ParentEnvironment;
                if (parentEnvironment == null)
                    return topology ?? firstResolved;

                var goToParent = topology == null || topology.Replicas.Count == 0 && environment.SkipIfEmpty();
                if (!goToParent)
                    return topology;

                currentEnvironmentName = parentEnvironment;
            }

            log.Warn("Cycled when resolving '{Application}' application in '{Environment}'.", applicationName, environmentName);
            return firstResolved;
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
                var sw = Stopwatch.StartNew();

                environmentsStorage.UpdateAll();
                applicationsStorage.UpdateAll();

                log.Info("Cache update iteration completed in {Elapsed}.", sw.Elapsed);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Failed cache update iteration.");
            }

            await updateCacheSignal.WaitAsync().WaitAsync(settings.IterationPeriod).ConfigureAwait(false);
            updateCacheSignal.Reset();
        }

        private void OnCompleted()
        {
            log.Warn("Someone else has disposed ZooKeeper client.");
            if (state.TryIncreaseTo(Disposed))
            {
                environmentsStorage.Dispose();
                applicationsStorage.Dispose();

                updateCacheSignal.Set();
                // Note(kungurtsev): does not wait updateCacheTask, because it will deadlock CachingObservable.
            }
        }

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.Connected)
                updateCacheSignal.Set();
        }
    }
}