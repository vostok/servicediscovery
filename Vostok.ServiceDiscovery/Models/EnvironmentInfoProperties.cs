using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Collections;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Models
{
    /// <inheritdoc cref="IEnvironmentInfoProperties"/>
    internal class EnvironmentInfoProperties : ImmutableProperties, IEnvironmentInfoProperties
    {
        public EnvironmentInfoProperties([CanBeNull] IReadOnlyDictionary<string, string> properties)
            : base(properties)
        {
        }

        private EnvironmentInfoProperties(ImmutableArrayDictionary<string, string> properties)
            : base(properties)
        {
        }

        /// <inheritdoc cref="IEnvironmentInfoProperties"/>
        public new IEnvironmentInfoProperties Set(string key, string value) =>
            new EnvironmentInfoProperties(base.Set(key, value));

        /// <inheritdoc cref="IEnvironmentInfoProperties"/>
        public new IEnvironmentInfoProperties Remove(string key) =>
            new EnvironmentInfoProperties(base.Remove(key));
    }
}