# .NET Core Build Tools

### Windows
[![Build Status](https://ci.dot.net/job/dotnet_buildtools/job/master/job/Windows_NT/badge/icon)](https://ci.dot.net/job/dotnet_buildtools/job/master/job/Windows_NT/)

### Ubuntu 14.04
[![Build Status](https://ci.dot.net/job/dotnet_buildtools/job/master/job/Ubuntu14.04/badge/icon)](https://ci.dot.net/job/dotnet_buildtools/job/master/job/Ubuntu14.04/)

[![Packages](https://img.shields.io/dotnet.myget/dotnet-buildtools/v/Microsoft.DotNet.BuildTools.svg?label=Packages)](https://dotnet.myget.org/gallery/dotnet-buildtools/)

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
[Microsoft.DotNet.BuildTools]: https://dotnet.myget.org/feed/dotnet-buildtools/package/nuget/Microsoft.DotNet.BuildTools
[sn-sign]: https://github.com/dotnet/corefx/wiki/Strong%20Naming
