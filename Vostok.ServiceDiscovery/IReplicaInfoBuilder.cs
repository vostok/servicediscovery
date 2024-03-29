﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// <para><see cref="IReplicaInfoBuilder"/> is used to specify application instance details on <see cref="ServiceBeacon"/> construction.</para>
    /// <para>All parameters are optional.</para>
    /// <para>Use <see cref="SetUrl"/> or <see cref="SetPort"/> to advertise the application as an HTTP service.</para>
    /// </summary>
    [PublicAPI]
    public interface IReplicaInfoBuilder
    {
        /// <summary>
        /// <para>Overrides the whole replica identifier used for registration.</para>
        /// <para>Default value: assembled from url or host + PID.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetReplicaId([NotNull] string replica);

        /// <summary>
        /// <para>Sets application environment.</para>
        /// <para>Default value: <c>default</c>.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetEnvironment([NotNull] string environment);

        /// <summary>
        /// <para>Sets application name.</para>
        /// <para>Default value: current entry assembly name.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetApplication([NotNull] string application);

        /// <summary>
        /// <para>Sets replica HTTP url.</para>
        /// <para>By default, it will be constructed from scheme, current host name, port and virtual path, if port is specified.</para>
        /// <para>Should not be called in conjunction with <see cref="SetPort"/>, <see cref="SetScheme"/> and <see cref="SetUrlPath"/>.</para>
        /// <para>Setting this property will instruct <see cref="ServiceBeacon"/> to advertise application as an HTTP service.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetUrl([CanBeNull] Uri url);

        /// <summary>
        /// <para>Specifies the port the application uses to listen for HTTP requests.</para>
        /// <para>Default value: none.</para>
        /// <para>Should not be called in conjunction with <see cref="SetUrl"/>.</para>
        /// <para>Setting this property will instruct <see cref="ServiceBeacon"/> to advertise application as an HTTP service.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetPort(int? port);

        /// <summary>
        /// <para>Sets replica url scheme.</para>
        /// <para>Default value: <c>http</c>.</para>
        /// <para>Should not be called in conjunction with <see cref="SetUrl"/>.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetScheme([CanBeNull] string scheme);

        /// <summary>
        /// <para>Sets replica url path.</para>
        /// <para>Default value: <c>null</c></para>
        /// <para>Should not be called in conjunction with <see cref="SetUrl"/>.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetUrlPath([CanBeNull] string path);

        /// <summary>
        /// <para>Sets configuration for HostnameProvider.</para>
        /// <para>Default value: <c>null</c></para>
        /// <para>Should not be called in conjunction with <see cref="SetUrl"/>.</para>
        /// </summary>
        IReplicaInfoBuilder SetHostnameProvider([CanBeNull] Func<string> vpnHostnameProvider);

        /// <summary>
        /// <para>Sets build commit hash.</para>
        /// <para>By default, it will be parsed from <c>AssemblyTitle</c> attribute of entry assembly.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetCommitHash([CanBeNull] string commitHash);

        /// <summary>
        /// <para>Sets application build date.</para>
        /// <para>By default, it will be parsed from <c>AssemblyTitle</c> attribute of entry assembly.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetReleaseDate([CanBeNull] string releaseDate);

        /// <summary>
        /// <para>Sets process name.</para>
        /// <para>By default, it will be taken from <c>Process.GetCurrentProcess().ProcessName</c>.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetProcessName([CanBeNull] string processName);

        /// <summary>
        /// <para>Sets application dependencies (libraries along with their versions).</para>
        /// <para>By default it will be parsed from all <c>.dll</c>. and <c>.exe</c> files in the entry assembly directory.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetDependencies([CanBeNull] IEnumerable<string> dependencies);

        /// <summary>
        /// <para>Sets a custom <paramref name="key"/>-<paramref name="value"/> property.</para>
        /// <para>Built-in key names can be found in <see cref="ReplicaInfoKeys"/>.</para>
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetProperty([NotNull] string key, [CanBeNull] string value);

        /// <summary>
        /// Sets a custom beacon <paramref name="tags"/>, which can be overwritten only by restarting the beacon.
        /// </summary>
        [NotNull]
        IReplicaInfoBuilder SetTags([CanBeNull] TagCollection tags);
    }
}