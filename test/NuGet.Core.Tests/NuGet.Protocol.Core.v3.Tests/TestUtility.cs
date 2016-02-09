using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Protocol.Core.v3.Tests
{
    internal class TestContent : HttpContent
    {
        private MemoryStream _stream;

        public TestContent(string xml)
        {
            var doc = XDocument.Parse(xml);
            _stream = new MemoryStream();
            doc.Save(_stream);
            _stream.Seek(0, SeekOrigin.Begin);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            _stream.CopyTo(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = (long)_stream.Length;
            return true;
        }
    }

    internal class TestHandler : HttpClientHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage msg = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            if (request.RequestUri.AbsoluteUri == "http://testsource/v2/FindPackagesById()?Id='WindowsAzure.Storage'")
            {
                msg.Content = new TestContent(FindPackagesByIdData.WindowsAzureStorage);
            }
            else if (request.RequestUri.AbsoluteUri == "http://testsource/v2/FindPackagesById()?Id='ravendb.client'")
            {
                msg.Content = new TestContent(FindPackagesByIdData.MultiPage1);
            }
            else if (request.RequestUri.AbsoluteUri == "http://api.nuget.org/api/v2/FindPackagesById?id='ravendb.client'&$skiptoken='RavenDB.Client','1.2.2067-Unstable'")
            {
                msg.Content = new TestContent(FindPackagesByIdData.MultiPage2);
            }
            else if (request.RequestUri.AbsoluteUri == "http://api.nuget.org/api/v2/FindPackagesById?id='ravendb.client'&$skiptoken='RavenDB.Client','2.5.2617-Unstable'")
            {
                msg.Content = new TestContent(FindPackagesByIdData.MultiPage3);
            }
            else if (request.RequestUri.AbsoluteUri == "http://testsource/v2/Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-Client'&includePrerelease=false&$skip=0&$top=1")
            {
                msg.Content = new TestContent(SearchData.SearchAzureData1);
            }
            else if (request.RequestUri.AbsoluteUri == "http://testsource/v2/Search()?$filter=IsLatestVersion&searchTerm='azure'&targetFramework='net40-Client'&includePrerelease=false&$skip=0&$top=100")
            {
                msg.Content = new TestContent(SearchData.SearchAzureData100);
            }
            else
            {
                msg = new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Task.FromResult(msg);
        }
    }
}
