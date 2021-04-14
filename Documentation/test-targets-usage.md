# Test Targets

BuildTools provides test targets and related logic for publishing and running tests.  The primary entry points are the [`Test` target](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L340) in Microsoft.DotNet.Build.Tasks and the [`CloudBuild` target](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L79) in Microsoft.DotNet.Build.CloudTestTasks.  The `Test` target is used to run or archive tests locally, and the `CloudBuild` target is used to run tests in Helix.

## Usage scenarios

*Tips:*
- If building in a non-Windows environment, call `<repo-root>/Tools/msbuild.sh` instead of just `msbuild`.
- Dotnet repos typically provide `BuildAndTest` and `RebuildAndTest` targets which can be used to build and run tests from a single command, so you may wish to substitute either of those targets for `Test` in the examples below.
 
#### Run tests for a project with the default options

The following command runs tests for System.Collections.Immutable.Tests.csproj using the default options.
```
msbuild /t:Test System.Collections.Immutable.Tests.csproj
```

#### Run a single XUnit method

You can use the `XUnitOptions` property to override the options used for the XUnit command that runs the tests.  For example, the following command runs only the `System.Security.Cryptography.Pkcs.Tests.CmsRecipientCollectionTests.Twoary` method.

```
msbuild /t:Test "/p:XunitOptions=-method System.Security.Cryptography.Pkcs.Tests.CmsRecipientCollectionTests.Twoary" System.Security.Cryptography.Pkcs.Tests.csproj
```

#### Debug tests in Visual Studio

1.  Open the project's solution in Visual Studio.
2.  Set the test project as the Startup Project.
3.  Build (or rebuild) the solution.
4.  Press F5 to debug the project.

To modify the XUnit command which will be executed when debugging, edit the *Command line arguments* in the *Debug* section of the test project's properties, for example to debug just a single test method.  Note that these changes will be overwritten when rebuilding the project.

#### Run tests for a specified TFM and/or RID

To specify the target framework moniker (TFM) on which to run tests, use the `TestTFM` property.  Similarly, the runtime ID (RID) can be specified with the `TestNugetRuntimeId` property.

For example, the first command below runs tests against .NET Framework Desktop, and the second runs tests against .NET Core 5.0 for Windows 10 x64 (using the UWP runner).
```
msbuild /t:Test /p:TestTFM=net46 /p:TargetGroup=netstandard1.3 /p:OSGroup=Windows_NT System.Collections.Concurrent.Tests.csproj
msbuild /t:Test /p:TestTFM=netcore50 /p:TargetGroup=netstandard1.3 /p:OSGroup=Windows_NT /p:TestNugetRuntimeId=win10-x64 System.Collections.Concurrent.Tests.csproj
```

As the above commands suggest, it is often necessary to specify the `TargetGroup` and/or `OSGroup` together with the `TestTFM`.  For this purpose, CoreFX provides a .builds file for each test project which specifies supported configurations for the project.  These can be used to build a specified TFM by using just `FilterToTestTFM` property.  See [CoreFX's developer guide](https://github.com/dotnet/corefx/blob/release/3.1/Documentation/project-docs/developer-guide.md#running-tests-in-a-different-tfm) for more information on this approach.

#### Build and run tests with .NET Native (Windows only)

Tests can be compiled and run with .NET Native by specifying the `UseDotNetNativeToolchain` property.

```
msbuild /t:BuildAndTest /p:TestTFM=netcore50aot /p:TestNugetRuntimeId=win10-x64-aot /p:UseDotNetNativeToolchain=true Microsoft.CSharp.Tests.csproj
```

#### Run code coverage tests

Use the `Coverage` property to run code coverage tests.

```
msbuild /t:Test /p:Coverage=true System.Collections.Immutable.Tests.csproj
```

#### Run performance tests

Use the `Performance` property to run performance tests.

```
msbuild /t:Test /p:Performance=true System.Collections.Immutable.Tests.csproj
```

#### Archive tests for running remotely

Use the `ArchiveTests` property to archive tests.  This is typically used to prepare for running the tests in Helix.

```
msbuild /t:Test /p:ArchiveTests=true System.Collections.Immutable.Tests.csproj
```

The [CoreFX official build](https://github.com/dotnet/corefx/blob/021c590e6cb166c4bfc62b2c9966e317c37c1ed6/buildpipeline/DotNet-CoreFx-Trusted-Windows-Build-Test.json#L178-L179) provides an example of this usage.

#### Run tests in Helix

The `CloudBuild` target can be used to queue a test run in Helix.  The required properties are validated at [the top of the `VerifyInputs` target](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L85-L98).

See [the CoreFX official build](https://github.com/dotnet/corefx/blob/021c590e6cb166c4bfc62b2c9966e317c37c1ed6/buildpipeline/DotNet-CoreFx-Trusted-Windows-Build-Test.json#L215-L216) for an example.

## Other common properties

The following Boolean properties can also be used when running the `Test` target:
- `ForceRunTests`:  Run tests even if the input files haven't changed since the tests were last successfully run.
- `SkipTests`:  Skip running tests.
- `Outerloop`:  Include outerloop tests in the test execution.
- `TestWithLocalLibraries`:  Use locally-built libraries for all test dependencies, rather than using packages for the dependencies not directly referenced by the test project.
- `TestWithLocalNativeLibraries`:  Use locally-built native libraries.

The following properties, each specified as a semicolon-separate list, can be used to specify which XUnit test categories should be run:
- `WithCategories`:  Run tests for these categories.
- `WithoutCategories`:  Do not run tests for these categories.

For example, tests in the `OuterLoop` and `failing` categories are excluded by default, but you can run only the tests which are in either of those two categories with the following command:
```
msbuild /t:Test /p:WithCategories="OuterLoop;failing"
```
