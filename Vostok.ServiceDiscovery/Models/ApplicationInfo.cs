using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Models
{
    internal class ApplicationInfo : IApplicationInfo
    {
        public ApplicationInfo([NotNull] string environment, [NotNull] string application, [CanBeNull] IReadOnlyDictionary<string, string> properties)
        {
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentOutOfRangeException(nameof(environment), environment);
            if (string.IsNullOrWhiteSpace(application))
                throw new ArgumentOutOfRangeException(nameof(application), application);
            Environment = environment;
            Application = application;
            Properties = new ApplicationInfoProperties(properties);
        }

        public string Environment { get; }

        public string Application { get; }

        public IApplicationInfoProperties Properties { get; }
    }
}