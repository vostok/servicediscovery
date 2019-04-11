using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// <para>Represents a configuration of <see cref="ReplicaInfoBuilder"/> instance which may be filled during <see cref="ServiceBeacon"/> construction.</para>
    /// <para>All parameters are optional.</para>
    /// <para>If you want to register service, <see cref="Url"/> or <see cref="Port"/> should be specified.</para>
    /// </summary>
    [PublicAPI]
    public interface IReplicaInfoBuilder
    {
        /// <summary>
        /// <para>Service environment.</para>
        /// <para>Default value: <c>default</c>.</para>
        /// </summary>
        string Environment { set; }

        /// <summary>
        /// <para>Service name.</para>
        /// <para>Default value: application name.</para>
        /// </summary>
        string Service { set; }

        /// <summary>
        /// <para>Replica url.</para>
        /// <para>If is not specified and <see cref="Port"/> is not <c>null</c>,
        ///     will be constructed from <see cref="Scheme"/>, current hostname, <see cref="Port"/> and <see cref="VirtualPath"/>.</para>
        /// </summary>
        Uri Url { set; }

        /// <summary>
        /// <para>Replica url port.</para>
        /// <para>If is not specified, will be filled from <see cref="Url"/>.</para>
        /// <para>Default value: <c>null</c>.</para>
        /// </summary>
        int? Port { set; }

        /// <summary>
        /// <para>Replica url scheme.</para>
        /// <para>If is not specified, will be filled from <see cref="Url"/>.</para>
        /// <para>Default value: <c>http</c>.</para>
        /// </summary>
        string Scheme { set; }

        /// <summary>
        /// <para>Replica url virtual path.</para>
        /// <para>If is not specified, will be filled from <see cref="Url"/>.</para>
        /// <para>Default value: <c>null</c></para>
        /// </summary>
        string VirtualPath { set; }

        /// <summary>
        /// <para>Build commit hash.</para>
        /// <para>By default, will be parsed from <c>AssemblyTitle</c> of entry assembly.</para>
        /// </summary>
        string CommitHash { set; }

        /// <summary>
        /// <para>Build date.</para>
        /// <para>By default, will be parsed from <c>AssemblyTitle</c> of entry assembly.</para>
        /// </summary>
        string ReleaseDate { set; }

        /// <summary>
        /// <para>Assembly dependencies.</para>
        /// <para>By default will be parsed from all <c>.dll</c>. and <c>.exe</c> files of entry assembly directory.</para>
        /// </summary>
        List<string> Dependencies { set; }

        /// <summary>
        /// <para>Adds custom <paramref name="key"/>-<paramref name="value"/> property.</para>
        /// <para>Default key names can be found in <see cref="ReplicaInfoKeys"/>.</para>
        /// </summary>
        IReplicaInfoBuilder AddProperty([NotNull] string key, [CanBeNull] string value);
    }
}