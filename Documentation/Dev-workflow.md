Dev Workflow
===============
The dev workflow describes the development process to follow. It is divided into specific tasks that are fast, transparent and easy to understand.

This provides flexibility as with discrete tasks people can iterate on a specific one without needing to run the full workflow, other processes can reuse the tasks as needed and anyone can put together the tasks as wanted to create a customized workflow.

## Process 
![Dev Workflow process](images/Dev-workflow.jpg)

## Tasks

**Setup**

Set of instructions in order to have an environment ready to build de repo. It is repo specific.

**Clean**

The clean task is responsible of cleaning and leaving the developer enlistment/environment in a state as closest as the repository/code server.

**Sync**

The sync task gets the latest source history and the dependencies the build needs like for example restore the NuGet packages. Sync is the task in charge of eliminating all the network traffic when we build. This way we are able to hit the network only when we are intentional about doing it and then be able to build even in offline mode.

**Build**

Builds the source code. The order of how we build depends on each repo or the situation.

* Build product binaries: Builds the product binaries without need of hitting the network to restore packages. It doesn't build tests, run tests or builds packages.
* Build packages: Builds the NuGet packages from the binaries that were built in the Build product binaries step.
* Build tests: Builds the tests that are in the repository. It could also mean to queue a build in Helix.

**Run Tests**

Runs the tests that were built, either in the repository or in Helix.

**Publish Packages**

Publishes the NuGet packages that were built in the previous steps to an specified location.
