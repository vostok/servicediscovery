using System;
using System.Collections.Generic;
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

        [Test]
        public void Should_skip_environment_without_application()
        {
            var replica = new ReplicaInfo("parent", "vostok", "https://github.com/vostok");
            
            CreateEnvironmentNode("parent");
            CreateEnvironmentNode("child", "parent");

            CreateApplicationNode("parent", "vostok");

            CreateReplicaNode(replica);

            using (var locator = GetServiceLocator())
            {
                ShouldLocate(locator, "child", "vostok", replica.Replica);
                ShouldLocate(locator, "parent", "vostok", replica.Replica);
            }
        }

        [Test]
        public void Should_not_skip_environment_with_application()
        {
            var replicaParent = new ReplicaInfo("parent", "vostok", "https://github.com/vostok/parent");
            var replicaChild = new ReplicaInfo("child", "vostok", "https://github.com/vostok/child");

            CreateEnvironmentNode("parent");
            CreateEnvironmentNode("child", "parent");

            CreateApplicationNode("child", "vostok");
            CreateApplicationNode("parent", "vostok");

            CreateReplicaNode(replicaParent);

            using (var locator = GetServiceLocator())
            {
                ShouldLocate(locator, "child", "vostok");
                ShouldLocate(locator, "parent", "vostok", replicaParent.Replica);

                CreateReplicaNode(replicaChild);

                ShouldLocate(locator, "child", "vostok", replicaChild.Replica);
                ShouldLocate(locator, "parent", "vostok", replicaParent.Replica);
            }
        }

        [Test]
        public void Should_skip_environment_with_application_if_specified()
        {
            var replicaParent = new ReplicaInfo("parent", "vostok", "https://github.com/vostok/parent");
            var replicaChild = new ReplicaInfo("child", "vostok", "https://github.com/vostok/child");

            CreateEnvironmentNode("parent");
            CreateEnvironmentNode("child", "parent", new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, "True"}});

            CreateApplicationNode("child", "vostok");
            CreateApplicationNode("parent", "vostok");

            CreateReplicaNode(replicaParent);

            using (var locator = GetServiceLocator())
            {
                ShouldLocate(locator, "child", "vostok", replicaParent.Replica);
                ShouldLocate(locator, "parent", "vostok", replicaParent.Replica);

                CreateReplicaNode(replicaChild);

                ShouldLocate(locator, "child", "vostok", replicaChild.Replica);
                ShouldLocate(locator, "parent", "vostok", replicaParent.Replica);
            }
        }

        [Test]
        public void Should_track_replicas_registration_in_nested_environments()
        {
            var replica1Parent = new ReplicaInfo("parent", "vostok", "https://github.com/vostok1/parent");
            var replica2Parent = new ReplicaInfo("parent", "vostok", "https://github.com/vostok2/parent");
            var replica1Child = new ReplicaInfo("child", "vostok", "https://github.com/vostok1/child");
            var replica2Child = new ReplicaInfo("child", "vostok", "https://github.com/vostok2/child");

            CreateEnvironmentNode("parent");
            CreateEnvironmentNode("child", "parent");

            using (var locator = GetServiceLocator())
            {
                ShouldNotLocate(locator, "parent", "vostok");
                ShouldNotLocate(locator, "child", "vostok");

                CreateApplicationNode("parent", "vostok");

                ShouldLocate(locator, "parent", "vostok");
                ShouldLocate(locator, "child", "vostok");

                CreateReplicaNode(replica1Parent);
                ShouldLocate(locator, "parent", "vostok", replica1Parent.Replica);
                ShouldLocate(locator, "child", "vostok", replica1Parent.Replica);

                CreateReplicaNode(replica2Parent);
                ShouldLocate(locator, "parent", "vostok", replica1Parent.Replica, replica2Parent.Replica);
                ShouldLocate(locator, "child", "vostok", replica1Parent.Replica, replica2Parent.Replica);

                CreateApplicationNode("child", "vostok");
                ShouldLocate(locator, "parent", "vostok", replica1Parent.Replica, replica2Parent.Replica);
                ShouldLocate(locator, "child", "vostok");

                CreateReplicaNode(replica1Child);
                ShouldLocate(locator, "parent", "vostok", replica1Parent.Replica, replica2Parent.Replica);
                ShouldLocate(locator, "child", "vostok", replica1Child.Replica);

                CreateReplicaNode(replica2Child);
                ShouldLocate(locator, "parent", "vostok", replica1Parent.Replica, replica2Parent.Replica);
                ShouldLocate(locator, "child", "vostok", replica1Child.Replica, replica2Child.Replica);
            }
        }

        [Test]
        public void Should_track_replica_deletion()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            CreateEnvironmentNode("default");

            CreateApplicationNode("default", "vostok");

            CreateReplicaNode(replica);

            using (var locator = GetServiceLocator())
            {
                ShouldLocate(locator, "default", "vostok", replica.Replica);

                DeleteReplicaNode(replica);

                ShouldLocate(locator, "default", "vostok");
            }
        }

        [Test]
        public void Should_track_application_deletion()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            CreateEnvironmentNode("default");

            CreateApplicationNode("default", "vostok");

            CreateReplicaNode(replica);

            using (var locator = GetServiceLocator())
            {
                ShouldLocate(locator, "default", "vostok", replica.Replica);

                DeleteApplicationNode("default", "vostok");

                ShouldNotLocate(locator, "default", "vostok");
            }
        }

        [Test]
        public void Should_track_environment_deletion()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            CreateEnvironmentNode("default");

            CreateApplicationNode("default", "vostok");

            CreateReplicaNode(replica);

            using (var locator = GetServiceLocator())
            {
                ShouldLocate(locator, "default", "vostok", replica.Replica);

                DeleteEnvironmentNode("default");

                ShouldNotLocate(locator, "default", "vostok");
            }
        }

        [Test]
        public void Should_return_application_properties()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var properties = new Dictionary<string, string>
            {
                {"key1", "value1"},
                {"key2", "value2"}
            };

            CreateEnvironmentNode("default");

            CreateApplicationNode("default", "vostok");

            CreateReplicaNode(replica);

            using (var locator = GetServiceLocator())
            {
                locator.Locate("default", "vostok").Properties.Should().AllBeEquivalentTo(properties);
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

        private static void ShouldLocateImmediately(ServiceLocator locator, string environment, string application, params string[] replicas)
        {
            var topology = locator.Locate(environment, application);
            topology.Should().NotBeNull();
            topology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas).Cast<object>());
        }

        private void ShouldNotLocate(ServiceLocator locator, string environment, string application)
        {
            Action assertion = () =>
            {
                ShouldNotLocateImmediately(locator, environment, application);
            };
            assertion.ShouldPassIn(DefaultTimeout);
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