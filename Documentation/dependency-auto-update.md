# Dependency auto-update

Buildtools provides tooling for automatically updating a repository's dependencies (project.json, msbuild .props, and arbitrary versions in files). Combined with [Maestro][Maestro], this allows a flow where one repository finishes an official build, and all repositories that depend on that repository get an automatically generated pull request from a bot GitHub account.

As of writing, the Repo API Compose commands are not implemented yet. This document describes auto-update in a pre-Repo-API world.


## Parts of auto-update flow

The [versions repo (dotnet/versions)][dotnet/versions] stores information about the last official build of each orchestrated repository in text files.

[Maestro][Maestro] detects changes to files in GitHub and kicks off tasks depending on the file that changed, according to [subscriptions.json][subscriptions]. For dependency auto-update, the relevent files are the build-info files in dotnet/versions.

VSTS builds are used to execute the auto-update pull request generation task. The naming scheme for these build definitions for CoreFX, CoreCLR, and WCF is `Maestro-<Project>GeneralExecutor`.

[VersionTools][VersionTools] is a library that can update and verify dependencies, commit and push changes, and make pull requests. The main BuildTools package includes "wrapper" targets/tasks that run this library, and the library's DLL.


## Auto-update in action

For example, the flow that updated upgrade PR [coreclr/pull/7472](https://github.com/dotnet/coreclr/pull/7472) with CoreFX master beta-24604-02:

 1. The official build writes its output data to the dotnet/versions [build-info/dotnet/corefx/master](https://github.com/dotnet/versions/tree/0e83ecde3e99f5c13b9532e3b06003b85b83f435/build-info/dotnet/corefx/master).
   1. Latest.txt stores the prerelease specifier for all packages published.
   2. Latest_Packages.txt stores the full id and version identifier for each package published.
 2. [Maestro][Maestro] detects the build-info commit and the subscription triggers [Maestro-CoreCLRGeneralExecutor #359499](https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_build/index?buildId=359499).
 3. The VSTS build runs `/t:UpdateDependenciesAndSubmitPullRequest /p:GitHubUser=dotnet-bot /p:GitHubEmail=dotnet-bot@microsoft.com /p:GitHubAuthToken=******** /p:ProjectRepoOwner=dotnet /p:ProjectRepoName=coreclr /p:ProjectRepoBranch=main /p:NotifyGitHubUsers=dotnet/coreclr-contrib`.
   1. The latest build-info Latest_Packages.txt is downloaded and used to update project.jsons and msbuild files.
   2. The build searches for an auto-update PR that already exists for the branch and finds [coreclr/pull/7472](https://github.com/dotnet/coreclr/pull/7472).
   3. Auto-update PRs are reused when possible, so changes are committed and pushed (`--force`) to the branch for PR 7472.
   4. The build updates the title of PR 7472 to indicate the update that took place. If no auto-update PR had existed to update, it would create a fresh PR instead.
 4. CI runs, and when it's green, project maintainers can merge the auto-update PR when convenient.


## Subscription

A subscription is simply the path that [Maestro][Maestro] should listen to, and what build to run when it detects a change. These are defined in [subscriptions.json][subscriptions]. For example, a subscription to create auto-update PRs in WCF for the latest CoreFX build, (comments mine):

```
{
	"path": "https://github.com/dotnet/versions/blob/main/build-info/dotnet/corefx/master/Latest.txt",
	"handlers": [
		{
			// The build definition to run, a key to a dict elsewhere in subscriptions.json.
			"maestroAction": "wcf-general", 
			// A delay after the change is detected to allow published artifacts to propagate.
			"maestroDelay": "00:10:00",
			
			// A queue-time variable sent to the build definition. No special Maestro handling.
			// The definition runs the named script with the args given in the "Arguments" variable.
			"ScriptFileName": "build-managed.cmd",
			// Maestro concatenates the list of arguments and passes them at queue time as a string.
			"Arguments": [
				"--",
				"/t:UpdateDependenciesAndSubmitPullRequest",
				"/p:GitHubUser=dotnet-bot",
				"/p:GitHubEmail=dotnet-bot@microsoft.com",
				"/p:GitHubAuthToken=`$(`$Secrets['DotNetBotGitHubPassword'])",
				"/p:ProjectRepoOwner=dotnet",
				"/p:ProjectRepoName=wcf",
				"/p:ProjectRepoBranch=main",
				"/p:NotifyGitHubUsers=dotnet/wcf-contrib",
				"/verbosity:Normal"
			]
		}
	]
}
```

([Link to subscription in context.](https://github.com/dotnet/versions/blob/0e83ecde3e99f5c13b9532e3b06003b85b83f435/Maestro/subscriptions.json#L146-L163))

`wcf-general` is [defined as](https://github.com/dotnet/versions/blob/0e83ecde3e99f5c13b9532e3b06003b85b83f435/Maestro/subscriptions.json#L51-L56) this, pointing to the definition that should be queued:

```
"wcf-general": {
	"vsoInstance": "devdiv.visualstudio.com",
	"vsoProject": "DevDiv",
	"buildDefinitionId": 4226
}
```

### MSBuild arguments

 * `/t:UpdateDependenciesAndSubmitPullRequest`: the target that invokes [VersionTools][VersionTools].
 * `/p:GitHubUser=dotnet-bot`: the fork owner and committer name.
 * `/p:GitHubEmail=dotnet-bot@microsoft.com`: committer email.
 * `/p:GitHubAuthToken=`$(`$Secrets['DotNetBotGitHubPassword'])`: fork owner token. The build definition has this as a secret variable, and the PowerShell in this string finds the secret's value and passes it to msbuild.
 * `/p:ProjectRepoOwner=dotnet`: the owner of the repo to submit the PR to.
 * `/p:ProjectRepoName=wcf`: the name of the repo to submit the PR to.
 * `/p:ProjectRepoBranch=main`: the base branch to PR against.
 * `/p:NotifyGitHubUsers=dotnet/wcf-contrib`: a user/group to `@`-mention in the PR description. In theory a semicolon-delimited list, but the layers of JSON and PowerShell escaping make it difficult so subscriptions have only specified a single user/group so far.


## dependencies.props

For an in-depth look at `dependencies.props`, which stores the current dependencies and configures auto-update's specific behavior for a repository, read [annotated-dependency-props.md](annotated-dependency-props.md).


# Adding or removing a subscription

In general, submit a PR for changes to [subscriptions.json][subscriptions].

When adding a subscription to a new branch, if there isn't an existing "non-main" subscription to examine as an example, note that the `vsoSourceBranch` property (sibling of `maestroAction` and `maestroDelay`) tells Maestro which branch to run the GeneralExecutor on.

When adding an entirely new project, a new build definition is also needed. Look at the `Maestro-*GeneralExecutor` definitions as examples and, if possible, clone an existing one and change the repository pointer in VSTS. Add a corresponding entry to the `actions` object in subscriptions.json.

To remove a subscription, delete the entry in the `handlers` array.

When changing subscriptions.json, match the existing comment style as best as possible, because it makes searching the file more consistent.


[Maestro]: https://github.com/dotnet/versions/tree/main/Maestro
[dotnet/versions]: https://github.com/dotnet/versions
[subscriptions]: https://github.com/dotnet/versions/blob/main/Maestro/subscriptions.json
[VersionTools]: https://github.com/dotnet/buildtools/tree/094e239b8c1e7f1495abf8c7dc96c71e56bf6c96/src/Microsoft.DotNet.VersionTools
