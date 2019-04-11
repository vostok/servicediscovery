using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    public class ReplicaInfo_Tests
    {
        [Test]
        public void Should_add_properties()
        {
            var info = new ReplicaInfo("environment", "service", "replica")
                .AddProperty("Process name", "Vostok.App.1")
                .AddProperty("Process ID", "4242");

            info.Properties
                .Should()
                .BeEquivalentTo(
                    new Dictionary<string, string>
                    {
                        {"Process name", "Vostok.App.1"},
                        {"Process ID", "4242"}
                    });
        }
    }
}