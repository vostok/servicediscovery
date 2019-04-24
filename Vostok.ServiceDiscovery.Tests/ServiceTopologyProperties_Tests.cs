using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class ServiceTopologyProperties_Tests
    {
        [Test]
        public void Should_be_immutable()
        {
            var dict = new Dictionary<string, string>
            {
                {"key", "value"}
            };

            var properties = new ServiceTopologyProperties(dict);

            var propertiesA = properties.Set("A", "aaa");

            var propertiesB = propertiesA.Set("B", "bbb");

            var propertiesC = propertiesB.Remove("key");

            dict.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                {"key", "value"}
            });

            properties.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                {"key", "value"}
            });

            propertiesA.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                {"key", "value"},
                {"A", "aaa" }
            });

            propertiesB.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                {"key", "value"},
                {"A", "aaa" },
                {"B", "bbb" }
            });

            propertiesC.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                {"A", "aaa" },
                {"B", "bbb" }
            });
        }
    }
}