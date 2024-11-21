using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Telemetry;
using Vostok.ServiceDiscovery.Telemetry.Event;
using Vostok.ServiceDiscovery.Telemetry.EventsSender;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

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
            var updatedProperties = new Dictionary<string, string>()
            {
                ["updatedKey"] = "updatedValue"
            };

            IEnvironmentInfoProperties UpdatePropertiesFunc(IEnvironmentInfoProperties props)
            {
                foreach (var property in updatedProperties)
                    props = props.Set(property.Key, property.Value);
                return props;
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
                .BeEquivalentTo(initProperties.Concat(updatedProperties));
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

            var updatedProperties = new Dictionary<string, string>()
            {
                ["updatedKey"] = "updatedValue"
            };

            IEnvironmentInfoProperties UpdatePropertiesFunc(IEnvironmentInfoProperties props)
            {
                foreach (var property in updatedProperties)
                    props.Set(property.Key, property.Value);
                return props;
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
            var updatedProperties = new Dictionary<string, string>
            {
                ["updatedKey"] = "updatedValue"
            };

            IApplicationInfoProperties UpdatePropertiesFunc(IApplicationInfoProperties props)
            {
                foreach (var property in updatedProperties)
                    props = props.Set(property.Key, property.Value);
                return props;
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

            Action assert = () => serviceDiscoveryManager.GetApplicationAsync("environment", "vostok")
                .GetAwaiter()
                .GetResult()
                .Properties
                .Should()
                .BeEquivalentTo(initProperties.Concat(updatedProperties));
        }

        [Test]
        public void TryUpdateApplicationPropertiesAsync_should_return_false_and_do_not_create_non_existent_environment_nor_non_existent_application()
        {
            const string environment = "environment";
            const string application = "vostok";

            var updatedProperties = new Dictionary<string, string>
            {
                ["updatedKey"] = "updatedValue"
            };

            IApplicationInfoProperties UpdatePropertiesFunc(IApplicationInfoProperties props)
            {
                foreach (var property in updatedProperties)
                    props.Set(property.Key, property.Value);
                return props;
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
        public void TryUpdateApplicationPropertiesAsync_should_send_events_from_context_description()
        {
            const string environment = "environment";
            const string application = "vostok";
            const string replica = "https://github.com/vostok";
            var client = GetZooKeeperClient();
            var initProperties = GetProperties();
            CreateApplicationNode(environment, application, initProperties);

            var sender = Substitute.For<IServiceDiscoveryEventsSender>();
            ServiceDiscoveryEvent received = null;
            sender.Send(Arg.Do<ServiceDiscoveryEvent>(serviceDiscoveryEvent => received = serviceDiscoveryEvent));

            var setting = new ServiceDiscoveryManagerSettings {ServiceDiscoveryEventContext = new ServiceDiscoveryEventsContext(new ServiceDiscoveryEventsContextConfig(sender))};
            var serviceDiscoveryManager = new ServiceDiscoveryManager(client, setting, Log);
            var expected = new ServiceDiscoveryEvent(ServiceDiscoveryEventKind.ReplicaRemovedFromBlacklist, environment, application, replica, DateTimeOffset.UtcNow, new Dictionary<string, string>());
            new ServiceDiscoveryEventsContextToken(builder => builder.SetKind(ServiceDiscoveryEventKind.ReplicaRemovedFromBlacklist).AddReplicas(replica));

            serviceDiscoveryManager.TryUpdateApplicationPropertiesAsync(environment, application, properties => properties.Set("updatedKey", "updatedValue"))
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();
            received.Should().BeEquivalentTo(expected, options => options.Excluding(serviceDiscoveryEvent => serviceDiscoveryEvent.Timestamp));

            client.Dispose();
        }

        [Test]
        public void TrySetNewReplicaPropertiesAsync_should_rewrite_existing_replica_properties()
        {
            const string environment = "default";
            const string application = "vostok";
            const string replica = "replica";

            var updatedProperties = new Dictionary<string, string>
            {
                ["updatedKey"] = "updatedValue"
            };
            
            CreateReplicaNode(new ReplicaInfo(environment, application, replica, GetProperties()));
            
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);
            
            serviceDiscoveryManager.TrySetNewReplicaPropertiesAsync(environment, application, replica, updatedProperties)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();

            serviceDiscoveryManager.GetReplicaAsync(environment, application, replica)
                .GetAwaiter()
                .GetResult()
                .Properties.Should()
                .BeEquivalentTo(updatedProperties);
        } 
        
        [Test]
        public void TryUpdateReplicaPropertiesAsync_should_not_rewrite_existing_replica_properties()
        {
            const string environment = "default";
            const string application = "vostok";
            const string replica = "replica";

            var replicaProperties = GetProperties();

            Func<Dictionary<string, string>, Dictionary<string, string>> updatedFunc = x =>
            {
                x["updatedKey"] = "updatedValue";

                return x;
            };
            
            CreateReplicaNode(new ReplicaInfo(environment, application, replica, GetProperties()));
            
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);
            
            serviceDiscoveryManager.TryUpdateReplicaPropertiesAsync(environment, application, replica, updatedFunc)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();
            
            serviceDiscoveryManager.GetReplicaAsync(environment, application, replica)
                .GetAwaiter()
                .GetResult()
                .Properties.Should()
                .BeEquivalentTo(replicaProperties.Concat(new Dictionary<string, string> { { "updatedKey", "updatedValue" } }));
        }
        
        [Test]
        public void TrySetNewReplicaPropertiesAsync_should_not_recreate_replica()
        {
            const string environment = "default";
            const string application = "vostok";
            const string replica = "replica";

            var updatedProperties = new Dictionary<string, string>
            {
                ["updatedKey"] = "updatedValue"
            };
            
            CreateReplicaNode(new ReplicaInfo(environment, application, replica, GetProperties()));
            
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);
            var version = ZooKeeperClient.GetDataAsync(new GetDataRequest(PathHelper.BuildApplicationPath(environment, application))).GetAwaiter().GetResult().Stat.ChildrenVersion;
            
            serviceDiscoveryManager.TrySetNewReplicaPropertiesAsync(environment, application, replica, updatedProperties)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();
            
            var version2 = ZooKeeperClient.GetDataAsync(new GetDataRequest(PathHelper.BuildApplicationPath(environment, application))).GetAwaiter().GetResult().Stat.ChildrenVersion;

            version2.Should().Be(version);
        }

        [Test]
        public void TryDeletePermanentReplicaAsync_should_return_true_for_non_existent_replica()
        {
            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeletePermanentReplicaAsync("default", "vostok", "replica1")
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

            serviceDiscoveryManager.TryDeletePermanentReplicaAsync(replicaToDelete.Environment, replicaToDelete.Application, replicaToDelete.Replica)
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

        [Test]
        public void TryDeletePermanentReplicaAsync_should_return_false_and_do_not_delete_ephemeral_replica()
        {
            var replicaToDelete = new ReplicaInfo("default", "vostok", "replica1");
            CreateReplicaNode(replicaToDelete, false);
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "replica2"));
            CreateReplicaNode(new ReplicaInfo("default", "vostok", "replica3"));

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.TryDeletePermanentReplicaAsync(replicaToDelete.Environment, replicaToDelete.Application, replicaToDelete.Replica)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            serviceDiscoveryManager.GetAllReplicasAsync("default", "vostok")
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeEquivalentTo("replica1", "replica2", "replica3");
        }
    }
}