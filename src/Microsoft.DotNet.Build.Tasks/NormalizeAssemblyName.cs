// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class NormalizeAssemblyName : BuildTask
    {
        [Required]
        public string AssemblyName { get; set; }

        [Output]
        public string NormalizedAssemblyName { get; set; }

        public override bool Execute()
        {
            try
            {
                NormalizedAssemblyName = Helper.NormalizeAssemblyName(AssemblyName);
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to normalize the assembly name {AssemblyName} with error:\n{e.Message}");
                return false; // fail the task
            }

            return true;
        }
    }
}
