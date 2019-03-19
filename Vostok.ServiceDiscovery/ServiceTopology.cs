using System;
using System.Collections.Generic;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery
{
    public class ServiceTopology : IServiceTopology
    {
        public IReadOnlyList<Uri> Replicas { get; }
        public IReadOnlyDictionary<string, string> Data { get; }
    }
}