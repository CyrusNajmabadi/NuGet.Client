using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class DependencyInfoResourceV2Feed : DependencyInfoResource
    {
        private readonly V2FeedParser _feedParser;
        private readonly FrameworkReducer _frameworkReducer = new FrameworkReducer();
        private readonly SourceRepository _source;

        public DependencyInfoResourceV2Feed(V2FeedParser feedParser, SourceRepository source)
        {
            if (feedParser == null)
            {
                throw new ArgumentNullException(nameof(feedParser));
            }

            _feedParser = feedParser;
            _source = source;
        }

        public override async Task<SourcePackageDependencyInfo> ResolvePackage(
            PackageIdentity package,
            NuGetFramework projectFramework,
            ILogger log,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var packages = await _feedParser.FindPackagesByIdAsync(package.Id, log, token);

            return CreateDependencyInfo(packages.Where(p => p.Version.Equals(package.Version)).FirstOrDefault(), projectFramework);
        }

        public override async Task<IEnumerable<SourcePackageDependencyInfo>> ResolvePackages(
            string packageId,
            NuGetFramework projectFramework,
            ILogger log,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var packages = await _feedParser.FindPackagesByIdAsync(packageId, log, token);

            var results = new List<SourcePackageDependencyInfo>();

            foreach (var package in packages)
            {
                results.Add(CreateDependencyInfo(package, projectFramework));
            }

            return results;
        }

        /// <summary>
        /// Convert a V2 feed package into a V3 PackageDependencyInfo
        /// </summary>
        private SourcePackageDependencyInfo CreateDependencyInfo(
            V2FeedPackageInfo packageVersion,
            NuGetFramework projectFramework)
        {
            var deps = Enumerable.Empty<PackageDependency>();

            var identity = new PackageIdentity(packageVersion.Id, NuGetVersion.Parse(packageVersion.Version.ToString()));
            if (packageVersion.DependencySets != null
                && packageVersion.DependencySets.Any())
            {
                // Take only the dependency group valid for the project TFM
                var nearestFramework = _frameworkReducer.GetNearest(
                    projectFramework,
                    packageVersion.DependencySets.Select(group => group.TargetFramework));

                if (nearestFramework != null)
                {
                    var matches = packageVersion.DependencySets.Where(e => (e.TargetFramework.Equals(nearestFramework)));
                    deps = matches.First().Packages;
                }
            }

            var result = new SourcePackageDependencyInfo(
                identity,
                deps,
                packageVersion.IsListed,
                _source,
                new Uri(packageVersion.DownloadUrl),
                packageVersion.PackageHash);

            return result;
        }
    }
}
