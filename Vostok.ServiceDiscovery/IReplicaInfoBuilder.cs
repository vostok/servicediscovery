using System;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    [PublicAPI]
    public interface IReplicaInfoBuilder
    {
        string Environment { set; }

        string Service { set; }

        Uri Url { set; }

        int? Port { set; }
        string Scheme { set; }
        string VirtualPath { set; }

        string CommitHash { set; }
        string ReleaseDate { set; }

        IReplicaInfoBuilder AddProperty([NotNull] string key, [CanBeNull] string value);
    }
}