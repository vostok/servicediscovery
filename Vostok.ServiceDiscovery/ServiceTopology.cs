using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc />
    [PublicAPI]
    public class ServiceTopology : IServiceTopology
    {
        /// <inheritdoc />
        public IReadOnlyList<Uri> Replicas { get; }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Properties { get; }
    }
}