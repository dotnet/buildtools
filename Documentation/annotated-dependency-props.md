# Annotated dependencies.props

This file is used in CoreFX, CoreCLR, WCF, and BuildTools, located in the repository root. Below is a breakdown of [corefx's dependencies.props](https://github.com/dotnet/corefx/blob/b57a43bb40fc2099e91d641a8b4f8c76a46afe6a/dependencies.props). It is used for dependency auto-upgrade and dependency verification.

``` xml
<PropertyGroup>
	<CoreFxCurrentRef>450606241ffd24c3c9671cd002955a68e98008a7</CoreFxCurrentRef>
	<CoreClrCurrentRef>450606241ffd24c3c9671cd002955a68e98008a7</CoreClrCurrentRef>
	<ExternalCurrentRef>0db1f9d8996a6a05960f79712299652a4b04147f</ExternalCurrentRef>
	<ProjectNTfsCurrentRef>450606241ffd24c3c9671cd002955a68e98008a7</ProjectNTfsCurrentRef>
</PropertyGroup>
```

Source of truth for dependency tooling: the commit hash of the dotnet/versions main branch as of the last auto-upgrade. These are used with the GitHub raw api to download build-infos.

In `/t:UpdateDependenciesAndSubmitPullRequest`, the task first finds the latest CurrentRef of the dotnet/versions repository, which when used with the raw API will return the latest version of *every* build info. The update proceeds, using that latest build-info data. After updating, the task determines which build-infos were used and updates only the used build-info `*CurrentRef`s in the above part of dependencies.props.

When doing a manual update with `/t:UpdateDependencies`, you need to change these `CurrentRef`s yourself to whatever dotnet/versions commit you want to update to before executing the target.

During dependency verification (`/t:VerifyDependencies` or automatically during `sync`) the targeted build-infos are downloaded and project files are checked for consistency with the build-infos.

``` xml
<!-- Auto-upgraded properties for each build info dependency. -->
<PropertyGroup>
	<CoreFxExpectedPrerelease>beta-24601-02</CoreFxExpectedPrerelease>
	<CoreClrExpectedPrerelease>beta-24603-02</CoreClrExpectedPrerelease>
	<ExternalExpectedPrerelease>beta-24523-00</ExternalExpectedPrerelease>
	<ProjectNTfsExpectedPrerelease>beta-24603-00</ProjectNTfsExpectedPrerelease>
</PropertyGroup>
```

These are auto-updated by `UpdateDependenciesAndSubmitPullRequest` and `UpdateDependencies`, with values taken from `Latest.txt`. They are only used to flow this info into MSBuild, *not* by project.json validation (as they were in older types of dependency auto-update).

These properties are verified to match the downloaded build-info during `VerifyDependencies`.

``` xml
<!-- Full package version strings that are used in other parts of the build. -->
<PropertyGroup>
	<AppXRunnerVersion>1.0.3-prerelease-00826-05</AppXRunnerVersion>
	<XunitPerfAnalysisPackageVersion>1.0.0-alpha-build0040</XunitPerfAnalysisPackageVersion>
</PropertyGroup>
```

Similar to the `*ExpectedPrerelease` properties, but these are for specific named packages. They are used in MSBuild targets during builds. These versions in particular aren't auto-updated because they are tools that don't regularly change.

``` xml
<!-- Package dependency verification/auto-upgrade configuration. -->
<PropertyGroup>
	<BaseDotNetBuildInfo>build-info/dotnet/</BaseDotNetBuildInfo>
	<DependencyBranch>main</DependencyBranch>
	<CurrentRefXmlPath>$(MSBuildThisFileFullPath)</CurrentRefXmlPath>
</PropertyGroup>
```

The first two properties assemble the path to the build-info files that CoreFX master depends on.

`CurrentRefXmlPath` is used by the auto-update targets to determine where `dependencies.props` is.

``` xml
<ItemGroup>
	<RemoteDependencyBuildInfo Include="CoreFx">
		<BuildInfoPath>$(BaseDotNetBuildInfo)corefx/$(DependencyBranch)</BuildInfoPath>
		<CurrentRef>$(CoreFxCurrentRef)</CurrentRef>
	</RemoteDependencyBuildInfo>
	<RemoteDependencyBuildInfo Include="CoreClr">
		<BuildInfoPath>$(BaseDotNetBuildInfo)coreclr/$(DependencyBranch)</BuildInfoPath>
		<CurrentRef>$(CoreClrCurrentRef)</CurrentRef>
	</RemoteDependencyBuildInfo>
	<RemoteDependencyBuildInfo Include="External">
		<BuildInfoPath>$(BaseDotNetBuildInfo)projectk-tfs/$(DependencyBranch)</BuildInfoPath>
		<CurrentRef>$(ExternalCurrentRef)</CurrentRef>
	</RemoteDependencyBuildInfo>
	<RemoteDependencyBuildInfo Include="ProjectNTfs">
		<BuildInfoPath>$(BaseDotNetBuildInfo)projectn-tfs/$(DependencyBranch)</BuildInfoPath>
		<CurrentRef>$(ProjectNTfsCurrentRef)</CurrentRef>
	</RemoteDependencyBuildInfo>

	<DependencyBuildInfo Include="@(RemoteDependencyBuildInfo)">
		<RawVersionsBaseUrl>https://raw.githubusercontent.com/dotnet/versions</RawVersionsBaseUrl>
	</DependencyBuildInfo>
	
	<XmlUpdateStep Include="CoreFx">
		<Path>$(MSBuildThisFileFullPath)</Path>
		<ElementName>CoreFxExpectedPrerelease</ElementName>
		<BuildInfoName>CoreFx</BuildInfoName>
	</XmlUpdateStep>
	<XmlUpdateStep Include="CoreClr">
		<Path>$(MSBuildThisFileFullPath)</Path>
		<ElementName>CoreClrExpectedPrerelease</ElementName>
		<BuildInfoName>CoreClr</BuildInfoName>
	</XmlUpdateStep>
	<XmlUpdateStep Include="External">
		<Path>$(MSBuildThisFileFullPath)</Path>
		<ElementName>ExternalExpectedPrerelease</ElementName>
		<BuildInfoName>External</BuildInfoName>
	</XmlUpdateStep>
	<XmlUpdateStep Include="ProjectNTfs">
		<Path>$(MSBuildThisFileFullPath)</Path>
		<ElementName>ProjectNTfsExpectedPrerelease</ElementName>
		<BuildInfoName>ProjectNTfs</BuildInfoName>
	</XmlUpdateStep>
</ItemGroup>
```

Each `RemoteDependencyBuildInfo` indicates a build-info to download, which consists of the `Latest.txt` and `Latest_Packages.txt` at a certain path. `CurrentRef` is flowed from the property into the item metadata rather than hard-coded in the metadata for enhanced visibility in auto-update diffs.

The `XmlUpdateStep`s are rules that match the `*ExpectedPrerelease` properties earlier in this file and link them to the build infos.

``` xml
<!-- Set up dependencies on packages that aren't found in a BuildInfo. -->
<ItemGroup>
	<TargetingPackDependency Include="Microsoft.TargetingPack.NETFramework.v4.5" />
	<TargetingPackDependency Include="Microsoft.TargetingPack.NETFramework.v4.5.1" />
	<TargetingPackDependency Include="Microsoft.TargetingPack.NETFramework.v4.5.2" />
	<TargetingPackDependency Include="Microsoft.TargetingPack.NETFramework.v4.6" />
	<TargetingPackDependency Include="Microsoft.TargetingPack.NETFramework.v4.6.1" />
	<TargetingPackDependency Include="Microsoft.TargetingPack.NETFramework.v4.6.2" />
	<TargetingPackDependency Include="Microsoft.TargetingPack.Private.WinRT" />
	<StaticDependency Include="@(TargetingPackDependency)">
		<Version>1.0.1</Version>
	</StaticDependency>

	<XUnitDependency Include="xunit"/>
	<XUnitDependency Include="xunit.runner.utility"/>
	<XUnitDependency Include="xunit.runner.console"/>
	<StaticDependency Include="@(XUnitDependency)">
		<Version>2.1.0</Version>
	</StaticDependency>

	<StaticDependency Include="Microsoft.xunit.netcore.extensions;Microsoft.DotNet.BuildTools.TestSuite">
		<Version>1.0.0-prerelease-00830-02</Version>
	</StaticDependency>

	<PerformancePackDependency Include="Microsoft.DotNet.xunit.performance" />
	<PerformancePackDependency Include="Microsoft.DotNet.xunit.performance.analysis" />
	<PerformancePackDependency Include="Microsoft.DotNet.xunit.performance.analysis.cli" />
	<PerformancePackDependency Include="Microsoft.DotNet.xunit.performance.runner.cli" />
	<PerformancePackDependency Include="Microsoft.DotNet.xunit.performance.runner.Windows" />
	<StaticDependency Include="@(PerformancePackDependency)">
		<Version>$(XunitPerfAnalysisPackageVersion)</Version>
	</StaticDependency>

	<DependencyBuildInfo Include="@(StaticDependency)">
		<PackageId>%(Identity)</PackageId>
		<UpdateStableVersions>true</UpdateStableVersions>
	</DependencyBuildInfo>

	<DependencyBuildInfo Include="uwpRunnerVersion">
		<PackageId>microsoft.xunit.runner.uwp</PackageId>
		<Version>$(AppXRunnerVersion)</Version>
	</DependencyBuildInfo>

</ItemGroup>
```

These are "local" `DependencyBuildInfo`s created to cover packages that aren't in downloaded build-infos because they are external, not published to dotnet/versions, or don't normally change. Specifically, these allow the package versions to be validated in `project.json` files.
