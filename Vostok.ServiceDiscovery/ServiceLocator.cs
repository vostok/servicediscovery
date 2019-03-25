using System;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery
{
    public class ServiceLocator : IServiceLocator
    {
        public IServiceTopology Locate(string environment, string service) =>
            throw new NotImplementedException();
    }
}