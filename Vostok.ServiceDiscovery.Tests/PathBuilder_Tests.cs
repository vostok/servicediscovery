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
            var service = "ServiceX.Api";
            var replica = "http://some-infra-host123:13528/";

            var pathBuilder = new PathBuilder(null);
            pathBuilder.BuildEnvironmentPath(environment).Should().Be("/default");
            pathBuilder.BuildServicePath(environment, service).Should().Be("/default/ServiceX.Api");
            pathBuilder.BuildReplicaPath(environment, service, replica).Should().Be("/default/ServiceX.Api/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Should_combine_with_prefix()
        {
            var prefix = "/prefix/nested";
            var environment = "default";
            var service = "ServiceX.Api";
            var replica = "http://some-infra-host123:13528/";

            var pathBuilder = new PathBuilder(prefix);
            pathBuilder.BuildEnvironmentPath(environment).Should().Be("/prefix/nested/default");
            pathBuilder.BuildServicePath(environment, service).Should().Be("/prefix/nested/default/ServiceX.Api");
            pathBuilder.BuildReplicaPath(environment, service, replica).Should().Be("/prefix/nested/default/ServiceX.Api/http%3A%2F%2Fsome-infra-host123%3A13528%2F");
        }

        [Test]
        public void Should_encode_environment_in_lower_case()
        {
            new PathBuilder(null).BuildReplicaPath("EEE/eee", "s", "r").Should().Be("/eee%2Feee/s/r");
        }

        [Test]
        public void Should_encode_service()
        {
            new PathBuilder(null).BuildReplicaPath("e", "SSS/sss", "r").Should().Be("/e/SSS%2Fsss/r");
        }

        [Test]
        public void Should_encode_replica()
        {
            new PathBuilder(null).BuildReplicaPath("e", "s", "RRR/rrr:88").Should().Be("/e/s/RRR%2Frrr%3A88");
        }
    }
}