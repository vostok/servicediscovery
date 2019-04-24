using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery.Tests.Helpers
{
    [TestFixture]
    internal class VersionedContainer_Tests
    {
        [Test]
        public void Should_has_null_value_initially()
        {
            new VersionedContainer<string>().Value.Should().BeNull();
        }

        [Test]
        public void Update_should_work_sequentially()
        {
            var container = new VersionedContainer<string>();
            for (var i = 0; i < 10; i++)
            {
                // ReSharper disable once AccessToModifiedClosure
                container.Update(i, () => i.ToString());
                container.Value.Should().Be(i.ToString());
            }
        }

        [Test]
        public void Update_should_ignore_smaller_versions()
        {
            var container = new VersionedContainer<string>();

            container.Update(2, () => "x");
            container.Update(1, () => throw new AssertionException("Should not be called."));

            container.Value.Should().Be("x");
        }

        [Test]
        public void Update_should_has_max_version_after_concurrent_updates()
        {
            var container = new VersionedContainer<string>();

            var random = new Random(314);
            var updates = Enumerable.Range(0, 10000).Select(i => random.Next()).ToList();

            Parallel.ForEach(
                updates,
                new ParallelOptions { MaxDegreeOfParallelism = 10 },
                u =>
                {
                    container.Update(u,
                        () =>
                        {
                            Thread.Sleep(u % 100);
                            return u.ToString();
                        });
                });

            container.Value.Should().Be(updates.Max().ToString());
        }

        [Test]
        public void Clear_should_reset_value_and_version()
        {
            var container = new VersionedContainer<string>();

            container.Update(10, () => "x");
            container.Value.Should().Be("x");

            container.Clear();
            container.Value.Should().BeNull();

            container.Update(1, () => "y");
            container.Value.Should().Be("y");
        }
    }
}