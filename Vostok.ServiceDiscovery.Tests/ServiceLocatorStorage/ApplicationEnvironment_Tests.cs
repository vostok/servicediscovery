using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Helpers.Url;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage
{
    [TestFixture]
    internal class ApplicationEnvironment_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();

        private ApplicationEnvironment environment;

        private EnvironmentInfo environment1, environment2;
        private ApplicationInfo application1, application2;
        private string[] replicas1, replicas2;

        [SetUp]
        public void SetUp()
        {
            environment = new ApplicationEnvironment("name");

            environment1 = new EnvironmentInfo("parent1", new Dictionary<string, string>
            {
                {"key", "value1"}
            });

            environment2 = new EnvironmentInfo("parent2", new Dictionary<string, string>
            {
                {"key", "value2"}
            });

            application1 = new ApplicationInfo(new Dictionary<string, string>
            {
                {"key", "value1"}
            });

            application2 = new ApplicationInfo(new Dictionary<string, string>
            {
                {"key", "value2"}
            });

            replicas1 = new[] { "http://x.ru", "http://y.ru" };
            replicas2 = new string[0];
        }

        [Test]
        public void Should_initially_has_null_values()
        {
            environment.Name.Should().Be("name");
            environment.Environment.Should().BeNull();
            environment.ServiceTopology.Should().BeNull();
        }

        [Test]
        public void Should_store_environment_info()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);

            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
        }

        [Test]
        public void Should_store_application_properties()
        {
            environment.UpdateReplicas(ReplicasChildrenResult(new []{"x"}, 1), log);

            environment.UpdateApplication(ApplicationData(application1, 1), log);

            environment.ServiceTopology.Properties.Should().BeEquivalentTo(application1.Properties);

            environment.UpdateApplication(ApplicationData(application2, 2), log);

            environment.ServiceTopology.Properties.Should().BeEquivalentTo(application2.Properties);

            environment.UpdateApplication(ApplicationData(application1, 1), log);

            environment.ServiceTopology.Properties.Should().BeEquivalentTo(application2.Properties);
        }

        [Test]
        public void Should_store_replicas()
        {
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas1).Cast<object>());

            environment.UpdateReplicas(ReplicasChildrenResult(replicas2, 2), log);
            
            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas2).Cast<object>());

            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas2).Cast<object>());
        }

        [Test]
        public void Should_return_null_topology_without_replicas_initialization()
        {
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas1).Cast<object>());
        }

        [Test]
        public void Should_reset_environment_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplication(ApplicationData(application2, 2), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas2), application2.Properties));

            environment.UpdateEnvironment(GetDataResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, "", null), log);

            environment.Environment.Should().BeNull();
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplication(ApplicationData(application1, 1), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void Should_reset_application_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplication(ApplicationData(application2, 2), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas2), application2.Properties));

            environment.UpdateApplication(GetDataResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, "", null), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplication(ApplicationData(application1, 1), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void Should_reset_replicas_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplication(ApplicationData(application2, 2), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas2), application2.Properties));

            environment.UpdateReplicas(GetChildrenResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, "", null), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplication(ApplicationData(application1, 1), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void Should_not_update_with_invalid_environment_data()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplication(ApplicationData(application1, 1), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));

            environment.UpdateEnvironment(GetDataResult.Successful("", new byte[]{1, 2, 3}, new NodeStat(0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0)), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void Should_not_update_with_invalid_application_data()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplication(ApplicationData(application1, 1), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));

            environment.UpdateApplication(GetDataResult.Successful("", new byte[] { 1, 2, 3 }, new NodeStat(0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0)), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void Should_not_update_with_invalid_replicas()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplication(ApplicationData(application1, 1), log);
            environment.UpdateReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));

            environment.UpdateReplicas(GetChildrenResult.Successful("", new string[]{null}, new NodeStat(0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(new ServiceTopology(UrlParser.Parse(replicas1), application1.Properties));
        }

        private static GetDataResult EnvironmentData(EnvironmentInfo info, long zxId)
        {
            var bytes = EnvironmentNodeDataSerializer.Serialize(info);
            return GetDataResult.Successful("", bytes, new NodeStat(0, zxId, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        private static GetDataResult ApplicationData(ApplicationInfo info, long zxId)
        {
            var bytes = ApplicationNodeDataSerializer.Serialize(info);
            return GetDataResult.Successful("", bytes, new NodeStat(0, zxId, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        private static GetChildrenResult ReplicasChildrenResult(IEnumerable<string> children, long zxId)
        {
            return GetChildrenResult.Successful("", children.ToList(), new NodeStat(0, 0, zxId, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}