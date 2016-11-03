# Project NuGet Dependencies

## Dependency version verification

The dependencies in each project.json file are validated by a few rules in `dependencies.props` to ensure package versions across the repository stay in sync. Dependencies are normally verified before the NuGet restore step, but to manually verify run the `VerifyDependencies` MSBuild target.

Errors from failed dependency version validation are like the following:

    C:\git\corefx\Tools\VersionTools.targets(47,5): error : Dependency verification errors detected. To automatically fix based on dependency rules, run the msbuild target 'UpdateDependencies' [C:\git\corefx\build.proj]
    C:\git\corefx\Tools\VersionTools.targets(47,5): error : Dependencies invalid: In 'C:\git\corefx\src\Common\test-runtime\project.json', 'Microsoft.DotNet.BuildTools.TestSuite 1.0.0-prerelease-00704-04' must be '1.0.0-prerelease-00704-05' (Microsoft.DotNet.BuildTools.TestSuite) [C:\git\corefx\build.proj]
    C:\git\corefx\Tools\VersionTools.targets(47,5): error : Dependencies invalid: In 'C:\git\corefx\src\Common\tests\project.json', 'Microsoft.xunit.netcore.extensions 1.0.0-prerelease-00704-04' must be '1.0.0-prerelease-00704-05' (Microsoft.xunit.netcore.extensions) [C:\git\corefx\build.proj]

To automatically fix these by setting the versions to expected values, use the `UpdateDependencies` target. `UpdateDependencies` can also be used to automatically update package versions, described in the next section.

If an expected value looks wrong, edit `dependencies.props` to change it. See [annotated-dependency-props.md](annotated-dependency-props.md) for an explanation of `dependencies.props`.


## Upgrading a package dependency

To update a package that isn't validated by a rule, simply change the project.json file.

Otherwise, follow these steps:

 1. Edit `dependencies.props` to match the new expectations. See [annotated-dependency-props.md](annotated-dependency-props.md).
 2. Run the dependency update target in the repository root. In corefx, use this command:

        build-managed.cmd -- /t:UpdateDependencies

    Other repositories have slightly different ways to run targets.

 3. Commit the automated updates in an independent commit, isolating them from other changes. This makes pull requests easier to review because updates can change many files.

The `UpdateDependencies` target looks through all dependencies, using the validation rules to update any invalid versions. On `/verbosity:Normal` or higher, it logs which files were changed.


## Dependency auto-update

Dependency auto-update uses the dependency update/verify system to submit automated pull requests for package updates. See [dependency-auto-update.md](dependency-auto-update.md) for details.
