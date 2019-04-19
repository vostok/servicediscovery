using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class ServiceDiscoveryPath_Tests
    {
        [Test]
        public void Build_combine_without_prefix()
        {
            var environment = "default";
            var application = "App.1";
            var replica = "http://some-infra-host123:13528/";

            var path = new ServiceDiscoveryPath(null);
            path.BuildEnvironmentPath(environment).Should().Be("/default");
            path.BuildApplicationPath(environment, application).Should().Be("/default/App.1");
            path.BuildReplicaPath(environment, application, replica).Should().Be("/default/App.1/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Build_should_combine_with_prefix()
        {
            var prefix = "/prefix/nested";
            var environment = "default";
            var application = "App.1";
            var replica = "http://some-infra-host123:13528/";

            var path = new ServiceDiscoveryPath(prefix);
            path.BuildEnvironmentPath(environment).Should().Be("/prefix/nested/default");
            path.BuildApplicationPath(environment, application).Should().Be("/prefix/nested/default/App.1");
            path.BuildReplicaPath(environment, application, replica).Should().Be("/prefix/nested/default/App.1/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Build_should_combine_with_prefix_with_slash()
        {
            var prefix = "/prefix/nested/";
            var environment = "default";
            var application = "App.1";
            var replica = "http://some-infra-host123:13528/";

            var path = new ServiceDiscoveryPath(prefix);
            path.BuildEnvironmentPath(environment).Should().Be("/prefix/nested/default");
            path.BuildApplicationPath(environment, application).Should().Be("/prefix/nested/default/App.1");
            path.BuildReplicaPath(environment, application, replica).Should().Be("/prefix/nested/default/App.1/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Build_should_encode_environment_in_lower_case()
        {
            new ServiceDiscoveryPath(null).BuildReplicaPath("EEE/eee", "s", "r").Should().Be("/eee%2Feee/s/r");
        }

        [Test]
        public void Build_should_encode_application()
        {
            new ServiceDiscoveryPath(null).BuildReplicaPath("e", "AAA/aaa", "r").Should().Be("/e/AAA%2Faaa/r");
        }

        [Test]
        public void Build_should_encode_replica()
        {
            new ServiceDiscoveryPath(null).BuildReplicaPath("e", "s", "RRR/rrr:88").Should().Be("/e/s/RRR%2Frrr%3A88");
        }

        [Test, Combinatorial]
        public void TryParse_should_parse_replica_path(
            [Values(null, "prefix/node")] string prefix,
            [Values("environment", "EEE/eee")] string environment,
            [Values("application", "AAA/aaa")] string application,
            [Values("replica", "RRR/rrr")] string replica)
        {
            var path = new ServiceDiscoveryPath(prefix);

            path.TryParse(path.BuildEnvironmentPath(environment))
                .Should().Be(((string environment, string application, string replica)?) (environment?.ToLowerInvariant(), null, null));

            path.TryParse(path.BuildApplicationPath(environment, application))
                .Should().Be(((string environment, string application, string replica)?)(environment?.ToLowerInvariant(), application, null));

            path.TryParse(path.BuildReplicaPath(environment, application, replica))
                .Should().Be(((string environment, string application, string replica)?)(environment?.ToLowerInvariant(), application, replica));
        }
    }
}