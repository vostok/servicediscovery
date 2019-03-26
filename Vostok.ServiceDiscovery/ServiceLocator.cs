using System;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc />
    [PublicAPI]
    public class ServiceLocator : IServiceLocator
    {
        /// <inheritdoc />
        [NotNull]
        public IServiceTopology Locate(string environment, string service) =>
            throw new NotImplementedException();
    }
}