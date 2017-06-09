// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Protocol.Core.Types;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Use NuGet.Commands manually to perform a NuGet restore. This allows us to restore on the
    /// desktop framework on Windows, where encryption is available.
    /// 
    /// An alternative is to acquire nuget.exe, but it's heavier than NuGet.Commands and is harder
    /// to use in the build.
    /// </summary>
    public partial class EncryptedConfigNuGetRestore : Task
    {
        public ITaskItem[] Inputs { get; set; }

        public string ConfigFile { get; set; }

        public string PackagesDir { get; set; }

        public override bool Execute()
        {
            var args = new RestoreArgs
            {
                Inputs = Inputs.Select(item => item.ItemSpec).ToList(),
                ConfigFile = ConfigFile,
                GlobalPackagesFolder = PackagesDir,

                Sources = new List<string>(),
                FallbackSources = new List<string>(),
                CacheContext = new SourceCacheContext(),
                RequestProviders = new List<IRestoreRequestProvider>
                {
                    new ProjectJsonRestoreRequestProvider(new RestoreCommandProvidersCache())
                },
                Log = new NugetMsBuildLogger(new TaskLoggingHelper(this))
            };

            RestoreRunner.RunAsync(args).Wait();

            return !Log.HasLoggedErrors;
        }
    }
}
