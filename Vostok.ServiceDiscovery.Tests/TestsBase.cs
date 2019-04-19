using System;
using System.Threading.Tasks;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client;
using Vostok.ZooKeeper.LocalEnsemble;
using Vostok.ZooKeeper.Testing;

namespace Vostok.ServiceDiscovery.Tests
{
    internal abstract class TestsBase
    {
        protected static TimeSpan DefaultTimeout = 10.Seconds();
        protected readonly ILog Log = new SynchronousConsoleLog();
        protected ZooKeeperEnsemble Ensemble;
        protected ZooKeeperClient ZooKeeperClient;
        protected readonly ServiceDiscoveryPath ServiceDiscoveryPath = new ServiceDiscoveryPath(new ServiceBeaconSettings().ZooKeeperNodePath);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
            ZooKeeperClient = GetZooKeeperClient();
        }

        [SetUp]
        public void SetUp()
        {
            if (!Ensemble.IsRunning)
                Ensemble.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Ensemble.Dispose();
        }

        protected Task KillSession(ZooKeeperClient client) =>
            ZooKeeperClientTestsHelper.KillSession(client.SessionId, client.SessionPassword, client.OnConnectionStateChanged, Ensemble.ConnectionString, DefaultTimeout);

        protected ZooKeeperClient GetZooKeeperClient()
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString) {Timeout = DefaultTimeout};
            return new ZooKeeperClient(settings, Log);
        }
    }
}