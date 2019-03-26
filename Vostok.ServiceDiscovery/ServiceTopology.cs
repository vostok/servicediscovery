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
        [ItemNotNull]
        public IReadOnlyList<Uri> Replicas { get; }
        
        /// <inheritdoc />
        [NotNull]
        public IReadOnlyDictionary<string, string> Data { get; }
    }
}