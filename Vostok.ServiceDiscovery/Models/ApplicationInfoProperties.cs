using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Collections;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Models
{
    /// <inheritdoc cref="IApplicationInfoProperties"/>
    internal class ApplicationInfoProperties : ImmutableProperties, IApplicationInfoProperties
    {
        public ApplicationInfoProperties([CanBeNull] IReadOnlyDictionary<string, string> properties)
            : base(properties)
        {
        }

        private ApplicationInfoProperties(ImmutableArrayDictionary<string, string> properties)
            : base(properties)
        {
        }

        /// <inheritdoc cref="IApplicationInfoProperties"/>
        public new IApplicationInfoProperties Set(string key, string value) =>
            new ApplicationInfoProperties(base.Set(key, value));

        /// <inheritdoc cref="IApplicationInfoProperties"/>
        public new IApplicationInfoProperties Remove(string key) =>
            new ApplicationInfoProperties(base.Remove(key));
    }
}