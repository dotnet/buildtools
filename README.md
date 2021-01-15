# .NET Core Build Tools

This repo has been replaced by [Arcade](https://github.com/dotnet/arcade). It's still used for servicing older releases of .NET Core but new code shouldn't use it. As a result, we dont accept new PRs in this code base.

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
[Microsoft.DotNet.BuildTools]: https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=myget-legacy&package=Microsoft.DotNet.BuildTools&protocolType=NuGet
[sn-sign]: https://github.com/dotnet/corefx/wiki/Strong%20Naming
