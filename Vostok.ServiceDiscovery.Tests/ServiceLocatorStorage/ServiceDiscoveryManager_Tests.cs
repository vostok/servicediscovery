using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage
{
    internal class ServiceDiscoveryManager_Tests : TestsBase
    {        

        [Test]
        public void CheckZoneExistenceAsync_should_return_true_if_environment_exists()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var properties = new Dictionary<string, string>
            {
                ["propKey1"] = "propValue1",
                ["propKey2"] = "propValue2",
                ["propKey3"] = "propValue3"
            };
            CreateEnvironmentNode(replica.Environment, properties: properties);

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.CheckZoneExistenceAsync(PathHelper.BuildEnvironmentPath(replica.Environment))
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();
        }

        [Test]
        public void CheckZoneExistenceAsync_should_return_false_if_environment_does_not_exist()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var environment = "environment";
            var properties = new Dictionary<string, string>
            {
                ["propKey1"] = "propValue1",
                ["propKey2"] = "propValue2",
                ["propKey3"] = "propValue3"
            };
            CreateEnvironmentNode(replica.Environment, properties: properties);

            var serviceDiscoveryManager = new ServiceDiscoveryManager(GetZooKeeperClient(), log: Log);

            serviceDiscoveryManager.CheckZoneExistenceAsync(PathHelper.BuildEnvironmentPath(environment))
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();
        }

        [Test]
        public void UpdateEnvironmentPropertiesAsync_should_return_true_and_set_new_properties_for_environment()
        {
            var initProperties = new Dictionary<string, string>
            {
                ["propKey1"] = "propValue1",
                ["propKey2"] = "propValue2",
                ["propKey3"] = "propValue3"
            };
            var environment = "environment";
            CreateEnvironmentNode(environment, properties: initProperties);
            Dictionary<string, string> updatedProperties = null;

            IServiceTopologyProperties UpdatePropertiesFunc(IServiceTopologyProperties props)
            {
                updatedProperties = props.ToDictionary(kvp => kvp.Key, kvp => $"{kvp.Value}_updated");
                return new ServiceTopologyProperties(updatedProperties);
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

        // CR(kungurtsev): tests for other methods from IServiceDiscoveryManager.
    }
}