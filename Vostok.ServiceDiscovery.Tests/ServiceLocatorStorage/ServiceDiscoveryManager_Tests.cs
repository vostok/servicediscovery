using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage
{
    [TestFixture]
    internal class ServiceDiscoveryManager_Tests : TestsBase
    {
        [Test]
        public void GetEnvironmentAsync_should_return_not_null_if_environment_exists()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            CreateEnvironmentNode(replica.Environment);

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetEnvironmentAsync(replica.Environment)
                .GetAwaiter()
                .GetResult()
                .Should()
                .NotBeNull();
        }

        [Test]
        public void GetEnvironmentAsync_should_return_null_if_environment_does_not_exist()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var environment = "environment";

            CreateEnvironmentNode(replica.Environment);

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetEnvironmentAsync(environment)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void GetApplicationAsync_should_return_not_null_if_application_exists()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            CreateApplicationNode(replica.Environment, replica.Application);

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetApplicationAsync(replica.Environment, replica.Application)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo(new ApplicationInfo(replica.Environment, replica.Application, null));
        }

        [Test]
        public void GetApplicationAsync_should_return_null_if_application_does_not_exist()
        {
            CreateApplicationNode("notMyEnv", "notMyApp");

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetApplicationAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void GetAllEnvironmentsAsync_should_return_empty_array_if_no_environment_exists()
        {
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllEnvironmentsAsync()
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEmpty();
        }

        [Test]
        public void GetAllEnvironmentsAsync_should_return_all_existent_environments()
        {
            CreateEnvironmentNode("env1");
            CreateEnvironmentNode("env2");
            CreateEnvironmentNode("env3");

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllEnvironmentsAsync()
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("env1", "env2", "env3");
        }

        [Test]
        public void GetAllApplicationAsync_should_return_empty_array_if_there_are_no_application_in_environment()
        {
            CreateEnvironmentNode("env1");
            CreateApplicationNode("env2", "vostok");

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllApplicationsAsync("env1")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEmpty();
        }

        [Test]
        public void GetAllApplicationAsync_should_return_empty_array_if_environment_does_not_exists()
        {
            CreateEnvironmentNode("env1");

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllApplicationsAsync("env2")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEmpty();
        }

        [Test]
        public void GetAllApplicationAsync_should_return_all_applications_from_environment()
        {
            CreateApplicationNode("env1", "vostok1");
            CreateApplicationNode("env1", "vostok2");
            CreateApplicationNode("env2", "zapad1");

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllApplicationsAsync("env1")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("vostok1", "vostok2");
        }

        [Test]
        public void TryCreateEnvironmentAsync_should_return_true_and_create_new_environment()
        {
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            var environmentInfo = new EnvironmentInfo("default", "parent", GetProperties());

            serviceDiscoveryManager.TryCreateEnvironmentAsync(environmentInfo)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo(environmentInfo);
        }

        [Test]
        public void TryCreateEnvironmentAsync_should_return_false_and_not_create_environment_if_it_exists()
        {
            CreateEnvironmentNode("default", "parent", GetProperties());
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            var environmentInfo = new EnvironmentInfo("default", "parent", new Dictionary<string, string> {["prop"] = "propValue"});

            serviceDiscoveryManager.TryCreateEnvironmentAsync(environmentInfo)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            serviceDiscoveryManager.GetEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo(new EnvironmentInfo("default", "parent", GetProperties()));
        }

        [Test]
        public void TryDeleteEnvironmentAsync_should_return_true_and_delete_existent_environment_without_children()
        {
            CreateEnvironmentNode("default", "parent", GetProperties());
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeleteEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void TryDeleteEnvironmentAsync_should_return_true_and_delete_existent_environment_with_children()
        {
            CreateEnvironmentNode("default", "parent", GetProperties());
            CreateApplicationNode("default", "vostok", GetProperties());
            CreateApplicationNode("default", "zapad", GetProperties());
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeleteEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void TryDeleteEnvironmentAsync_should_return_true_for_non_existent_environment()
        {
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeleteEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void TryCreatePermanentReplicaAsync_should_return_true_and_create_new_replica_if_application_exists()
        {
            CreateApplicationNode("default", "vostok");

            var replicaInfo = new ReplicaInfo("default", "vostok", "replica1", GetProperties());
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryCreatePermanentReplicaAsync(replicaInfo)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager
                .GetAllReplicasAsync(replicaInfo.Environment, replicaInfo.Application)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("replica1");
        }

        [Test]
        public void TryCreatePermanentReplicaAsync_should_return_true_and_create_new_replica_environment_and_application_if_they_do_not_exist()
        {
            var replicaInfo = new ReplicaInfo("default", "vostok", "replica1", GetProperties());
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryCreatePermanentReplicaAsync(replicaInfo)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager
                .GetAllReplicasAsync(replicaInfo.Environment, replicaInfo.Application)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("replica1");

            serviceDiscoveryManager
                .GetEnvironmentAsync("default")
                .GetAwaiter()
                .GetResult()
                .Should()
                .NotBeNull();

            serviceDiscoveryManager
                .GetApplicationAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .NotBeNull();
        }

        [Test]
        public void GetAllReplicasAsync_should_return_empty_array_if_no_replicas_exists()
        {
            CreateApplicationNode("default", "vostok", GetProperties());

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllReplicasAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEmpty();
        }

        [Test]
        public void GetAllReplicasAsync_should_return_all_existent_replicas_from_environment_and_application()
        {
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "vr1", GetProperties()));
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "vr2", GetProperties()));
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "vr3", GetProperties()));
            CreateReplicaNode(new ReplicaInfo("default", "zapad", "zzzz111", GetProperties()));
            CreateReplicaNode(new ReplicaInfo("worldofpain", "vostok", "665", GetProperties()));

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.GetAllReplicasAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("vr1", "vr2", "vr3");
        }

        [Test]
        public void TryUpdateEnvironmentPropertiesAsync_should_return_true_and_set_new_properties_for_existent_environment()
        {
            var initProperties = GetProperties();
            var environment = "environment";
            CreateEnvironmentNode(environment, properties: initProperties);
            Dictionary<string, string> updatedProperties = null;

            IEnvironmentInfoProperties UpdatePropertiesFunc(IEnvironmentInfoProperties props)
            {
                updatedProperties = props.ToDictionary(kvp => kvp.Key, kvp => $"{kvp.Value}_updated");
                return new EnvironmentInfoProperties(updatedProperties);
            }

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);
            serviceDiscoveryManager.GetEnvironmentAsync(environment)
                .GetAwaiter()
                .GetResult()
                .Properties
                .Should()
                .BeEquivalentTo(initProperties);

            serviceDiscoveryManager.TryUpdateEnvironmentPropertiesAsync(environment, UpdatePropertiesFunc)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetEnvironmentAsync(environment)
                .GetAwaiter()
                .GetResult()
                .Properties
                .Should()
                .BeEquivalentTo(updatedProperties);
        }

        [Test]
        public void TryUpdateEnvironmentParentAsync_should_return_false_if_environment_does_not_exist()
        {
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            const string newParent = "newWorldRoot";
            serviceDiscoveryManager.TryUpdateEnvironmentParentAsync("environment", newParent)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            serviceDiscoveryManager.GetEnvironmentAsync("environment")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void TryUpdateEnvironmentParentAsync_should_return_true_and_set_new_parent_for_existent_environment()
        {
            const string oldParent = "root";
            CreateEnvironmentNode("environment", oldParent, GetProperties());

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);
            serviceDiscoveryManager.GetEnvironmentAsync("environment")
                .GetAwaiter()
                .GetResult()
                .ParentEnvironment
                .Should()
                .BeEquivalentTo(oldParent);

            const string newParent = "newWorldRoot";
            serviceDiscoveryManager.TryUpdateEnvironmentParentAsync("environment", newParent)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetEnvironmentAsync("environment")
                .GetAwaiter()
                .GetResult()
                .ParentEnvironment
                .Should()
                .BeEquivalentTo(newParent);
        }

        private static Dictionary<string, string> GetProperties() =>
            new Dictionary<string, string>
            {
                ["propKey1"] = "propValue1",
                ["propKey2"] = "propValue2",
                ["propKey3"] = "propValue3"
            };

        [Test]
        public void TryUpdateEnvironmentPropertiesAsync_should_return_false_and_do_not_create_non_existent_environment()
        {
            const string environment = "environment";

            IEnvironmentInfoProperties UpdatePropertiesFunc(IEnvironmentInfoProperties props)
            {
                var updatedProperties = props.ToDictionary(kvp => kvp.Key, kvp => $"{kvp.Value}_updated");
                return new EnvironmentInfoProperties(updatedProperties);
            }

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryUpdateEnvironmentPropertiesAsync(environment, UpdatePropertiesFunc)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            serviceDiscoveryManager.GetEnvironmentAsync(environment)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void TryUpdateApplicationPropertiesAsync_should_return_true_and_set_new_properties_for_existent_application()
        {
            var initProperties = GetProperties();
            CreateApplicationNode("environment", "vostok", properties: initProperties);
            Dictionary<string, string> updatedProperties = null;

            IApplicationInfoProperties UpdatePropertiesFunc(IApplicationInfoProperties props)
            {
                updatedProperties = props.ToDictionary(kvp => kvp.Key, kvp => $"{kvp.Value}_updated");
                return new ApplicationInfoProperties(updatedProperties);
            }

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);
            serviceDiscoveryManager.GetApplicationAsync("environment", "vostok")
                .GetAwaiter()
                .GetResult()
                .Properties
                .Should()
                .BeEquivalentTo(initProperties);

            serviceDiscoveryManager.TryUpdateApplicationPropertiesAsync("environment", "vostok", UpdatePropertiesFunc)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetApplicationAsync("environment", "vostok")
                .GetAwaiter()
                .GetResult()
                .Properties
                .Should()
                .BeEquivalentTo(updatedProperties);
        }

        [Test]
        public void TryUpdateApplicationPropertiesAsync_should_return_false_and_do_not_create_non_existent_environment_nor_non_existent_application()
        {
            const string environment = "environment";
            const string application = "vostok";

            IApplicationInfoProperties UpdatePropertiesFunc(IApplicationInfoProperties props)
            {
                var updatedProperties = props.ToDictionary(kvp => kvp.Key, kvp => $"{kvp.Value}_updated");
                return new ApplicationInfoProperties(updatedProperties);
            }

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryUpdateApplicationPropertiesAsync(environment, application, UpdatePropertiesFunc)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            serviceDiscoveryManager.GetApplicationAsync(environment, application)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();

            serviceDiscoveryManager.GetEnvironmentAsync(environment)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeNull();
        }

        [Test]
        public void TryDeletePermanentReplicaAsync_should_return_true_for_non_existent_replica()
        {
            var replicaInfo = new ReplicaInfo("default", "vostok", "replica1");

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeletePermanentReplicaAsync(replicaInfo)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetAllReplicasAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEmpty();
        }

        [Test]
        public void TryDeletePermanentReplicaAsync_should_return_true_and_delete_one_existent_replica()
        {
            var replicaToDelete = new ReplicaInfo("default", "vostok", "replica1");
            CreateReplicaNode(replicaToDelete);
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "replica2"));
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "replica3"));

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeletePermanentReplicaAsync(replicaToDelete)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetAllReplicasAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("replica2", "replica3");
        }
    }
}