using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// A light weight XML parser for NuGet V2 Feeds
    /// </summary>
    public sealed class V2FeedParser
    {
        private const string W3Atom = "http://www.w3.org/2005/Atom";
        private const string MetadataNS = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private const string DataServicesNS = "http://schemas.microsoft.com/ado/2007/08/dataservices";
        private const string FindPackagesByIdFormat = "/FindPackagesById()?Id='{0}'";
        private const string SearchEndPointFormat = "/Search()?$filter={0}&searchTerm='{1}'&targetFramework='{2}'&includePrerelease={3}&$skip={4}&$top={5}";
        private const string IsLatestVersionFilterFlag = "IsLatestVersion";
        private const string IsAbsoluteLatestVersionFilterFlag = "IsAbsoluteLatestVersion";

        // XNames used in the feed
        private static readonly XName _xnameEntry = XName.Get("entry", W3Atom);
        private static readonly XName _xnameTitle = XName.Get("title", W3Atom);
        private static readonly XName _xnameContent = XName.Get("content", W3Atom);
        private static readonly XName _xnameLink = XName.Get("link", W3Atom);
        private static readonly XName _xnameProperties = XName.Get("properties", MetadataNS);
        private static readonly XName _xnameId = XName.Get("Id", DataServicesNS);
        private static readonly XName _xnameVersion = XName.Get("Version", DataServicesNS);
        private static readonly XName _xnameSummary = XName.Get("summary", W3Atom);
        private static readonly XName _xnameDescription = XName.Get("Description", DataServicesNS);
        private static readonly XName _xnameIconUrl = XName.Get("IconUrl", DataServicesNS);
        private static readonly XName _xnameLicenseUrl = XName.Get("LicenseUrl", DataServicesNS);
        private static readonly XName _xnameProjectUrl = XName.Get("ProjectUrl", DataServicesNS);
        private static readonly XName _xnameTags = XName.Get("Tags", DataServicesNS);
        private static readonly XName _xnameReportAbuseUrl = XName.Get("ReportAbuseUrl", DataServicesNS);
        private static readonly XName _xnameDependencies = XName.Get("Dependencies", DataServicesNS);
        private static readonly XName _xnameRequireLicenseAcceptance = XName.Get("RequireLicenseAcceptance", DataServicesNS);
        private static readonly XName _xnameDownloadCount = XName.Get("DownloadCount", DataServicesNS);
        private static readonly XName _xnamePublished = XName.Get("Published", DataServicesNS);
        private static readonly XName _xnameName = XName.Get("name", W3Atom);
        private static readonly XName _xnameAuthor = XName.Get("author", W3Atom);
        private static readonly XName _xnamePackageHash = XName.Get("PackageHash", DataServicesNS);
        private static readonly XName _xnamePackageHashAlgorithm = XName.Get("PackageHashAlgorithm", DataServicesNS);
        private static readonly XName _xnameMinClientVersion = XName.Get("MinClientVersion", DataServicesNS);

        private readonly HttpSource _httpSource;
        private readonly PackageSource _source;
        private readonly string _findPackagesByIdFormat;
        private readonly string _searchEndPointFormat;

        public V2FeedParser(HttpSource httpSource, string sourceUrl)
            : this(httpSource, new PackageSource(sourceUrl))
        {

        }

        /// <summary>
        /// Creates a V2 parser
        /// </summary>
        /// <param name="httpHandler">Message handler containing auth/proxy support</param>
        /// <param name="source">endpoint source</param>
        public V2FeedParser(HttpSource httpSource, PackageSource source)
        {
            if (httpSource == null)
            {
                throw new ArgumentNullException(nameof(httpSource));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _httpSource = httpSource;
            _source = source;
            _findPackagesByIdFormat = source.Source.TrimEnd('/') + FindPackagesByIdFormat;
            _searchEndPointFormat = source.Source.TrimEnd('/') + SearchEndPointFormat;
        }

        /// <summary>
        /// Get an exact package
        /// </summary>
        public async Task<V2FeedPackageInfo> GetPackage(
            PackageIdentity package,
            ILogger log,
            CancellationToken token)
        {
            // TODO add support for the exact call
            var packages = await FindPackagesByIdAsync(package.Id, log, token);

            return packages.FirstOrDefault(entry => entry.Version == package.Version);
        }

        /// <summary>
        /// Retrieves all packages with the given Id from a V2 feed.
        /// </summary>
        public async Task<IEnumerable<V2FeedPackageInfo>> FindPackagesByIdAsync(string id, ILogger log, CancellationToken token)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id");
            }

            var uri = String.Format(CultureInfo.InvariantCulture, _findPackagesByIdFormat, id);
            return await QueryV2Feed(uri, id, log, token);
        }



        public async Task<IEnumerable<V2FeedPackageInfo>> Search(string searchTerm, SearchFilter filters, int skip, int take, ILogger log, CancellationToken cancellationToken)
        {
            var targetFramework = String.Join(@"/", filters.SupportedFrameworks);
            var uri = String.Format(CultureInfo.InvariantCulture, _searchEndPointFormat,
                                    filters.IncludePrerelease ? IsAbsoluteLatestVersionFilterFlag : IsLatestVersionFilterFlag,
                                    searchTerm,
                                    targetFramework,
                                    filters.IncludePrerelease.ToString().ToLowerInvariant(),
                                    skip,
                                    take);
            return await QueryV2Feed(uri, null, log, cancellationToken);
        }

        /// <summary>
        /// Finds all entries on the page and parses them
        /// </summary>
        private IEnumerable<V2FeedPackageInfo> ParsePage(XDocument doc, string id)
        {
            var result = doc.Root
                .Elements(_xnameEntry)
                .Select(x => ParsePackage(id, x));

            return result;
        }

        /// <summary>
        /// Parse an entry into a V2FeedPackageInfo
        /// </summary>
        private V2FeedPackageInfo ParsePackage(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);
            var idElement = properties.Element(_xnameId);
            var titleElement = element.Element(_xnameTitle);

            // If 'Id' element exist, use its value as accurate package Id
            // Otherwise, use the value of 'title' if it exist
            // Use the given Id as final fallback if all elements above don't exist
            string identityId = idElement?.Value ?? titleElement?.Value ?? id;
            string versionString = properties.Element(_xnameVersion).Value;
            NuGetVersion version = NuGetVersion.Parse(versionString);
            string downloadUrl = element.Element(_xnameContent).Attribute("src").Value;

            string title = titleElement?.Value;
            string summary = GetValue(element, _xnameSummary);
            string description = GetValue(properties, _xnameDescription);
            string iconUrl = GetValue(properties, _xnameIconUrl);
            string licenseUrl = GetValue(properties, _xnameLicenseUrl);
            string projectUrl = GetValue(properties, _xnameProjectUrl);
            string reportAbuseUrl = GetValue(properties, _xnameReportAbuseUrl);
            string tags = GetValue(properties, _xnameTags);
            string dependencies = GetValue(properties, _xnameDependencies);

            string downloadCount = GetValue(properties, _xnameDownloadCount);
            bool requireLicenseAcceptance = GetValue(properties, _xnameRequireLicenseAcceptance) == "true";

            string packageHash = GetValue(properties, _xnamePackageHash);
            string packageHashAlgorithm = GetValue(properties, _xnamePackageHashAlgorithm);
            string minClientVersionString = GetValue(properties, _xnameMinClientVersion);

            NuGetVersion minClientVersion = null;

            if (minClientVersionString != null)
            {
                NuGetVersion.TryParse(minClientVersionString, out minClientVersion);
            }

            DateTimeOffset? published = null;

            DateTimeOffset pubVal = DateTimeOffset.MinValue;
            string pubString = GetValue(properties, _xnamePublished);
            if (DateTimeOffset.TryParse(pubString, out pubVal))
            {
                published = pubVal;
            }

            // TODO: is this ever populated in v2?
            IEnumerable<string> owners = null;

            IEnumerable<string> authors = null;

            var authorNode = element.Element(_xnameAuthor);
            if (authorNode != null)
            {
                authors = authorNode.Elements(_xnameName).Select(e => e.Value);
            }

            return new V2FeedPackageInfo(new PackageIdentity(identityId, version),
                title, summary, description, authors, owners, iconUrl, licenseUrl,
                projectUrl, reportAbuseUrl, tags, published, dependencies,
                requireLicenseAcceptance, downloadUrl, downloadCount,
                packageHash,
                packageHashAlgorithm,
                minClientVersion
                );
        }

        /// <summary>
        /// Retrieve an XML value safely
        /// </summary>
        private static string GetValue(XElement parent, XName childName)
        {
            string value = null;

            if (parent != null)
            {
                XElement child = parent.Element(childName);

                if (child != null)
                {
                    value = child.Value;
                }
            }

            return value;
        }

        private async Task<List<V2FeedPackageInfo>> QueryV2Feed(string uri, string id, ILogger log, CancellationToken token)
        {
            var results = new List<V2FeedPackageInfo>();
            var page = 1;

            // first request
            Task<HttpResponseMessage> urlRequest = _httpSource.GetAsync(new Uri(uri), log, token);

            // TODO: re-implement caching at a higher level for both v2 and v3
            while (!token.IsCancellationRequested && urlRequest != null)
            {
                // TODO: Pages for a package Id are cached separately.
                // So we will get inaccurate data when a page shrinks.
                // However, (1) In most cases the pages grow rather than shrink;
                // (2) cache for pages is valid for only 30 min.
                // So we decide to leave current logic and observe.
                using (var data = await urlRequest)
                {
                    try
                    {
                        var doc = XDocument.Load(await data.Content.ReadAsStreamAsync());

                        // Example of what this looks like in the odata feed:
                        // <link rel="next" href="{nextLink}" />
                        var nextUri = (from e in doc.Root.Elements(_xnameLink)
                                       let attr = e.Attribute("rel")
                                       where attr != null && string.Equals(attr.Value, "next", StringComparison.OrdinalIgnoreCase)
                                       select e.Attribute("href") into nextLink
                                       where nextLink != null
                                       select nextLink.Value).FirstOrDefault();

                        // Request the next url in parallel to parsing the current page
                        urlRequest = null;
                        if (!string.IsNullOrEmpty(nextUri) && uri != nextUri)
                        {
                            // a bug on the server side causes the same next link to be returned 
                            // for every page. To avoid falling into an infinite loop we must
                            // keep track here.
                            uri = nextUri;

                            urlRequest = _httpSource.GetAsync(new Uri(nextUri), log, token);
                        }

                        // find results on the page
                        var result = ParsePage(doc, id);
                        results.AddRange(result);

                        page++;
                    }
                    catch (XmlException)
                    {
                        //_reports.Information.WriteLine("The XML file {0} is corrupt",
                        //    data.CacheFileName.Yellow().Bold());
                        throw;
                    }
                }
            }

            return results;
        }
    }
}
