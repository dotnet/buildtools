#!/usr/bin/env bash

__PROJECT_DIR=$1
__DOTNET_CMD=$2
__TOOLRUNTIME_DIR=$3
__TOOLS_DIR=$(cd "$(dirname "$0")"; pwd -P)
__MICROBUILD_VERSION=0.2.0
__PORTABLETARGETS_VERSION=0.1.1-dev
__NuProj_Version=0.10.4-beta-gf7fc34e7d8
__MSBUILD_CONTENT_JSON="{\"dependencies\": { \"NuProj\": \"$__NuProj_Version\", \"MicroBuild.Core\": \"$__MICROBUILD_VERSION\" },\"frameworks\": {\"dnxcore50\": {},\"net46\": {}}}"

__BUILDERRORLEVEL=0

if [ ! -d "$__PROJECT_DIR" ]; then
    echo "ERROR: Cannot find project root path at '$__PROJECT_DIR'. Please pass in the source directory as the 1st parameter."
    exit 1
fi

if [ ! -e "$__DOTNET_CMD" ]; then
    echo "ERROR: Cannot find dotnet.exe at path '$__DOTNET_CMD'. Please pass in the path to dotnet.exe as the 2nd parameter."
    exit 1
fi

if [ -z "$__TOOLRUNTIME_DIR" ]; then
    echo "ERROR: Please pass in the tools directory as the 3rd parameter."
    exit 1
fi

if [ ! -d "$__TOOLRUNTIME_DIR" ]; then
    mkdir $__TOOLRUNTIME_DIR
fi

# Copy NuProj Over to ToolRuntime and Micro Build.

if [ ! -d "${__TOOLS_DIR}/roslynPackage" ]; then mkdir "${__TOOLS_DIR}/roslynPackage"; fi
echo $__MSBUILD_CONTENT_JSON > "${__TOOLS_DIR}/roslynPackage/project.json"
cd "${__TOOLS_DIR}/roslynPackage"
"${__DOTNET_CMD}" restore --source http://www.nuget.org/api/v2 --packages "${__TOOLS_DIR}/roslynPackage/packages"
cp -R "${__TOOLS_DIR}/roslynPackage/packages/MicroBuild.Core/$__MICROBUILD_VERSION/build/." "$__TOOLRUNTIME_DIR/."
cp -R "${__TOOLS_DIR}/roslynPackage/packages/NuProj/$__NuProj_Version/tools/." "$__TOOLRUNTIME_DIR/NuProj/."
chmod a+x $__TOOLRUNTIME_DIR/corerun

if [ -n "$BUILDTOOLS_OVERRIDE_RUNTIME" ]; then
    find $__TOOLRUNTIME_DIR -name *.ni.* | xargs rm 2>/dev/null
    cp -R $BUILDTOOLS_OVERRIDE_RUNTIME/* $__TOOLRUNTIME_DIR
fi

# Temporary Hacks to fix couple of issues in the msbuild and roslyn nuget packages
cp "$__TOOLRUNTIME_DIR/corerun" "$__TOOLRUNTIME_DIR/corerun.exe"
mv "$__TOOLRUNTIME_DIR/Microsoft.CSharp.targets" "$__TOOLRUNTIME_DIR/Microsoft.CSharp.Targets"

exit $__BUILDERRORLEVEL
