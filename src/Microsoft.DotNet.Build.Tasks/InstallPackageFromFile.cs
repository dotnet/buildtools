// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using Task = Microsoft.Build.Utilities.Task;
using ThreadingTask = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.Tasks
{
    public class InstallPackageFromFile : BuildTask, ICancelableTask
    {
        [Required]
        public string PackageFile { get; set; }

        [Required]
        public string PackagesFolder { get; set; }

        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;
        internal const int DefaultBufferSize = 81920;

        public override bool Execute()
        {
            return ExecuteAsync(CancellationToken).Result;
        }

        public void Cancel()
        {
            TokenSource.Cancel();
        }

        private async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (Stream stream = File.OpenRead(PackageFile))
            {
                var reader = new PackageArchiveReader(stream, leaveStreamOpen: true);
                var packageIdentity = reader.GetIdentity();
                reader.Dispose();

                stream.Seek(0, SeekOrigin.Begin);

                await
                    InstallFromStream(ThreadingTask.FromResult(stream), packageIdentity, PackagesFolder,
                        cancellationToken);
            }
            return true;
        }

        private async ThreadingTask InstallFromStream(Task<Stream> getStream, PackageIdentity package,
            string packagesFolder, CancellationToken token)
        {
            bool isValid = true;
            if (OfflineFeedUtility.PackageExists(package, packagesFolder, out isValid))
            {
                return;
            }

            var logger = new NugetMsBuildLogger(new TaskLoggingHelper(this));

            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesFolder,
                isLowercasePackagesDirectory: false,
                logger: logger,
                packageSaveMode: PackageSaveMode.Defaultv3,
                xmlDocFileSaveMode: XmlDocFileSaveMode.None);

            await PackageExtractor.InstallFromSourceAsync(
                async dest =>
                {
                    var source = await getStream;
                    await source.CopyToAsync(dest, bufferSize: DefaultBufferSize, cancellationToken: token);
                },
                versionFolderPathContext,
                token);
        }
    }
}