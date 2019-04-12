using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class PathBuilder_Tests
    {
        [Test]
        public void Should_combine_without_prefix()
        {
            var environment = "default";
            var application = "App.1";
            var replica = "http://some-infra-host123:13528/";

            var pathBuilder = new PathBuilder(null);
            pathBuilder.BuildEnvironmentPath(environment).Should().Be("/default");
            pathBuilder.BuildApplicationPath(environment, application).Should().Be("/default/App.1");
            pathBuilder.BuildReplicaPath(environment, application, replica).Should().Be("/default/App.1/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Should_combine_with_prefix()
        {
            var prefix = "/prefix/nested";
            var environment = "default";
            var application = "App.1";
            var replica = "http://some-infra-host123:13528/";

            var pathBuilder = new PathBuilder(prefix);
            pathBuilder.BuildEnvironmentPath(environment).Should().Be("/prefix/nested/default");
            pathBuilder.BuildApplicationPath(environment, application).Should().Be("/prefix/nested/default/App.1");
            pathBuilder.BuildReplicaPath(environment, application, replica).Should().Be("/prefix/nested/default/App.1/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Should_encode_environment_in_lower_case()
        {
            new PathBuilder(null).BuildReplicaPath("EEE/eee", "s", "r").Should().Be("/eee%2Feee/s/r");
        }

        [Test]
        public void Should_encode_application()
        {
            new PathBuilder(null).BuildReplicaPath("e", "AAA/aaa", "r").Should().Be("/e/AAA%2Faaa/r");
        }

        [Test]
        public void Should_encode_replica()
        {
            new PathBuilder(null).BuildReplicaPath("e", "s", "RRR/rrr:88").Should().Be("/e/s/RRR%2Frrr%3A88");
        }
    }
}