# Composing our repos

In order to have automated builds that compose the output of our ever growing number of repos we need to get more structured data about their relationships.  Today repos provide nothing in the way of programmatic access to dependencies.  All of the information is known by the owners and as a result the builds and composition is fully manual. 

Going forward repos will be given contracts, or APIs, that provide structured data about their dependencies and outputs.  This will give us the necessary data to compose our repos in our growing set of scenarios:

- Linux distro builds from source
- Official Windows signed build
- Builds to quickly test and deploy new packages across repos

These contracts will be included in the `run` command [specification](RunCommand.md).  This document in particular will be discussing the commands which allow us to compose the outputs of our repos.

## Commands

This document describes the commands used to compose different repos together.  There is a larger set of commands including `build`, `sync` and `clean` which is [described separately](Dev-workflow.md). 

### consumes

The `consumes` command returns json output that describes all of the external artifacts consumed by this repo.  This includes NuGet feeds, packages and arbitrary files from the web or file system.  The format of all these items is describe in the Artifact Specification section below. 

The artifacts are grouped into sections: 

- Build Dependencies: artifacts which are referenced by the build output and include NuGet packages, MSI, etc ...  In order to support a composed build these are further divided into floating and static dependencies:
    - Floating: dependencies where versions can change as a part of a composed build via the `change` command.  This commonly includes CoreFx, CoreClr, etc ...  
    - Static: dependencies whose version do not change via the `change` command.  This is commonly used for SDK binaries, legacy tools, etc ...  
- Toolset Dependencies: artifacts used in the production of build output.  This commonly includes NuGet.exe, compilers, etc ... 

These sections are identified respectively by the following JSON sections:

``` json
"dependencies": {
    "floating": { 
        // Build artifacts
    },
    "static": { 
        // Static artifacts
    },
    "toolset": { 
        // Toolset artifacts
    }
}
```

The data in the output is further grouped by operating system.  Builds of the same repo on different operating system can reasonably consume a different set of resources.  The output of `consumes` reflect this and allows per operating system dependencies:

``` json
{
    "os-windows": {
        "dependencies": { } 
    },
    "os-linux": {
        "dependencies": { }
    }
}
```

In the case the `consumes` output doesn't want to take advantage of OS specific dependencies it can specify `"os-all"` as a catch all. 

In addition to artifacts the consume feed can also optionally list any machine prerequitsites needed to build or test the repo:

``` json
"prereq": { 
    "Microsoft Visual Studio": "2015",
    "CMake" : "1.0",
}
```

A full sample output for `consumes` is available in the Samples section.

### produces

The `produces` command returns json output which describes the artifacts produced by the the repo.  This includes NuGet packages and file artifacts.  

The output format for artifacts is special for `produces` because it lacks any hard location information.  For example: 

- NuGet artifacts lack feeds
- File artifacts lack a `"kind"` and supporting values

This is because the `produces` command represents "what" a repo produces, not "where" the repo produces it.  The "where" portion is controled by the `publish` command.  External components will take the output of `produces`, add the location information in and feed it back to `publish`.  

Like `consumes` the `produces` output is also grouped by the operating system:

``` json
{
    "os-windows": {
        "nuget": { },
        "file": { }
    },
    "os-linux": {
        "nuget": { },
        "file": { }
    }
}
```

A ful sample output for `produces` is available in the Samples section.

### change

The `change` command is used to alter the floating build dependencies section.  It can establish new versions of NuGet packages, new locations to find file artifacts, different NuGet feeds, etc ...  

This is the command which allow us to use the build output of one repo as the input of a dependent repo.  The first repo can build using a new output version (say beta5) and dependent repos can be changed to accept this new version.  

This command operates by providing json whose format is a subset of the output of `consumes`.  In particular it will provide the `"dependencies.floating"` section.  

``` json
"dependencies" {
    "floating": { 
        "nuget": {
            "packages" {
                "MicroBuild.Core": "0.2.0",
                "Microsoft.NETCore.Platforms": "1.0.1"
            }
        }
    }
}
```

A tool responsible for composing repos would ideally:

1. Execute `run.cmd consumes` and capture the output.
2. Alter the NuGet package versions in the json to have the correct build identifier: beta5, RTM, etc ..
3. Execute `run.cmd change` and pass in the altered output.


### publish

The `publish` command takes a json input that describes the locations artifacts should be published to.  The input to this command is the output of `produces` that is augmented with location information.  

## Artifact Specification

The json describing artifacts is the same between the `consume`, `produces` and `publish` commands.  These items can be used anywhere artifacts are listed above.

### NuGet packages

The description for NuGet artifacts is it two parts:

1. The set of feeds packages are being read from.
2. The set of packages that are being consumed and their respective versions.

Example:

``` json
"nuget": {
    "feeds": [
        { 
           "name": "core-clr",
           "value": "https://dotnet.myget.org/F/dotnet-coreclr/api/v3/index.json" 
        },
        {
            "name": "dotnet-core",
            "value": "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json"
        }
    ],
    "packages": {
        "MicroBuild.Core": "0.2.0",
        "Microsoft.NETCore.Platforms": "1.0.1"
    }
}
```

### File 

Any file which is not a NuGet package should be listed as a file artifact.  These can be downloaded from the web or copied from local places on the hard drive.  Each type of file entry will have a name uniquely identifying the artifact and a kind property specifying the remainder of the properties:

    - uri: a property named `"uri"` will contain an absolute Uri for the artifact.
    - filesystem: a property named `"location"` will contain an OS specific file path for the artifact.

Example: 

``` json
"file": {
    "nuget.exe":  {
        "kind": "uri",
        "uri": "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
    }, 
    "run.exe": { 
        "kind": "filesystem",
        "location": "c:\\tools\\run.exe"
    }
}
```

## Samples

### consumes

``` json
{
    "os-all": {
        "dependencies": {
            "floating": { 
                 "nuget": {
                    "feeds": [
                        { 
                           "name": "core-clr",
                           "value": "https://dotnet.myget.org/F/dotnet-coreclr/api/v3/index.json" 
                        },
                        {
                            "name": "dotnet-core",
                            "value": "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json"
                        }
                    ],
                    "packages" {
                        "MicroBuild.Core": "0.2.0",
                        "Microsoft.NETCore.Platforms": "1.0.1"
                    }
                },
                "file": {
                    "nuget.exe":  {
                        "kind": "url",
                        "uri": "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
                    }
                }
            },
            "toolset": { 
            },
            "static": { 
            }
        },
        "prereq": { 
            "Microsoft Visual Studio": "2015",
            "CMake" : "1.0"
        } 
    }
}
```

### produces

``` json
{
    "os-all": {
        "nuget": {
            "packages" {
                "MicroBuild.Core": "0.2.0",
                "Microsoft.NETCore.Platforms": "1.0.1"
            }
        },
        "file": {
            "nuget.exe": { } 
        }
    }
}
```

## Implementation Stages

Fully implementing the commands described in this document requires a decent amount of work.  The expectation is that it will be implemented in stages.  Each stage provides benefit to an existing tool or allows us to write a new tool for a major scenario:

- Stage 1: NuGet build information.  NuGet packages are how the majority of repo artifacts are shared today.  Getting the NuGet section of the above commands implemented allows us to automate the majority of our composed builds.  This is really just a formalization of the commands repos already implement as a part of being inside the Maestro tool.  
- Stage 2: Remainder of artifacts:  Fill out the build and toolset sections with all of the other file / NuGet dependencies.  This output will allow us to fully understand the tools required for composed builds.  
- Stage 3: Pre-req information.  Having correct prereq information will allow us to understand how to create and provision build machines.
- Stage 4: Change command for all artifacts.  The ability to change a dependency from downloading nuget.exe from nuget.org to a place on the local file system.  This will give us the ability to implement fully offline builds.  

## FAQ

### How would a composer tool match the inputs / outputs of repos?

The goal of this effort is to allow a composer tool to examine an arbitrary set of repos and establish a build order.  It can do so by examining the output of `consumes` and `produces` for each repo and establishing a dependency graph based on the outputs.  Artifacts from `produces` can be linked to floating build dependencies in `consumes`.

But there is no guarantee that at any given point in time that the outputs of one repo will match exactly with the inputs of a depndent repo.  The names are likely to match but not the versions.  For example what is the chance for any given commit that dotnet/corefx is outputing System.Collections.Immutable at the exact version that dotnet/roslyn is cosuming for a given commit?  Probably fairly unlikely and getting them to agree requires coordination which is expensive and intended to be avoided by this very design.

This is not an issue though because composers should not consider version information when building a dependency graph.  Versions of packages, both output from `publish` and built against via `consumes` can be controlled.  The `change` command is used to alter floating build dependencies and `publish` takes version information as an input.  This means version information is completely controlled by the composition process. 

Hence when establishing dependency graphs a composer should link repo artifacts based on their name only.

### Why can't we use project.json + NuGet.config

At a glance it appears that much of the information described here is simply the contents of NuGet.config and the project.json in the repo.  That is possibly true for small repos.  For repos of significant size and dependencies more structure is needed to describe the intent of a given project.json in the repo.

For example at the time of this writing [dotnet/roslyn](https://github.com/dotnet/roslyn) contains 40+ project.json files.  It's not possible to know which of these represent build dependencies, tooling or static dependencies.  The repo has to provide a mechanism to discover this.

### This looks a lot like a package manager. 

Indeed it does. If there is an existing package manager specification which meets our needs here I'm happy to see if we can leverage it.  

### Your samples have comments in JSON.  That's not legal.

Yes they do.  It's a sample :smile:

## Open Issues

There are a series of open issues we need to track down here.

- What is the full set of operating system identifiers?  The set I have listed above is just a place holder and was given no real thought.  More information is needed here. 
- How can we relate file names between repos.  It's easy for us to understand that Microsoft.CodeAnalysis.nupkg is the same between repos.  How do we know that a repo which produces core-setup.msi is the input for a repo that consumes core-setup.msi?  Perhaps we have to say that output file identifiers must be unique across repos?  That seems like the simplest approach.
- Can the `change` command also be used to alter `toolset` versions? 
- Should the NuGet feed be separated from the packages?  Probably should be an entirely different section
