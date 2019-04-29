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

            environment1 = new EnvironmentInfo(
                "parent1",
                new Dictionary<string, string>
                {
                    {"key", "value1"}
                });

            environment2 = new EnvironmentInfo(
                "parent2",
                new Dictionary<string, string>
                {
                    {"key", "value2"}
                });

            application1 = new ApplicationInfo(
                new Dictionary<string, string>
                {
                    {"key", "value1"}
                });

            application2 = new ApplicationInfo(
                new Dictionary<string, string>
                {
                    {"key", "value2"}
                });

            replicas1 = new[] {"http://x.ru", "http://y.ru"};
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
        public void NeedUpdateApplicationInfo_should_works_correctly()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 2), log);

            environment.NeedUpdateApplicationInfo(ApplicationExists(1, 0)).Should().BeFalse();
            environment.NeedUpdateApplicationInfo(ApplicationExists(2, 0)).Should().BeFalse();
            environment.NeedUpdateApplicationInfo(ApplicationExists(3, 0)).Should().BeTrue();
        }

        [Test]
        public void NeedUpdateApplicationReplicas_should_works_correctly()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 2), log);

            environment.NeedUpdateApplicationReplicas(ApplicationExists(0, 1)).Should().BeFalse();
            environment.NeedUpdateApplicationReplicas(ApplicationExists(0, 2)).Should().BeFalse();
            environment.NeedUpdateApplicationReplicas(ApplicationExists(0, 3)).Should().BeTrue();
        }

        [Test]
        public void UpdateEnvironment_should_works_correctly()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);

            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
        }

        [Test]
        public void UpdateApplicationInfo_should_works_correctly()
        {
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(new[] {"x"}, 1), log);

            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);

            environment.ServiceTopology.Properties.Should().BeEquivalentTo(application1.Properties);

            environment.UpdateApplicationInfo(ApplicationData(application2, 2), log);

            environment.ServiceTopology.Properties.Should().BeEquivalentTo(application2.Properties);

            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);

            environment.ServiceTopology.Properties.Should().BeEquivalentTo(application2.Properties);
        }

        [Test]
        public void UpdateApplicationReplicas_should_works_correctly()
        {
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas1).Cast<object>());

            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas2).Cast<object>());

            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas2).Cast<object>());
        }

        [Test]
        public void ServiceTopology_should_be_null_topology_without_replicas_initialization()
        {
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.ServiceTopology.Replicas.Should().BeEquivalentTo(UrlParser.Parse(replicas1).Cast<object>());
        }

        [Test]
        public void NeedUpdateApplicationInfo_should_reset_environment_and_topology_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplicationInfo(ApplicationData(application2, 2), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas2), application2.Properties));

            environment.NeedUpdateApplicationInfo(ExistsResult.Successful("", null)).Should().BeFalse();

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void NeedUpdateApplicationReplicas_should_reset_environment_and_topology_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplicationInfo(ApplicationData(application2, 2), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas2), application2.Properties));

            environment.NeedUpdateApplicationReplicas(ExistsResult.Successful("", null)).Should().BeFalse();

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void UpdateEnvironment_should_reset_environment_and_topology_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplicationInfo(ApplicationData(application2, 2), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas2), application2.Properties));

            environment.UpdateEnvironment(GetDataResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, "", null), log);

            environment.Environment.Should().BeNull();
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void UpdateApplicationInfo_should_reset_topology_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplicationInfo(ApplicationData(application2, 2), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas2), application2.Properties));

            environment.UpdateApplicationInfo(GetDataResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, "", null), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void UpdateApplicationReplicas_should_reset_topology_when_node_not_found()
        {
            environment.UpdateEnvironment(EnvironmentData(environment2, 2), log);
            environment.UpdateApplicationInfo(ApplicationData(application2, 2), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas2, 2), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas2), application2.Properties));

            environment.UpdateApplicationReplicas(GetChildrenResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, "", null), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeNull();

            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment2);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void UpdateEnvironment_should_ignore_invalid_data()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));

            environment.UpdateEnvironment(GetDataResult.Successful("", new byte[] {1, 2, 3}, new NodeStat(0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0)), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void UpdateApplicationInfo_should_ignore_invalid_data()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));

            environment.UpdateApplicationInfo(GetDataResult.Successful("", new byte[] {1, 2, 3}, new NodeStat(0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0)), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        [Test]
        public void UpdateApplicationReplicas_should_ignore_invalid_data()
        {
            environment.UpdateEnvironment(EnvironmentData(environment1, 1), log);
            environment.UpdateApplicationInfo(ApplicationData(application1, 1), log);
            environment.UpdateApplicationReplicas(ReplicasChildrenResult(replicas1, 1), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));

            environment.UpdateApplicationReplicas(GetChildrenResult.Successful("", new string[] {null}, new NodeStat(0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)), log);

            environment.Environment.Should().BeEquivalentTo(environment1);
            environment.ServiceTopology.Should().BeEquivalentTo(ServiceTopology.Build(UrlParser.Parse(replicas1), application1.Properties));
        }

        private static GetDataResult EnvironmentData(EnvironmentInfo info, long zxId)
        {
            var bytes = EnvironmentNodeDataSerializer.Serialize(info);
            return GetDataResult.Successful("", bytes, new NodeStat(0, zxId, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        private static ExistsResult ApplicationExists(long modifiedZxId, long childrenZxId)
        {
            return ExistsResult.Successful("", new NodeStat(0, modifiedZxId, childrenZxId, 0, 0, 0, 0, 0, 0, 0, 0));
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