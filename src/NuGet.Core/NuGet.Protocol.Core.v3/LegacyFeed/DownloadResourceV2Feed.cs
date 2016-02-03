using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class DownloadResourceV2Feed : DownloadResource
    {
        private readonly V2FeedParser _feedParser;

        public DownloadResourceV2Feed(V2FeedParser feedParser)
        {
            if (_feedParser == null)
            {
                throw new ArgumentNullException(nameof(feedParser));
            }

            _feedParser = feedParser;
        }

        public override async Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            token.ThrowIfCancellationRequested();

            var sourcePackage = identity as SourcePackageDependencyInfo;
            bool isFromUri = sourcePackage?.PackageHash != null
                            && sourcePackage?.DownloadUri != null;

            if (isFromUri)
            {
                // If this is a SourcePackageDependencyInfo object with everything populated
                // and it is from an online source, use the machine cache and download it using the
                // given url.
                return await _feedParser.DownloadFromUrl(sourcePackage, sourcePackage.DownloadUri, settings, log, token);
            }
            else
            {
                // Look up the package from the id and version and download it.
                return await _feedParser.DownloadFromIdentity(identity, settings, log, token);
            }
        }
    }
}
