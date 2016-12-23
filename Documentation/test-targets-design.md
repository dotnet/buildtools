# Test Target Design

This document describes the design of the BuildTools test targets. For documentation on their usage, see [Test Targets](test-targets-usage.md).

The primary entry points are the [`Test` target](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L340) in Microsoft.DotNet.Build.Tasks and the [`CloudBuild` target](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L79) in Microsoft.DotNet.Build.CloudTestTasks, which are each described below.

## `Test` target

The `Test` target is used to run or archive tests locally. At a high level, this target performs four steps:

1. Copies the required files for running the tests to the test execution directory.
2. Generates the test execution script, which was introduced for running the tests in distributed automation systems such as [Helix](https://helix.dot.net/), but is also used for running them locally.
3. (Optional) Runs the tests locally.
4. (Optional) Archives the tests, typically for running in Helix.

When the tests are run in Helix, the common test runtime dependencies which aren't directly referenced by the test project are copied from a common location into each test project's test execution directory at test execution time so that these files aren't duplicated across each project.

The `Test` target depends on the following targets:

#### [`SetupTestProperties`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L325)
Sets properties and items for the `Test` target. For example, `GetDefaultTestRid` sets the `TestNugetRuntimeId` property if it isn't already set, and `CheckTestPlatforms` disables the execution of the tests if the `TargetOS` is unsupported.

#### [`CopyTestToTestDirectory`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/publishtest.targets#L143)

Copies the test dependencies which are specific to the test project (e.g. the binaries for the test project and the projects it directly references) to the test directory. This copy happens at build-time in all cases. These items are calculated in [`DiscoverTestInputs`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L125).

The [`CopySupplementalTestData`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/publishtest.targets#L162) target is executed after this target. This target copies the `SupplementalTestData` items to the test execution directory, which are files that are shared between multiple projects. These are copied in a separate step because, unlike the other files, they cannot be copied using hard links; doing so would result in race conditions between the archiving and copying of different links of the file. 

#### [`CopyDependenciesToTestDirectory`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/publishtest.targets#L196)

Copies the common test runtime dependencies which aren't directly referenced by the test project to the test execution directory. These items are calculated in [`DiscoverTestDependencies`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/publishtest.targets#L24). This target is only executed when not archiving the tests.

#### [`GenerateTestBindingRedirects`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L151)

Generates assembly binding redirects when running tests against .NET Framework Desktop.

#### [`GenerateTestExecutionScripts`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L168)

Generates a script for running the tests. This script is either a batch file or a Bash script, depending on the `TargetOS`.

The script performs two high-level steps:

1. Copies the common test runtime dependencies calculated in `DiscoverTestDependencies` to the test execution directory. Each copy command no-ops if the file already exists in the test execution directory.
2. Runs the tests.

#### [`RunTestsForProject`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L265)

Runs the tests by invoking the test execution script. This target can be skipped by setting the `SkipTests` property to `True`.

This target is not executed if the [input files](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L129-L133) (the test project's binaries and direct dependencies) have not changed since the tests were last successfully run. This behavior can be overriden by setting the `ForceRunTests` property to `True`. This is implemented by creating a `TestsSuccessfulSemaphore` file when the tests are successfully run and declaring it as one of the `Outputs` of the `RunTestsForProject` target; this sempahore is deleted in `SetupTestProperties` if `ForceRunTests` is true.

#### [`ArchiveTestBuild`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/publishtest.targets#L221)

Archives the test execution directory. This target is only executed if `ArchiveTests` is set to `True`.

## Debugging a test project in Visual Studio

When building a test project in Visual Studio, the build [sets the project's debug settings](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tests.targets#L117-L123) to directly invoke the test program (e.g. the XUnit executable) to run the tests. This is used instead of the test execution script because the tests can only be debugged by attaching directly to the test program's process.

It then executes a subset of the `Test` subtargets: `CopyTestToTestDirectory`, `CopyDependenciesToTestDirectory`, and `GenerateTestBindingRedirects`. This is achieved by [adding them to `PrepareForRunDependsOn`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.Tasks/PackageFiles/publishtest.targets#L202-L204).

## `CloudBuild` target

The `CloudBuild` target is used to run tests in Helix. At a high level, this target performs four steps:

1. Gathers the list of test archives to upload to Azure.
2. Generates JSON files specifying information used when running the tests in Helix.
3. Uploads the test archives and other required files for running the tests in Helix.
4. Submits a Helix job which downloads the archives from Azure and runs the tests.

It depends on the following targets:

#### [`VerifyInputs`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L84)

Verifies that all required properties have been specified, and then gathers the test archives for this build, optionally filtering them based on `FilterToTestTFM` and `FilterToOSGroup`.

#### [`PreCloudBuild`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L187)

Prepares other files required for running the tests in Helix to be uploaded to Azure, such as the runner scripts and the TestILC folder if using .NET Native.

#### [`CreateTestListJson`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L256)

Generates JSON files specifying information used when running the tests in Helix.

#### [`UploadContent`](https://github.com/dotnet/buildtools/blob/87422f6cb8/src/Microsoft.DotNet.Build.CloudTestTasks/PackageFiles/CloudTest.targets#L408)

Uploads the test archives and other required files to Azure, and then submits a Helix job which downloads the archives and runs the tests.