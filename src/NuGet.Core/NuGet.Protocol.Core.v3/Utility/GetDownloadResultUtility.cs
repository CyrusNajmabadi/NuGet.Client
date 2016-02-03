using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public static class GetDownloadResultUtility
    {
        public static async Task<DownloadResourceResult> GetDownloadResultAsync(
           HttpSource client,
           PackageIdentity identity,
           Uri uri,
           ISettings settings,
           ILogger log,
           CancellationToken token)
        {
            // Uri is not null, so the package exists in the source
            // Now, check if it is in the global packages folder, before, getting the package stream

            // TODO: This code should respect no_cache settings and not write or read packages from the global packages folder
            var packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(identity, settings);

            if (packageFromGlobalPackages != null)
            {
                return packageFromGlobalPackages;
            }

            log.LogVerbose($"  GET: {uri}");

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var packageStream = await client.GetStreamAsync(uri, log, token))
                    {
                        var downloadResult = await GlobalPackagesFolderUtility.AddPackageAsync(identity,
                            packageStream,
                            settings,
                            log,
                            token);

                        return downloadResult;
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException && i < 2)
                {
                    string message = $"Error downloading {identity} from {uri} {ExceptionUtilities.DisplayMessage(ex)}";

                    log.LogWarning(message);
                }
                catch (Exception ex)
                {
                    throw new FatalProtocolException(ex);
                }
            }

            throw new InvalidOperationException("Reached an unexpected point in the code");
        }
    }
}
