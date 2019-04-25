using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// <para>Represents a configuration of <see cref="ReplicaInfoBuilder"/> instance which may be filled during <see cref="ServiceBeacon"/> construction.</para>
    /// <para>All parameters are optional.</para>
    /// <para>If you want to register service, url or port should be specified.</para>
    /// </summary>
    [PublicAPI]
    public interface IReplicaInfoBuilder
    {
        /// <summary>
        /// <para>Sets application environment.</para>
        /// <para>Default value: <c>default</c>.</para>
        /// </summary>
        IReplicaInfoBuilder SetEnvironment(string environment);

        /// <summary>
        /// <para>Sets application name.</para>
        /// <para>Default value: current application name.</para>
        /// </summary>
        IReplicaInfoBuilder SetApplication(string application);

        /// <summary>
        /// <para>Sets replica url.</para>
        /// <para>By default, it will be constructed from scheme, current host name, port and virtual path,
        ///     if port is specified.</para>
        /// </summary>
        IReplicaInfoBuilder SetUrl(Uri url);

        /// <summary>
        /// <para>Sets replica url port.</para>
        /// <para>If is not specified, it will be filled from url.</para>
        /// <para>Default value: <c>null</c>.</para>
        /// </summary>
        IReplicaInfoBuilder SetPort(int port);

        /// <summary>
        /// <para>Sets replica url scheme.</para>
        /// <para>If is not specified, it will be filled from url.</para>
        /// <para>Default value: <c>http</c>.</para>
        /// </summary>
        IReplicaInfoBuilder SetScheme(string scheme);

        /// <summary>
        /// <para>Sets replica url virtual path.</para>
        /// <para>If is not specified, it will be filled from url.</para>
        /// <para>Default value: <c>null</c></para>
        /// </summary>
        IReplicaInfoBuilder SetVirtualPath(string virtualPath);

        /// <summary>
        /// <para>Sets build commit hash.</para>
        /// <para>By default, it will be parsed from <c>AssemblyTitle</c> of entry assembly.</para>
        /// </summary>
        IReplicaInfoBuilder SetCommitHash(string commitHash);

        /// <summary>
        /// <para>Build date.</para>
        /// <para>By default, it will be parsed from <c>AssemblyTitle</c> of entry assembly.</para>
        /// </summary>
        IReplicaInfoBuilder SetReleaseDate(string releaseDate);

        /// <summary>
        /// <para>Assembly dependencies.</para>
        /// <para>By default it will be parsed from all <c>.dll</c>. and <c>.exe</c> files of entry assembly directory.</para>
        /// </summary>
        IReplicaInfoBuilder SetDependencies(IEnumerable<string> dependencies);

        /// <summary>
        /// <para>Sets custom <paramref name="key"/>-<paramref name="value"/> property.</para>
        /// <para>Default key names can be found in <see cref="ReplicaInfoKeys"/>.</para>
        /// </summary>
        IReplicaInfoBuilder SetProperty([NotNull] string key, [CanBeNull] string value);
    }
}