using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery.Tests.Models
{
    [TestFixture]
    public class ReplicaInfo_Tests
    {
        [Test]
        public void Should_add_properties()
        {
            var info = new ReplicaInfo("environment", "application", "replica");
            info.SetProperty("Process name", "Vostok.App.1");
            info.SetProperty("Process ID", "4242");

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