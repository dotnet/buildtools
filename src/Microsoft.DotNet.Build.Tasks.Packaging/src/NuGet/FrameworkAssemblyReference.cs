// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet
{
    public class FrameworkAssemblyReference
    {
        public FrameworkAssemblyReference(string assemblyName, IEnumerable<NuGetFramework> supportedFrameworks)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentException(nameof(assemblyName));
            }

            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException(nameof(supportedFrameworks));
            }

            AssemblyName = assemblyName;
            SupportedFrameworks = supportedFrameworks;
        }

        public string AssemblyName { get; private set; }

        public IEnumerable<NuGetFramework> SupportedFrameworks { get; private set; }
    }
}