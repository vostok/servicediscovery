using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Collections;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Models
{
    /// <inheritdoc cref="IServiceTopologyProperties"/>
    internal class ServiceTopologyProperties : ImmutableProperties, IServiceTopologyProperties
    {
        public ServiceTopologyProperties([CanBeNull] IReadOnlyDictionary<string, string> properties)
            : base(properties)
        {
        }

        private ServiceTopologyProperties(ImmutableArrayDictionary<string, string> properties)
            : base(properties)
        {
        }

        /// <inheritdoc cref="IServiceTopologyProperties"/>
        public new IServiceTopologyProperties Set(string key, string value) =>
            new ServiceTopologyProperties(base.Set(key, value));

        /// <inheritdoc cref="IServiceTopologyProperties"/>
        public new IServiceTopologyProperties Remove(string key) =>
            new ServiceTopologyProperties(base.Remove(key));
    }
}