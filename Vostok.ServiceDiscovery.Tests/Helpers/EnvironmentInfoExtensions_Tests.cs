using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery.Tests.Helpers
{
    [TestFixture]
    internal class EnvironmentInfoExtensions_Tests
    {
        [Test]
        public void SkipIfEmpty_should_be_false()
        {
            ((EnvironmentInfo)null).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, null).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, new Dictionary<string, string> {{"key", "value"}}).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, null}}).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, ""}}).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, "false"}}).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, "False"}}).SkipIfEmpty().Should().BeFalse();
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, "xxx"}}).SkipIfEmpty().Should().BeFalse();
        }

        [Test]
        public void SkipIfEmpty_should_be_true()
        {
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, "true"}}).SkipIfEmpty().Should().BeTrue();
            new EnvironmentInfo(null, new Dictionary<string, string> {{EnvironmentInfoKeys.SkipIfEmpty, "True"}}).SkipIfEmpty().Should().BeTrue();
        }
    }
}