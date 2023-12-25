using System;
using System.Collections.Generic;
using System.Linq;
using Vostok.Commons.Environment;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;
using EnvironmentInfo = Vostok.Commons.Environment.EnvironmentInfo;

namespace Vostok.ServiceDiscovery
{
    internal class ReplicaInfoBuilder : IReplicaInfoBuilder
    {
        private const string DependenciesDelimiter = ";";
        private readonly int? processId;
        private readonly string baseDirectory;

        private readonly Dictionary<string, string> properties = new Dictionary<string, string>();

        private string host;
        private string processName;

        private string environment;
        private string application;
        private string replica;

        private Uri url;
        private string scheme;
        private int? port;
        private string urlPath;

        private string commitHash;
        private string releaseDate;

        private List<string> dependencies;
        private TagCollection tags;

        private bool useFQDN;
        private Func<bool, string> hostNameProvider;

        private ReplicaInfoBuilder(bool useFQDN)
        {
            environment = "default";
            application = EnvironmentInfo.Application;
            this.useFQDN = useFQDN;
            host = useFQDN ? EnvironmentInfo.FQDN : EnvironmentInfo.Host;
            processName = EnvironmentInfo.ProcessName;
            processId = EnvironmentInfo.ProcessId;
            baseDirectory = EnvironmentInfo.BaseDirectory;
            commitHash = AssemblyCommitHashExtractor.ExtractFromEntryAssembly();
            releaseDate = AssemblyBuildTimeExtractor.ExtractFromEntryAssembly()?.ToString("O");
            dependencies = AssemblyDependenciesExtractor.ExtractFromEntryAssembly();
            tags = new TagCollection();
        }

        public static ServiceBeaconInfo Build(ReplicaInfoSetup setup, bool useFQDN)
        {
            var builder = new ReplicaInfoBuilder(useFQDN);
            setup?.Invoke(builder);
            return builder.Build();
        }

        public ServiceBeaconInfo Build()
        {
            if (hostNameProvider != null)
            {
                host = hostNameProvider(useFQDN);
            }
            
            url ??= BuildUrl();
            replica ??= url.ToString();

            if (url != null)
            {
                scheme = url.Scheme;
                port = url.Port;
                urlPath = url.AbsolutePath;
            }

            var replicaInfo = new ReplicaInfo(environment, application, replica);

            FillProperties(replicaInfo);

            return new ServiceBeaconInfo(replicaInfo, tags);
        }

        private Uri BuildUrl()
        {
            if (port == null)
                return null;

            return new UriBuilder
            {
                Scheme = scheme ?? "http",
                Host = host,
                Port = port.Value,
                Path = urlPath ?? ""
            }.Uri;
        }

        private void FillProperties(ReplicaInfo replicaInfo)
        {
            replicaInfo.SetProperty(ReplicaInfoKeys.Environment, environment);
            replicaInfo.SetProperty(ReplicaInfoKeys.Application, application);
            replicaInfo.SetProperty(ReplicaInfoKeys.Replica, replica);
            replicaInfo.SetProperty(ReplicaInfoKeys.Url, url?.ToString());
            replicaInfo.SetProperty(ReplicaInfoKeys.Host, host);
            replicaInfo.SetProperty(ReplicaInfoKeys.ProcessName, processName);
            replicaInfo.SetProperty(ReplicaInfoKeys.ProcessId, processId?.ToString());
            replicaInfo.SetProperty(ReplicaInfoKeys.BaseDirectory, baseDirectory);
            replicaInfo.SetProperty(ReplicaInfoKeys.CommitHash, commitHash);
            replicaInfo.SetProperty(ReplicaInfoKeys.ReleaseDate, releaseDate);
            replicaInfo.SetProperty(ReplicaInfoKeys.Dependencies, FormatDependencies());
            replicaInfo.SetProperty(ReplicaInfoKeys.Port, port?.ToString());

            foreach (var property in properties)
            {
                replicaInfo.SetProperty(property.Key, property.Value);
            }
        }

        private string FormatDependencies()
        {
            return dependencies == null
                ? null
                : string.Join(
                    DependenciesDelimiter,
                    dependencies.Select(d => d?.Replace(DependenciesDelimiter, "_")));
        }

        #region Builder methods

        // ReSharper disable ParameterHidesMember

        public IReplicaInfoBuilder SetReplicaId(string replica)
        {
            this.replica = replica;
            return this;
        }

        public IReplicaInfoBuilder SetEnvironment(string environment)
        {
            this.environment = environment;
            return this;
        }

        public IReplicaInfoBuilder SetApplication(string application)
        {
            this.application = application;
            return this;
        }

        public IReplicaInfoBuilder SetUrl(Uri url)
        {
            this.url = url;
            return this;
        }

        public IReplicaInfoBuilder SetPort(int? port)
        {
            this.port = port;
            return this;
        }

        public IReplicaInfoBuilder SetScheme(string scheme)
        {
            this.scheme = scheme;
            return this;
        }

        public IReplicaInfoBuilder SetUrlPath(string path)
        {
            urlPath = path;
            return this;
        }

        public IReplicaInfoBuilder SetupHostnameProvider(Func<bool, string> hostNameProvider)
        {
            this.hostNameProvider = hostNameProvider;
            return this;
        }

        public IReplicaInfoBuilder SetCommitHash(string commitHash)
        {
            this.commitHash = commitHash;
            return this;
        }

        public IReplicaInfoBuilder SetProcessName(string processName)
        {
            this.processName = processName;
            return this;
        }

        public IReplicaInfoBuilder SetReleaseDate(string releaseDate)
        {
            this.releaseDate = releaseDate;
            return this;
        }

        public IReplicaInfoBuilder SetDependencies(IEnumerable<string> dependencies)
        {
            this.dependencies = dependencies?.ToList();
            return this;
        }

        public IReplicaInfoBuilder SetProperty(string key, string value)
        {
            properties[key ?? throw new ArgumentNullException(nameof(key))] = value;
            return this;
        }

        public IReplicaInfoBuilder SetTags(TagCollection tags)
        {
            this.tags = tags ?? new TagCollection();
            return this;
        }

        // ReSharper restore ParameterHidesMember

        #endregion
    }
}