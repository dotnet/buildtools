# Implementing [Repo API](RepoCompose.md) compose commands in buildtools

## Why not RepoUtil?

[RepoUtil][RepoUtil] derives `consumes` and `change` behavior from the contents of project.json files in a project, considering them the *source of truth* for dependency information. In some repositories, dependency information is solely stored in project.json files. When adding Repo API support in those repositories, it makes sense to use RepoUtil as-is.

However, repositories that currently use buildtools tend to store some dependency information in other files, for example package versions used in MSBuild project files. There is already a *source of truth* independent of project.jsons in these repositories: [dependencies.props](https://github.com/dotnet/corefx/blob/713e104c72538f5b42c75882a337178ad8e4229b/dependencies.props). This doc assumes that buildtools-using repositories will want to continue using a non-project.json source of truth. Repositories that use buildtools are corefx, coreclr, wcf, and buildtools. (The current plan is to also convert core-setup to use buildtools, but that project doesn't have a dependencies.props source of truth as of writing. See [core-setup #283](https://github.com/dotnet/core-setup/issues/283).)

This doc proposes the way that Repo API Compose commands will be implemented in BuildTools, with a non-project.json source of truth.


## Overview

The way `consumes`, `change`, and `verify` interact make it so they can be simplified by checking the `consumes` output into the repo as "consumes.json" and using it as the source of truth.

 * `consumes` reads the contents of consumes.json and writes it out verbatim.

 * `change` takes a modified consumes.json and overwrites the checked-in consumes.json file. Then, it changes the project contents so they match the new source of truth.

 * `verify` reads consumes.json and ensures its contents are self-consistent and match the repository contents. This allows devs to check if their project.json or other modifications are valid.
   * This is not a documented Repo API Compose command, but [RepoUtil][RepoUtil] offers it and the existing buildtools target `/t:VerifyDependencies` is an analogue.

The `produces` command scans artifacts of a completed build for files that the repository produced and intends to publish. More detailed documentation will be available in BuildToolsRepoApiProducesImplementation.md [~~link~~](BuildToolsRepoApiProducesImplementation.md).

The Repo API Compose command document [describes the workflow to update a version](RepoCompose.md#change), but to go into more detail, the corresponding flow in the Buildtools implementation is:

 1. `run consumes > temp.json`
 2. Open the temp file in an editor, change the versions as desired.
 3. `run change < temp.json`

That flow leaves the temporary file for manual cleanup. If that isn't desired:

 1. Edit "consumes.json".
 2. `run change -InPlace`

The Repo API spec doesn't require a file that represents `consumes` output, so the stdin/stdout flow is standard.


## Mechanics of a `change`

A `change` scenario typically has three steps. This is what a dev (or automation process) would do to change dependencies in a project:

 1. Execute `consumes` and capture the output json.
 2. Change the versions in the output json to the desired dependency state.
 3. Execute `change` and pass in the altered output json.

In the last step, the `change` implementation makes any source file changes needed to ensure:

 * The next time `consumes` is called, it will output the same json that was passed to `change`.
 * The dependencies specified by `consumes` are the exact ones being consumed by the repository.

The proposed way that a buildtools-using repository would ensure these properties is:

 * Replace the checked-in "consumes.json" file with the provided "consumes.json". This changes the `consumes` output to be correct.
 * Using a set of repository-specific rules, enforce the dependencies and versions defined in the provided consumes data.
   * To reuse the most possible code, this uses the msbuild configuration and targets already created to do this using rules defined in "dependencies.props". The targets need to be modified to allow "consumes.json" as the source of truth.


## Edge cases of `change`

### Upgrading from a stable dependency
In CoreFX, some projects depend on other projects within the repository. When possible, these dependencies are set to the lowest-possible stable version, and should never auto-update. However, when one project needs to consume a breaking change from another, the dependency is set to a prerelease version and auto-updated from then on.

So far, this has been dealt with by the rule "if a dependency is stable, don't auto-update it; if it's a prerelease dependency, auto-update it."

This causes problems after a release. When building release candidate packages that have stable versions, *all* dependencies in the release branch source are auto-updated to stable versions and no longer auto-update. This means that when a servicing change or similar is building after a stable version has been released from that git branch, all applicable dependency versions must be updated manually. It's unclear how to distinguish between stable dependencies not to upgrade vs. stable dependencies to upgrade.

This situation is something to address in the future if we can think of a reliable update strategy.

### Adding/removing a dependency
With the set of Repo API Compose commands above, when a dev adds or removes a dependency, they need to make the change to the entire source tree at the same time, including the checked-in "consumes.json". `verify` can be used to check if the add/remove was done correctly, but that's only partially helpful.

If this is worthwhile to solve, we could add a new command (or an option for `change`) that finds discrepancies in the source tree and tries to modify "consumes.json" to match. This could work for simple removes and adds. The new mode would be a reversal of the usual `change` flow of *source of truth* to project, flowing from the project state to the source of truth instead.


## Using RepoUtil to seed `consumes` output.

When adding the Repo API Compose commands to a buildtools-using repository, the repository needs an initial accurate consumes.json file. To get the initial list of everything a repository consumes, we can use [RepoUtil][RepoUtil] to generate an initial source of truth from project.json files with the full set of dependencies.

Most of this information can be derived from a "dependencies.props" file, but it isn't complete because in that system, unregulated dependencies are allowed.


[RepoUtil]: ../src/RepoUtil/README.md
