using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class SearchLatestV2FeedResource : SearchLatestResource
    {
        private readonly V2FeedParser _feedParser;

        public SearchLatestV2FeedResource(V2FeedParser feedParser)
        {
            if (_feedParser == null)
            {
                throw new ArgumentNullException(nameof(feedParser));
            }

            _feedParser = feedParser;
        }

        public override async Task<IEnumerable<ServerPackageMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, Logging.ILogger log, CancellationToken cancellationToken)
        {
            var ListofPackageInfo = await _feedParser.Search(searchTerm, filters, skip, take, log, cancellationToken);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var results = new List<ServerPackageMetadata>();

            foreach (var package in ListofPackageInfo)
            {
                if (seen.Add(package.Id))
                {
                    var highest = ListofPackageInfo.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, package.Id))
                        .OrderByDescending(p => p.Version).First();
                    
                    // This concept is not in v2 yet
                    IEnumerable<string> types = new string[] { "Package" };
                    var tags = highest.Tags == null ? new string[0] : package.Tags.Split(' ');

                    var metadata = new ServerPackageMetadata(highest, highest.Title, highest.Summary, highest.Description,
                                                             highest.Authors, new Uri(highest.IconUrl),new Uri(highest.LicenseUrl), new Uri(highest.ProjectUrl),
                                                             tags, highest.Published,highest.DependencySets, highest.RequireLicenseAcceptance,
                                                             highest.MinClientVersion, highest.DownloadCountAsInt, -1, highest.Owners, types);
                    results.Add(metadata);
                }
            }

            return results;
        }
    }
}
