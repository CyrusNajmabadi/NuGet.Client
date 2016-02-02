using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class SearchLatestResourceV2FeedProvider: ResourceProvider
    {
        public SearchLatestResourceV2FeedProvider()
            : base(typeof(SearchLatestResource), "SearchLatestResourceV2FeedProvider", NuGetResourceProviderPositions.Last)
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            SearchLatestResource resource = null;

            if ((FeedTypeUtility.GetFeedType(source.PackageSource) & FeedType.HttpV2) != FeedType.None)
            {
                var httpSource = HttpSource.Create(source);
                var parser = new V2FeedParser(httpSource, source.PackageSource);

                resource = new SearchLatestV2FeedResource(parser);
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(resource != null, resource));
        }
    }
}
