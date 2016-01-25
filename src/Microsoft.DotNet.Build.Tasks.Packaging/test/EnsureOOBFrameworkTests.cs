// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class EnsureOOBFrameworkTests
    {
        private Log _log;
        private TestBuildEngine _engine;

        public EnsureOOBFrameworkTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);
        }

        [Fact]
        public void OOBFxShouldAddRef()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/net45", "net45"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/wp80", "wp80"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/wpa81", "wpa81"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinmac20", "xamarinmac20"),
                CreateItem(@"D:\K2\binaries\x86ret\Contracts\System.Threading\4.0.0.0\System.Threading.dll", "ref/netstandard1.0", "netstandard1.0"),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Threading.xml", "ref/netstandard1.0", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Threading.xml", "ref/netstandard1.0/zh-hant", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Threading.xml", "ref/netstandard1.0/de", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Threading.xml", "ref/netstandard1.0/fr", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Threading.xml", "ref/netstandard1.0/it", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Threading.xml", "ref/netstandard1.0/ja", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Threading.xml", "ref/netstandard1.0/ko", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Threading.xml", "ref/netstandard1.0/ru", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Threading.xml", "ref/netstandard1.0/zh-hans", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Threading.xml", "ref/netstandard1.0/es", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Threading.xml", "ref/netstandard1.3/zh-hant", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Threading.xml", "ref/netstandard1.3/de", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Threading.xml", "ref/netstandard1.3", ""),
                CreateItem(@"D:\K2\binaries\x86ret\Contracts\System.Threading\4.0.11.0\System.Threading.dll", "ref/netstandard1.3", "netstandard1.3"),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Threading.xml", "ref/netstandard1.3/fr", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Threading.xml", "ref/netstandard1.3/it", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Threading.xml", "ref/netstandard1.3/ja", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Threading.xml", "ref/netstandard1.3/ko", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Threading.xml", "ref/netstandard1.3/ru", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Threading.xml", "ref/netstandard1.3/zh-hans", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Threading.xml", "ref/netstandard1.3/es", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/net45", "net45"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/wp80", "wp80"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/wpa81", "wpa81"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinmac20", "xamarinmac20"),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Manifests\System.Threading\runtime.json", "", "")
            };

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(11, task.AdditionalFiles.Length);
            Assert.All(task.AdditionalFiles, f => f.GetMetadata("TargetPath").Contains(oobFx[0]));
            Assert.All(task.AdditionalFiles, f => f.GetMetadata("TargetPath").StartsWith("ref"));
            Assert.All(task.AdditionalFiles, f => f.GetMetadata("TargetFramework").Equals(oobFx[0]));
        }

        [Fact]
        public void OOBFxShouldAddRefAndLib()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\System.Collections.Concurrent.dll", "lib/netstandard1.3", "netstandard1.3"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/net45", "net45"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/wpa81", "wpa81"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinmac20", "xamarinmac20"),
                CreateItem(@"D:\K2\binaries\x86ret\Contracts\System.Collections.Concurrent\4.0.0.0\System.Collections.Concurrent.dll", "ref/netstandard1.1", "netstandard1.1"),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Collections.Concurrent.xml", "ref/netstandard1.1", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Collections.Concurrent.xml", "ref/netstandard1.1/zh-hant", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Collections.Concurrent.xml", "ref/netstandard1.1/de", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Collections.Concurrent.xml", "ref/netstandard1.1/fr", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Collections.Concurrent.xml", "ref/netstandard1.1/it", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Collections.Concurrent.xml", "ref/netstandard1.1/ja", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Collections.Concurrent.xml", "ref/netstandard1.1/ko", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Collections.Concurrent.xml", "ref/netstandard1.1/ru", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Collections.Concurrent.xml", "ref/netstandard1.1/zh-hans", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Collections.Concurrent.xml", "ref/netstandard1.1/es", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Collections.Concurrent.xml", "ref/netstandard1.3/zh-hant", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Collections.Concurrent.xml", "ref/netstandard1.3/de", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Collections.Concurrent.xml", "ref/netstandard1.3", ""),
                CreateItem(@"D:\K2\binaries\x86ret\Contracts\System.Collections.Concurrent\4.0.11.0\System.Collections.Concurrent.dll", "ref/netstandard1.3", "netstandard1.3"),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Collections.Concurrent.xml", "ref/netstandard1.3/fr", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Collections.Concurrent.xml", "ref/netstandard1.3/it", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Collections.Concurrent.xml", "ref/netstandard1.3/ja", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Collections.Concurrent.xml", "ref/netstandard1.3/ko", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Collections.Concurrent.xml", "ref/netstandard1.3/ru", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Collections.Concurrent.xml", "ref/netstandard1.3/zh-hans", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Collections.Concurrent.xml", "ref/netstandard1.3/es", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/net45", "net45"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/wpa81", "wpa81"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinmac20", "xamarinmac20")
            };

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(12, task.AdditionalFiles.Length);
            Assert.All(task.AdditionalFiles, f => f.GetMetadata("TargetPath").Contains(oobFx[0]));
            Assert.All(task.AdditionalFiles, f => f.GetMetadata("TargetFramework").Equals(oobFx[0]));
        }

        [Fact]
        public void OOBFxShouldDoNothing()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\dnxcore\System.Xml.XmlSerializer.dll", "lib/DNXCore50", "dnxcore50"),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\dnxcore\System.Xml.XmlSerializer.dll", "lib/netcore50", "netcore50"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/net45", "net45"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/wpa81", "wpa81"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinmac20", "xamarinmac20")
            };

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(0, task.AdditionalFiles.Length);
        }

        [Fact]
        public void OOBFxShouldNotAddForCompletelyOOB()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\System.Xml.XPath.XDocument.dll", "lib/netstandard1.3", "netstandard1.3"),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\net\System.Xml.XPath.XDocument.dll", "lib/net46", "net46"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinmac20", "xamarinmac20"),
                CreateItem(@"D:\K2\binaries\x86ret\Contracts\System.Xml.XPath.XDocument\4.0.1.0\System.Xml.XPath.XDocument.dll", "ref/netstandard1.0", "netstandard1.0"),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/zh-hant", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/de", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/fr", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/it", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/ja", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/ko", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/ru", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/zh-hans", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Xml.XPath.XDocument.xml", "ref/netstandard1.0/es", ""),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\net\System.Xml.XPath.XDocument.dll", "ref/net46", "net46"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoAndroid10", "MonoAndroid10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoTouch10", "MonoTouch10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinios10", "xamarinios10"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinmac20", "xamarinmac20")
            };

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(0, task.AdditionalFiles.Length);
        }

        [Fact]
        public void OOBFxShouldNotAddWhenAlreadyExists()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/aot/lib/netcore50", ""),
                CreateItem(@"D:\K2\binaries\x86ret\Contracts\System.Reflection.Emit\4.0.1.0\System.Reflection.Emit.dll", "ref/netstandard1.1", "netstandard1.1"),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1033\System.Reflection.Emit.xml", "ref/netstandard1.1", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1028\System.Reflection.Emit.xml", "ref/netstandard1.1/zh-hant", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1031\System.Reflection.Emit.xml", "ref/netstandard1.1/de", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1036\System.Reflection.Emit.xml", "ref/netstandard1.1/fr", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1040\System.Reflection.Emit.xml", "ref/netstandard1.1/it", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1041\System.Reflection.Emit.xml", "ref/netstandard1.1/ja", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1042\System.Reflection.Emit.xml", "ref/netstandard1.1/ko", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\1049\System.Reflection.Emit.xml", "ref/netstandard1.1/ru", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\2052\System.Reflection.Emit.xml", "ref/netstandard1.1/zh-hans", ""),
                CreateItem(@"D:\K2\src\Redist\x86\retail\bin\i386\HelpDocs\intellisense\NETCore5\3082\System.Reflection.Emit.xml", "ref/netstandard1.1/es", ""),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\dnxcore\System.Reflection.Emit.dll", "lib/DNXCore50", "DNXCore50"),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\netcoreforcoreclr\System.Reflection.Emit.dll", "lib/netcore50", "netcore50"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/MonoAndroid10", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/MonoAndroid10", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/xamarinmac20", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/xamarinmac20", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "lib/net45", ""),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/net45", "")
};

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx,
                RuntimeId = "",
                RuntimeJson = "runtime.json"
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(0, task.AdditionalFiles.Length);
        }

        [Fact]
        public void OOBFxRuntimePackage()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/wp8", "wp8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/wpa81", "wpa81"),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\dnxcore\System.Diagnostics.FileVersionInfo.dll", "runtimes/win7/lib/netstandard1.3", "netstandard1.3"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/netstandard", "netstandard")
            };

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx,
                RuntimeJson = "runtime.json",
                RuntimeId = "win7"
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(1, task.AdditionalFiles.Length);
        }

        [Fact]
        public void OOBFxRuntimePackageExplicitNotSupported()
        {
            ITaskItem[] files = new[]
            {
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/win8", "win8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/wp8", "wp8"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/wpa81", "wpa81"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "runtimes/win7/lib/netcore50", "netcore50"),
                CreateItem(@"D:\K2\binaries\x86ret\NETCore\Libraries\dnxcore\System.Diagnostics.FileVersionInfo.dll", "runtimes/win7/lib/netstandard1.3", "netstandard1.3"),
                CreateItem(@"D:\K2\src\NDP\FxCore\src\Packages\_._", "ref/netstandard", "netstandard")
            };

            string[] oobFx = new[] { "netcore50" };

            EnsureOOBFramework task = new EnsureOOBFramework()
            {
                BuildEngine = _engine,
                Files = files,
                OOBFrameworks = oobFx,
                RuntimeJson = "runtime.json",
                RuntimeId = "win7"
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(0, task.AdditionalFiles.Length);
        }
        private static ITaskItem CreateItem(string sourcePath, string targetPath, string targetFramework)
        {
            TaskItem item = new TaskItem(sourcePath);
            item.SetMetadata("TargetPath", targetPath);
            item.SetMetadata("TargetFramework", targetFramework);
            return item;
        }
    }
}
