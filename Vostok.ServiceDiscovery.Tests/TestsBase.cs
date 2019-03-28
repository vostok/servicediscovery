using System;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ZooKeeper.Client;
using Vostok.ZooKeeper.LocalEnsemble;

namespace Vostok.ServiceDiscovery.Tests
{
    internal abstract class TestsBase
    {
        protected static TimeSpan DefaultTimeout = 10.Seconds();
        protected readonly ILog Log = new SynchronousConsoleLog();
        protected ZooKeeperEnsemble Ensemble;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
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

        protected ZooKeeperClient GetClient()
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString) {Timeout = DefaultTimeout, LoggingLevel = LogLevel.Debug};
            return new ZooKeeperClient(settings, Log);
        }
    }
}