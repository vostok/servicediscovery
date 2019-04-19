using System;
using System.Collections.Generic;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery
{
    internal class ServiceTopology : IServiceTopology
    {
        public ServiceTopology(IReadOnlyList<Uri> replicas, IReadOnlyDictionary<string, string> properties)
        {
            Replicas = replicas ?? new List<Uri>();
            Properties = properties;
        }

        /// <inheritdoc />
        public IReadOnlyList<Uri> Replicas { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Properties { get; }
    }
}