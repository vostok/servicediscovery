using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Helpers.Url;
using Vostok.Commons.Testing;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class ServiceLocator_Tests : TestsBase
    {
        [Test]
        public void Should_locate_registered_ServiceBeacon_service()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                WaitReplicaRegistered(replica);

                using (var locator = GetServiceLocator())
                {
                    ShouldLocateImmediately(locator, replica.Environment, replica.Application, replica.Replica);
                }
            }
        }

        [Test]
        public void Should_locate_ServiceBeacon_service_after_registration()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                using (var locator = GetServiceLocator())
                {
                    ShouldNotLocateImmediately(locator, replica.Environment, replica.Application);

                    beacon.Start();
                    WaitReplicaRegistered(replica);

                    ShouldLocate(locator, replica.Environment, replica.Application, replica.Replica);
                }
            }
        }

        private void ShouldLocate(ServiceLocator locator, string environment, string application, params string[] replicas)
        {
            Action assertion = () =>
            {
                ShouldLocateImmediately(locator, environment, application, replicas);
            };
            assertion.ShouldPassIn(DefaultTimeout);
        }

        private void ShouldNotLocate(ServiceLocator locator, string environment, string application)
        {
            Action assertion = () =>
            {
                ShouldNotLocateImmediately(locator, environment, application);
            };
            assertion.ShouldPassIn(DefaultTimeout);
        }

        private static void ShouldLocateImmediately(ServiceLocator locator, string environment, string application, params string[] replicas)
        {
            var topology = locator.Locate(environment, application);
            topology.Should().NotBeNull();
            topology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas).Cast<object>());
        }

        private static void ShouldNotLocateImmediately(ServiceLocator locator, string environment, string application)
        {
            locator.Locate(environment, application).Should().BeNull();
        }

        private ServiceLocator GetServiceLocator()
        {
            return new ServiceLocator(ZooKeeperClient, null, Log);
        }
    }
}