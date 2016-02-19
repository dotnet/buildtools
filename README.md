# .NET Core Build Tools

[![Build status](http://dotnet-ci.cloudapp.net/job/dotnet_buildtools/job/master/job/innerloop/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_buildtools/job/innerloop/)

This repository contains supporting build tools that are necessary for building
the [.NET Core][dotnet-corefx] projects. These projects consume the build tools
via the corresponding [Microsoft.DotNet.BuildTools][Microsoft.DotNet.BuildTools]
NuGet package.

The build tools are MSBuild `.targets` and tasks. These extend the build process
to provide additional functionality, such as producing version information and
performing [strong name signing][sn-sign].

**Note:** Please note that these tools are currently not meant for consumption
outside of the .NET Core projects.

[dotnet-corefx]: https://github.com/dotnet/corefx
[Microsoft.DotNet.BuildTools]: http://nuget.org/packages/Microsoft.DotNet.BuildTools
[sn-sign]: https://github.com/dotnet/corefx/wiki/Strong%20Naming
