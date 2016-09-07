# Dependency auto-update

**TODO**: How auto-update happens without the repo API.

# Auto-update using the Repo API

The Repo API Compose commands (see [the proposed buildtools implementation](buildtools-repo-compose.md)) are used by the auto-update flow in the same way that a dev could use them. A full cycle, from the end of an official build to update PRs being posted to dependent repositories, is:

1. The build publishes its output (packages, installers, etc.).
2. `produces` and `consumes` are run and their output captured.
3. The `produces` and `consumes` outputs are published on the dotnet/versions repository, representing the state of the latest official build. (See [#986](https://github.com/dotnet/buildtools/issues/986))
4. [Maestro][Maestro] notices the change and triggers the auto-update pull request generation VSTS build for each dependent repository:
   1. `consumes` is run.
   2. The `consumes` output is compared against the `produces` output found on dotnet/versions. Dependencies that have a new version available are changed in the `consumes` output.
      * Some per-repository metadata is necessary for any unusual upgrade behaviors we need to follow. See [Configuring Repo API auto-update](#configuring-repo-api-auto-update)
   3. The modified `consumes` output is passed to `change`.
   4. The changes that `change` makes to the dependent repository are committed and pushed.
   5. A new PR is opened for the pushed branch, but if there was an existing auto-upgrade PR, that is updated to include the new change.


## Configuring Repo API auto-update

An additional file maps `produces` outputs from dotnet/versions to update locations in the `consumes` data. For example, the coreclr build-info is mapped to update floating dependencies in corefx.


[Maestro]: https://github.com/dotnet/versions/tree/master/Maestro
