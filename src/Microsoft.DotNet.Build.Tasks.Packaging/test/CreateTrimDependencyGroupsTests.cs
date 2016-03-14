// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class CreateTrimDependencyGroupsTests
    {
        private Log _log;
        private TestBuildEngine _engine;

        public CreateTrimDependencyGroupsTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);
        }

        [Fact]
        public void NoAdditionalDependenciesForPlaceholders()
        {
            ITaskItem[] files = new[]
            {
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoAndroid10", "MonoAndroid10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoTouch10", "MonoTouch10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/net45", "net45"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/win8", "win8"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/wp8", "wp8"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/wpa81", "wpa81"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinios10", "xamarinios10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinmac20", "xamarinmac20"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarintvos10", "xamarintvos10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinwatchos10", "xamarinwatchos10"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\System.Collections.Immutable.dll", "lib/netstandard1.0", "netstandard1.0"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Open\CoreFx\Windows_NT.x86.Release\System.Collections.Immutable\System.Collections.Immutable.xml", "lib/netstandard1.0", "netstandard1.0"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Open\CoreFx\Windows_NT.x86.Release\System.Collections.Immutable\System.Collections.Immutable.xml", "lib/portable-net45+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\System.Collections.Immutable.dll", "lib/portable-net45+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoAndroid10", "MonoAndroid10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoTouch10", "MonoTouch10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/net45", "net45"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/win8", "win8"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/wp8", "wp8"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/wpa81", "wpa81"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinios10", "xamarinios10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarintvos10", "xamarintvos10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinwatchos10", "xamarinwatchos10"),
                CreateFileItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinmac20", "xamarinmac20")
            };
            ITaskItem[] dependencies = new[]
            {
                CreateDependencyItem(@"_._", null, "MonoAndroid10"),
                CreateDependencyItem(@"_._", null, "MonoTouch10"),
                CreateDependencyItem(@"_._", null, "net45"),
                CreateDependencyItem(@"_._", null, "win8"),
                CreateDependencyItem(@"_._", null, "wp8"),
                CreateDependencyItem(@"_._", null, "wpa81"),
                CreateDependencyItem(@"_._", null, "xamarinios10"),
                CreateDependencyItem(@"_._", null, "xamarinmac20"),
                CreateDependencyItem(@"_._", null, "xamarintvos10"),
                CreateDependencyItem(@"_._", null, "xamarinwatchos10"),
                CreateDependencyItem(@"System.Runtime", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Resources.ResourceManager", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Collections", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Diagnostics.Debug", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Linq", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Runtime.Extensions", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Globalization", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Threading", "4.0.0", "netstandard1.0")
            };
            string frameworkListsPath = "FrameworkLists";

            CreateTrimDependencyGroups task = new CreateTrimDependencyGroups()
            {
                BuildEngine = _engine,
                Files = files,
                Dependencies = dependencies,
                FrameworkListsPath = frameworkListsPath
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);

            // Assert that we're not adding any new trimmed inbox dependencies, we have placeholders for all inbox frameworks.
            Assert.Equal(0, task.TrimmedDependencies.Length);
        }

        [Fact]
        public void NoPlaceholders()
        {
            ITaskItem[] files = new[]
            {
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\System.Collections.Immutable.dll", "lib/netstandard1.0", "netstandard1.0"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Open\CoreFx\Windows_NT.x86.Release\System.Collections.Immutable\System.Collections.Immutable.xml", "lib/netstandard1.0", "netstandard1.0"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Open\CoreFx\Windows_NT.x86.Release\System.Collections.Immutable\System.Collections.Immutable.xml", "lib/portable-net45+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\System.Collections.Immutable.dll", "lib/portable-net45+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"),
            };
            ITaskItem[] dependencies = new[]
            {
                CreateDependencyItem(@"System.Runtime", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Resources.ResourceManager", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Collections", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Diagnostics.Debug", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Linq", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Runtime.Extensions", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Globalization", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Threading", "4.0.0", "netstandard1.0")
            };
            string frameworkListsPath = "FrameworkLists";

            CreateTrimDependencyGroups task = new CreateTrimDependencyGroups()
            {
                BuildEngine = _engine,
                Files = files,
                Dependencies = dependencies,
                FrameworkListsPath = frameworkListsPath
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);

            // Assert that we're adding dependency groups for all 7 inbox tfm's we support
            Assert.Equal(10, task.TrimmedDependencies.Length);
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("win8")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("monoandroid1")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("monotouch1")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("net45")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("wp8")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("wpa81")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("xamarinios1")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("xamarintvos1")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("xamarinwatchos1")).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Contains("xamarinmac2")).Count());
            // Assert these are empty dependencygroups.
            Assert.All(task.TrimmedDependencies, f => f.ToString().Equals("_._"));
        }

        [Fact]
        public void MultiGeneration()
        {
            ITaskItem[] files = new[]
            {
                CreateFileItem(@"C:\bin\System.ComponentModel.dll", "lib/netstandard1.3", "netstandard1.3"),
                CreateFileItem(@"C:\bin\System.ComponentModel.dll", "lib/netcore50/System.ComponentModel.dll", "netcore50"),
                CreateFileItem(@"C:\bin\ns10\System.ComponentModel.dll", "lib/netstandard1.0", "netstandard1.0"),
                CreateFileItem(@"C:\bin\_._", "lib/MonoAndroid10", "MonoAndroid10"),
                CreateFileItem(@"C:\bin\_._", "lib/MonoTouch10", "MonoTouch10"),
                CreateFileItem(@"C:\bin\_._", "lib/win8", "win8"),
                CreateFileItem(@"C:\bin\_._", "lib/wp80", "wp80"),
                CreateFileItem(@"C:\bin\_._", "lib/wpa81", "wpa81"),
                CreateFileItem(@"C:\bin\_._", "lib/xamarinios10", "xamarinios10"),
                CreateFileItem(@"C:\bin\_._", "lib/xamarinmac20", "xamarinmac20"),
                CreateFileItem(@"C:\bin\_._", "lib/xamarintvos10", "xamarintvos10"),
                CreateFileItem(@"C:\bin\_._", "lib/xamarinwatchos10", "xamarinwatchos10"),
                CreateFileItem(@"C:\bin\ref\System.ComponentModel.dll", "ref/netstandard1.3", "netstandard1.3"),
                CreateFileItem(@"C:\bin\ref\System.ComponentModel.dll", "ref/netcore50/System.ComponentModel.dll", "netcore50"),
                CreateFileItem(@"C:\bin\ref\ns10\System.ComponentModel.dll", "ref/netstandard1.0", "netstandard1.0"),
                CreateFileItem(@"C:\bin\_._", "ref/MonoAndroid10", "MonoAndroid10"),
                CreateFileItem(@"C:\bin\_._", "ref/MonoTouch10", "MonoTouch10"),
                CreateFileItem(@"C:\bin\_._", "ref/win8", "win8"),
                CreateFileItem(@"C:\bin\_._", "ref/wp80", "wp80"),
                CreateFileItem(@"C:\bin\_._", "ref/wpa81", "wpa81"),
                CreateFileItem(@"C:\bin\_._", "ref/xamarinios10", "xamarinios10"),
                CreateFileItem(@"C:\bin\_._", "ref/xamarintvos10", "xamarintvos10"),
                CreateFileItem(@"C:\bin\_._", "ref/xamarinwatchos10", "xamarinwatchos10"),
                CreateFileItem(@"C:\bin\_._", "ref/xamarinmac20", "xamarinmac20"),
            };
            ITaskItem[] dependencies = new[]
            {
                CreateDependencyItem(@"_._", null, "MonoAndroid10"),
                CreateDependencyItem(@"_._", null, "MonoTouch10"),
                CreateDependencyItem(@"_._", null, "win8"),
                CreateDependencyItem(@"_._", null, "wp8"),
                CreateDependencyItem(@"_._", null, "wpa81"),
                CreateDependencyItem(@"_._", null, "xamarinios10"),
                CreateDependencyItem(@"_._", null, "xamarinmac20"),
                CreateDependencyItem(@"_._", null, "xamarintvos10"),
                CreateDependencyItem(@"_._", null, "xamarinwatchos10"),
                CreateDependencyItem(@"System.Runtime", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Runtime", "4.0.20", "netstandard1.3"),
                // Make up some dependencies which are not inbox on net45, net451, net46
                CreateDependencyItem(@"System.Collections.Immutable", "4.0.0", "netstandard1.0"),
                CreateDependencyItem(@"System.Collections.Immutable", "4.0.20", "netstandard1.2"),
                CreateDependencyItem(@"System.Collections.Immutable", "4.0.20", "netstandard1.3"),
                CreateDependencyItem(@"System.Runtime", "4.0.20", ".NETCore50")
            };
            string frameworkListsPath = "FrameworkLists";

            CreateTrimDependencyGroups task = new CreateTrimDependencyGroups()
            {
                BuildEngine = _engine,
                Files = files,
                Dependencies = dependencies,
                FrameworkListsPath = frameworkListsPath
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);

            // System.Collections.Immutable is not inbox and we've specified different versions for netstandard1.0 and netstandard1.3, so
            // we're expecting those dependencies to both be present for the net45 and net46 target frameworks.
            Assert.Equal(2, task.TrimmedDependencies.Length);
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Equals("net45") && f.ItemSpec.Equals("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase)).Count());
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Equals("net451") && f.ItemSpec.Equals("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase)).Count());
        }

        [Fact]
        public void NotSupported()
        {
            ITaskItem[] files = new[]
            {
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\dnxcore\System.Threading.AccessControl.dll", "lib/DNXCore50", "dnxcore50"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\net\System.Threading.AccessControl.dll", "lib/net46", "net46"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Contracts\System.Threading.AccessControl\4.0.0.0\System.Threading.AccessControl.dll", "ref/netstandard1.3", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Threading.AccessControl.xml", "ref/netstandard1.3", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Threading.AccessControl.xml", "ref/netstandard1.3/zh-hant", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Threading.AccessControl.xml", "ref/netstandard1.3/de", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Threading.AccessControl.xml", "ref/netstandard1.3/fr", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Threading.AccessControl.xml", "ref/netstandard1.3/it", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Threading.AccessControl.xml", "ref/netstandard1.3/ja", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Threading.AccessControl.xml", "ref/netstandard1.3/ko", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Threading.AccessControl.xml", "ref/netstandard1.3/ru", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Threading.AccessControl.xml", "ref/netstandard1.3/zh-hans", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Threading.AccessControl.xml", "ref/netstandard1.3/es", "netstandard1.3"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\net\System.Threading.AccessControl.dll", "ref/net46", "netstandard1.3")
            };
            ITaskItem[] dependencies = new[]
            {
                CreateDependencyItem(@"System.Runtime", "4.0.20", "dnxcore50"),
                CreateDependencyItem(@"System.Resources.ResourceManager", "4.0.0", "dnxcore50"),
                CreateDependencyItem(@"System.Security.AccessControl", "4.0.0-rc2-23516", "dnxcore50"),
                CreateDependencyItem(@"System.Security.Principal.Windows", "4.0.0-rc2-23516", "dnxcore50"),
                CreateDependencyItem(@"System.Runtime.Handles", "4.0.0", "dnxcore50"),
                CreateDependencyItem(@"System.Threading", "4.0.10", "dnxcore50")
            };
            string frameworkListsPath = "FrameworkLists";

            CreateTrimDependencyGroups task = new CreateTrimDependencyGroups()
            {
                BuildEngine = _engine,
                Files = files,
                Dependencies = dependencies,
                FrameworkListsPath = frameworkListsPath
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);

            // Assert that we're not adding any new trimmed inbox dependencies, for unsupported inbox frameworks.
            Assert.Equal(0, task.TrimmedDependencies.Length);
        }

        [Fact]
        public void AddInboxFrameworkGroupsAndDependencies()
        {
            ITaskItem[] files = new[]
            {
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\System.Reflection.Metadata.dll", "lib/netstandard1.1", "netstandard1.1"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Open\CoreFx\Windows_NT.x86.Release\System.Reflection.Metadata\System.Reflection.Metadata.xml", "lib/netstandard1.1", "netstandard1.1"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\Open\CoreFx\Windows_NT.x86.Release\System.Reflection.Metadata\System.Reflection.Metadata.xml", "lib/portable-net45+win8", "portable-net45+win8"),
                CreateFileItem(@"E:\ProjectK\binaries\x86ret\NETCore\Libraries\System.Reflection.Metadata.dll", "lib/portable-net45+win8", "portable-net45+win8"),
            };
            ITaskItem[] dependencies = new[]
            {
                CreateDependencyItem(@"System.Runtime", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Resources.ResourceManager", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Reflection.Primitives", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.IO", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Collections", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Diagnostics.Debug", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Text.Encoding", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Runtime.InteropServices", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Reflection", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Runtime.Extensions", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Threading", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Text.Encoding.Extensions", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Reflection.Extensions", "4.0.0", "netstandard1.1"),
                CreateDependencyItem(@"System.Collections.Immutable", "1.1.37", "netstandard1.1"),
                CreateDependencyItem(@"System.Collections.Immutable", "1.1.37", "portable-net45+win80")
            };
            string frameworkListsPath = "FrameworkLists";

            CreateTrimDependencyGroups task = new CreateTrimDependencyGroups()
            {
                BuildEngine = _engine,
                Files = files,
                Dependencies = dependencies,
                FrameworkListsPath = frameworkListsPath
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            IEnumerable<string> tmp = task.TrimmedDependencies.Select(f => f.GetMetadata("TargetFramework"));

            // Assert that we're creating new dependency groups
            Assert.Equal(9, task.TrimmedDependencies.Length);

            // System.Collections.Immutable is not inbox
            Assert.Equal(1, task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Equals("monoandroid10")).Count());

            // The only added dependency in wpa81 is System.Collections.Immutable
            Assert.Equal("System.Collections.Immutable", task.TrimmedDependencies.Where(f => f.GetMetadata("TargetFramework").Equals("wpa81")).First().ItemSpec);
        }

        public static ITaskItem CreateFileItem(string sourcePath, string targetPath, string targetFramework)
        {
            TaskItem item = new TaskItem(sourcePath);
            item.SetMetadata("TargetPath", targetPath);
            item.SetMetadata("TargetFramework", targetFramework);
            return item;
        }
        public static ITaskItem CreateDependencyItem(string sourcePath, string version, string targetFramework)
        {
            TaskItem item = new TaskItem(sourcePath);

            if (version != null)
            {
                item.SetMetadata("Version", version);
            }

            item.SetMetadata("TargetFramework", targetFramework);
            return item;
        }
    }
}
