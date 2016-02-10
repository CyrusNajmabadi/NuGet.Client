﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class V2FeedParserTests
    {
        [Fact]
        public async Task V2FeedParser_Basic()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");

            var packages = await parser.FindPackagesByIdAsync(
                "WindowsAzure.Storage",
                NullLogger.Instance,
                CancellationToken.None);

            Assert.Equal(34, packages.Count());
        }

        [Fact]
        public async Task V2FeedParser_FollowNextLinks()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");

            var packages = await parser.FindPackagesByIdAsync("ravendb.client", NullLogger.Instance, CancellationToken.None);

            Assert.Equal(300, packages.Count());
        }

        [Fact]
        public async Task V2FeedParser_PackageInfo()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");

            var packages = await parser.FindPackagesByIdAsync("WindowsAzure.Storage", NullLogger.Instance, CancellationToken.None);

            var latest = packages.OrderByDescending(e => e.Version, VersionComparer.VersionRelease).FirstOrDefault();

            Assert.Equal("WindowsAzure.Storage", latest.Id);
            Assert.Equal("4.3.2-preview", latest.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", latest.Title);
            Assert.Equal("Microsoft", String.Join(",", latest.Authors));
            Assert.Equal("", String.Join(",", latest.Owners));
            Assert.True(latest.Description.StartsWith("This client library enables"));
            Assert.Equal(2102565, latest.DownloadCountAsInt);
            Assert.Equal("http://api.nuget.org/api/v2/package/WindowsAzure.Storage/4.3.2-preview", latest.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", latest.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", latest.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", latest.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2014-11-12T22:19:16.297"), latest.Published.Value);
            Assert.Equal("http://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/4.3.2-preview", latest.ReportAbuseUrl);
            Assert.True(latest.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", latest.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", latest.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.3:aspnetcore50|Microsoft.Data.Services.Client:5.6.3:aspnetcore50|System.Spatial:5.6.3:aspnetcore50|System.Collections:4.0.10-beta-22231:aspnetcore50|System.Collections.Concurrent:4.0.0-beta-22231:aspnetcore50|System.Collections.Specialized:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Debug:4.0.10-beta-22231:aspnetcore50|System.Diagnostics.Tools:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.TraceSource:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Tracing:4.0.10-beta-22231:aspnetcore50|System.Dynamic.Runtime:4.0.0-beta-22231:aspnetcore50|System.Globalization:4.0.10-beta-22231:aspnetcore50|System.IO:4.0.10-beta-22231:aspnetcore50|System.IO.FileSystem:4.0.0-beta-22231:aspnetcore50|System.IO.FileSystem.Primitives:4.0.0-beta-22231:aspnetcore50|System.Linq:4.0.0-beta-22231:aspnetcore50|System.Linq.Expressions:4.0.0-beta-22231:aspnetcore50|System.Linq.Queryable:4.0.0-beta-22231:aspnetcore50|System.Net.Http:4.0.0-beta-22231:aspnetcore50|System.Net.Primitives:4.0.10-beta-22231:aspnetcore50|System.Reflection:4.0.10-beta-22231:aspnetcore50|System.Reflection.Extensions:4.0.0-beta-22231:aspnetcore50|System.Reflection.TypeExtensions:4.0.0-beta-22231:aspnetcore50|System.Runtime:4.0.20-beta-22231:aspnetcore50|System.Runtime.Extensions:4.0.10-beta-22231:aspnetcore50|System.Runtime.InteropServices:4.0.20-beta-22231:aspnetcore50|System.Runtime.Serialization.Primitives:4.0.0-beta-22231:aspnetcore50|System.Runtime.Serialization.Xml:4.0.10-beta-22231:aspnetcore50|System.Security.Cryptography.Encoding:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Encryption:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing.Algorithms:4.0.0-beta-22231:aspnetcore50|System.Text.Encoding:4.0.10-beta-22231:aspnetcore50|System.Text.Encoding.Extensions:4.0.10-beta-22231:aspnetcore50|System.Text.RegularExpressions:4.0.10-beta-22231:aspnetcore50|System.Threading:4.0.0-beta-22231:aspnetcore50|System.Threading.Tasks:4.0.10-beta-22231:aspnetcore50|System.Threading.Thread:4.0.0-beta-22231:aspnetcore50|System.Threading.ThreadPool:4.0.10-beta-22231:aspnetcore50|System.Threading.Timer:4.0.0-beta-22231:aspnetcore50|System.Xml.ReaderWriter:4.0.10-beta-22231:aspnetcore50|System.Xml.XDocument:4.0.0-beta-22231:aspnetcore50|System.Xml.XmlSerializer:4.0.0-beta-22231:aspnetcore50|Microsoft.Data.OData:5.6.3:aspnet50|Microsoft.Data.Services.Client:5.6.3:aspnet50|System.Spatial:5.6.3:aspnet50|Microsoft.Data.OData:5.6.2:net40-Client|Newtonsoft.Json:5.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.2:net40-Client|Microsoft.WindowsAzure.ConfigurationManager:1.8.0.0:net40-Client|Microsoft.Data.OData:5.6.2:win80|Microsoft.Data.OData:5.6.2:wpa|Microsoft.Data.OData:5.6.2:wp80|Newtonsoft.Json:5.0.8:wp80", latest.Dependencies);
            Assert.Equal(6, latest.DependencySets.Count());
            Assert.Equal("aspnetcore50", latest.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_DownloadFromUrl()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new HttpClientHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("https://www.nuget.org/api/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            using(var downloadResult = await parser.DownloadFromUrl(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0")),
                                                              new Uri("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.0"),
                                                              Configuration.NullSettings.Instance,
                                                              NullLogger.Instance, 
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }

        [Fact]
        public async Task V2FeedParser_DownloadFromIdentity()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new HttpClientHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("https://www.nuget.org/api/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            using (var downloadResult = await parser.DownloadFromIdentity(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0")),
                                                              Configuration.NullSettings.Instance,
                                                              NullLogger.Instance,
                                                              CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(11, files.Count());
            }
        }

        [Fact]
        public async Task V2FeedParser_Search()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
               Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net40-Client" }
            };
            var packages = await parser.Search("azure", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var package = packages.FirstOrDefault();

            Assert.Equal("WindowsAzure.Storage", package.Id);
            Assert.Equal("6.2.0", package.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", package.Title);
            Assert.Equal("Microsoft", String.Join(",", package.Authors));
            Assert.Equal("", String.Join(",", package.Owners));
            Assert.True(package.Description.StartsWith("This client library enables"));
            Assert.Equal(3863927, package.DownloadCountAsInt);
            Assert.Equal("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.0", package.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", package.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", package.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", package.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2015-12-10T22:39:05.103"), package.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/6.2.0", package.ReportAbuseUrl);
            Assert.True(package.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", package.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", package.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.4:net40-Client|Newtonsoft.Json:6.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.4:net40-Client|Microsoft.Azure.KeyVault.Core:1.0.0:net40-Client|Microsoft.Data.OData:5.6.4:win80|Newtonsoft.Json:6.0.8:win80|Microsoft.Data.OData:5.6.4:wpa|Newtonsoft.Json:6.0.8:wpa|Microsoft.Data.OData:5.6.4:wp80|Newtonsoft.Json:6.0.8:wp80|Microsoft.Azure.KeyVault.Core:1.0.0:wp80", package.Dependencies);
            Assert.Equal(4, package.DependencySets.Count());
            Assert.Equal("net40-client", package.DependencySets.First().TargetFramework.GetShortFolderName());
        }

        [Fact]
        public async Task V2FeedParser_SearchTop100()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
              Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");
            var searchFilter = new SearchFilter()
            {
                IncludePrerelease = false,
                SupportedFrameworks = new string[] { "net40-Client" }
            };
            var packages = await parser.Search("azure", searchFilter, 0, 100, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(100, packages.Count());
        }

        [Fact]
        public async Task V2FeedParser_GetPackage()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
               Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");

            var package = await parser.GetPackage(new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.2-preview")), NullLogger.Instance, CancellationToken.None);

            Assert.Equal("WindowsAzure.Storage", package.Id);
            Assert.Equal("4.3.2-preview", package.Version.ToNormalizedString());
            Assert.Equal("WindowsAzure.Storage", package.Title);
            Assert.Equal("Microsoft", String.Join(",", package.Authors));
            Assert.Equal("", String.Join(",", package.Owners));
            Assert.True(package.Description.StartsWith("This client library enables"));
            Assert.Equal(3916917, package.DownloadCountAsInt);
            Assert.Equal("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/4.3.2-preview", package.DownloadUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkID=288890", package.IconUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=331471", package.LicenseUrl);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=235168", package.ProjectUrl);
            Assert.Equal(DateTimeOffset.Parse("2014-11-12T22:19:16.297"), package.Published.Value);
            Assert.Equal("https://www.nuget.org/package/ReportAbuse/WindowsAzure.Storage/4.3.2-preview", package.ReportAbuseUrl);
            Assert.True(package.RequireLicenseAcceptance);
            Assert.Equal("A client library for working with Microsoft Azure storage services including blobs, files, tables, and queues.", package.Summary);
            Assert.Equal("Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial", package.Tags);
            Assert.Equal("Microsoft.Data.OData:5.6.3:aspnetcore50|Microsoft.Data.Services.Client:5.6.3:aspnetcore50|System.Spatial:5.6.3:aspnetcore50|System.Collections:4.0.10-beta-22231:aspnetcore50|System.Collections.Concurrent:4.0.0-beta-22231:aspnetcore50|System.Collections.Specialized:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Debug:4.0.10-beta-22231:aspnetcore50|System.Diagnostics.Tools:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.TraceSource:4.0.0-beta-22231:aspnetcore50|System.Diagnostics.Tracing:4.0.10-beta-22231:aspnetcore50|System.Dynamic.Runtime:4.0.0-beta-22231:aspnetcore50|System.Globalization:4.0.10-beta-22231:aspnetcore50|System.IO:4.0.10-beta-22231:aspnetcore50|System.IO.FileSystem:4.0.0-beta-22231:aspnetcore50|System.IO.FileSystem.Primitives:4.0.0-beta-22231:aspnetcore50|System.Linq:4.0.0-beta-22231:aspnetcore50|System.Linq.Expressions:4.0.0-beta-22231:aspnetcore50|System.Linq.Queryable:4.0.0-beta-22231:aspnetcore50|System.Net.Http:4.0.0-beta-22231:aspnetcore50|System.Net.Primitives:4.0.10-beta-22231:aspnetcore50|System.Reflection:4.0.10-beta-22231:aspnetcore50|System.Reflection.Extensions:4.0.0-beta-22231:aspnetcore50|System.Reflection.TypeExtensions:4.0.0-beta-22231:aspnetcore50|System.Runtime:4.0.20-beta-22231:aspnetcore50|System.Runtime.Extensions:4.0.10-beta-22231:aspnetcore50|System.Runtime.InteropServices:4.0.20-beta-22231:aspnetcore50|System.Runtime.Serialization.Primitives:4.0.0-beta-22231:aspnetcore50|System.Runtime.Serialization.Xml:4.0.10-beta-22231:aspnetcore50|System.Security.Cryptography.Encoding:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Encryption:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing:4.0.0-beta-22231:aspnetcore50|System.Security.Cryptography.Hashing.Algorithms:4.0.0-beta-22231:aspnetcore50|System.Text.Encoding:4.0.10-beta-22231:aspnetcore50|System.Text.Encoding.Extensions:4.0.10-beta-22231:aspnetcore50|System.Text.RegularExpressions:4.0.10-beta-22231:aspnetcore50|System.Threading:4.0.0-beta-22231:aspnetcore50|System.Threading.Tasks:4.0.10-beta-22231:aspnetcore50|System.Threading.Thread:4.0.0-beta-22231:aspnetcore50|System.Threading.ThreadPool:4.0.10-beta-22231:aspnetcore50|System.Threading.Timer:4.0.0-beta-22231:aspnetcore50|System.Xml.ReaderWriter:4.0.10-beta-22231:aspnetcore50|System.Xml.XDocument:4.0.0-beta-22231:aspnetcore50|System.Xml.XmlSerializer:4.0.0-beta-22231:aspnetcore50|Microsoft.Data.OData:5.6.3:aspnet50|Microsoft.Data.Services.Client:5.6.3:aspnet50|System.Spatial:5.6.3:aspnet50|Microsoft.Data.OData:5.6.2:net40-Client|Newtonsoft.Json:5.0.8:net40-Client|Microsoft.Data.Services.Client:5.6.2:net40-Client|Microsoft.WindowsAzure.ConfigurationManager:1.8.0.0:net40-Client|Microsoft.Data.OData:5.6.2:win80|Microsoft.Data.OData:5.6.2:wpa|Microsoft.Data.OData:5.6.2:wp80|Newtonsoft.Json:5.0.8:wp80", package.Dependencies);
            Assert.Equal(6, package.DependencySets.Count());
            Assert.Equal("aspnetcore50", package.DependencySets.First().TargetFramework.GetShortFolderName());
        }
    }
}