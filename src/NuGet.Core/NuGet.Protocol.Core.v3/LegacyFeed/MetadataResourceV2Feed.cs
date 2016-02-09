﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class MetadataV2FeedResource : MetadataResource
    {
        private readonly V2FeedParser _feedParser;

        public MetadataV2FeedResource(V2FeedParser feedParser)
        {
            if (_feedParser == null)
            {
                throw new ArgumentNullException(nameof(feedParser));
            }

            _feedParser = feedParser;
        }

        public override async Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, ILogger log, CancellationToken token)
        {
            var results = new List<KeyValuePair<string, NuGetVersion>>();

            var tasks = new Stack<KeyValuePair<string, Task<IEnumerable<NuGetVersion>>>>();

            // fetch all ids in parallel
            foreach (var id in packageIds)
            {
                var task = new KeyValuePair<string, Task<IEnumerable<NuGetVersion>>>(id, GetVersions(id, includePrerelease, includeUnlisted, log, token));
                tasks.Push(task);
            }

            foreach (var pair in tasks)
            {
                // wait for the query to finish
                var versions = await pair.Value;

                if (versions == null || !versions.Any())
                {
                    results.Add(new KeyValuePair<string, NuGetVersion>(pair.Key, null));
                }
                else
                {
                    // sort and take only the highest version
                    NuGetVersion latestVersion = versions.OrderByDescending(p => p, VersionComparer.VersionRelease).FirstOrDefault();

                    results.Add(new KeyValuePair<string, NuGetVersion>(pair.Key, latestVersion));
                }
            }

            return results;
        }

        public override async Task<IEnumerable<NuGetVersion>> GetVersions(string packageId, bool includePrerelease, bool includeUnlisted, ILogger log, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var packages = await _feedParser.FindPackagesByIdAsync(packageId, log, token);

            return packages.Where(p => includeUnlisted || p.IsListed)
                .Select(p => p.Version)
                .Where(v => includePrerelease || !v.IsPrerelease).ToArray();
        }

        public override async Task<bool> Exists(PackageIdentity identity, bool includeUnlisted, ILogger log, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var versions = await GetVersions(identity.Id, true, includeUnlisted, log, token);

            return versions.Any(e => VersionComparer.Default.Equals(identity.Version, e));
        }

        public override async Task<bool> Exists(string packageId, bool includePrerelease, bool includeUnlisted, ILogger log, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var versions = await GetVersions(packageId, includePrerelease, includeUnlisted, log, token);

            return versions.Any();
        }
    }
}
