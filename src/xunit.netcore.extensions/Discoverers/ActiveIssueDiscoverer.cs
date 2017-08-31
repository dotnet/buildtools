// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the ActiveIssue attribute
    /// </summary>
    public class ActiveIssueDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            IEnumerable<object> ctorArgs = traitAttribute.GetConstructorArguments();
            Debug.Assert(ctorArgs.Count() >= 2);

            string issue = ctorArgs.First().ToString();
            TestPlatforms platforms = TestPlatforms.Any;
            TargetFrameworkMonikers frameworks = (TargetFrameworkMonikers)0;
            TestArchitectures architectures = TestArchitectures.Any;

            foreach (object arg in ctorArgs.Skip(1)) // First argument is the issue number.
            {
                if (arg is TestPlatforms)
                {
                    platforms = (TestPlatforms)arg;
                }
                else if (arg is TargetFrameworkMonikers)
                {
                    frameworks = (TargetFrameworkMonikers)arg;
                }
                else if (arg is TestArchitectures)
                {
                    architectures = (TestArchitectures)arg;
                }
            }

            bool appliesToPlatform =
                (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))) ||
                (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
                (platforms.HasFlag(TestPlatforms.NetBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD"))) ||
                (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
                (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            bool appliesToArchitecture =
                (architectures.HasFlag(TestArchitectures.X86) && RuntimeInformation.ProcessArchitecture == Architecture.X86) ||
                (architectures.HasFlag(TestArchitectures.X64) && RuntimeInformation.ProcessArchitecture == Architecture.X64) ||
                (architectures.HasFlag(TestArchitectures.Arm) && RuntimeInformation.ProcessArchitecture == Architecture.Arm) ||
                (architectures.HasFlag(TestArchitectures.Arm64) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64);

            if (appliesToPlatform && appliesToArchitecture)
            {
                if (frameworks.HasFlag(TargetFrameworkMonikers.NetFramework))
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetfxTest);
                if (frameworks.HasFlag(TargetFrameworkMonikers.Netcoreapp))
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreappTest);
                if (frameworks.HasFlag(TargetFrameworkMonikers.UapNotUapAot))
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonUapTest);
                if (frameworks.HasFlag(TargetFrameworkMonikers.UapAot))
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonUapAotTest);
                if (frameworks.HasFlag(TargetFrameworkMonikers.NetcoreCoreRT))
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreCoreRTTest);
                if (frameworks == (TargetFrameworkMonikers)0)
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing);

                yield return new KeyValuePair<string, string>(XunitConstants.ActiveIssue, issue);
            }
        }
    }
}
