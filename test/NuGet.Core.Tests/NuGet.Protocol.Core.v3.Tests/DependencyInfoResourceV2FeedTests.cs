using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class DependencyInfoResourceV2FeedTests
    {
        [Fact]
        public async Task DependencyInfoResourceV2Feed_GetDependencyInfoById()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");

            var dependencyInfoResource = new DependencyInfoResourceV2Feed(parser, null);

            var dependencyInfoList = await dependencyInfoResource.ResolvePackages("WindowsAzure.Storage", 
                                                                            NuGetFramework.Parse("aspnetcore50"),
                                                                            NullLogger.Instance, 
                                                                            CancellationToken.None);

            Assert.Equal(34, dependencyInfoList.Count());
        }

        [Fact]
        public async Task DependencyInfoResourceV2Feed_GetDependencyInfoByPackageIdentity()
        {
            Func<Task<HttpHandlerResource>> factory = () =>
                Task.FromResult<HttpHandlerResource>(new TestHttpHandler(new TestHandler()));

            var httpSource = new HttpSource(
                new Configuration.PackageSource("http://testsource/v2/"),
                factory);

            V2FeedParser parser = new V2FeedParser(httpSource, "http://testsource/v2/");

            var dependencyInfoResource = new DependencyInfoResourceV2Feed(parser, null);

            var packageIdentity = new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.2-preview"));

            var dependencyInfo = await dependencyInfoResource.ResolvePackage(packageIdentity,
                                                                            NuGetFramework.Parse("aspnetcore50"),
                                                                            NullLogger.Instance,
                                                                            CancellationToken.None);

            Assert.Equal(6, dependencyInfo.Dependencies.Count());
        }
    }
}
