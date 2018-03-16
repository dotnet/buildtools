#!/usr/bin/env bash

# Stop script on NZEC
set -e
# Stop script if unbound variable found (use ${var:-} if intentional)
set -u
# By default cmd1 | cmd2 returns exit code of cmd2 regardless of cmd1 success
# This is causing it to fail
set -o pipefail

__PROJECT_DIR=${1:-}
__DOTNET_CMD=${2:-}
__TOOLRUNTIME_DIR=${3:-}
__PACKAGES_DIR=${4:-$__TOOLRUNTIME_DIR}
__TOOLS_DIR=$(cd "$(dirname "$0")"; pwd -P)
__MICROBUILD_VERSION=0.2.0
__PORTABLETARGETS_VERSION=0.1.1-dev
__ROSLYNCOMPILER_VERSION=2.6.0-beta3-62316-02

__PORTABLETARGETS_PROJECT_CONTENT="
<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <TargetFrameworks>netcoreapp1.0;net46</TargetFrameworks>
    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=\"MicroBuild.Core\" Version=\"$__MICROBUILD_VERSION\" />
    <PackageReference Include=\"Microsoft.Portable.Targets\" Version=\"$__PORTABLETARGETS_VERSION\" />
    <PackageReference Include=\"Microsoft.NETCore.Compilers\" Version=\"$__ROSLYNCOMPILER_VERSION\" />
  </ItemGroup>
</Project>"
__PUBLISH_TFM=netcoreapp2.0

__INIT_TOOLS_RESTORE_ARGS="--source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json ${__INIT_TOOLS_RESTORE_ARGS:-}"
__TOOLRUNTIME_RESTORE_ARGS="--source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json ${__INIT_TOOLS_RESTORE_ARGS}"

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

cp -R $__TOOLS_DIR/* $__TOOLRUNTIME_DIR

__TOOLRUNTIME_PROJECT=$__TOOLS_DIR/tool-runtime/project.csproj

echo "Running: $__DOTNET_CMD restore \"${__TOOLRUNTIME_PROJECT}\" $__TOOLRUNTIME_RESTORE_ARGS"
$__DOTNET_CMD restore "${__TOOLRUNTIME_PROJECT}" $__TOOLRUNTIME_RESTORE_ARGS

echo "Running: $__DOTNET_CMD publish \"${__TOOLRUNTIME_PROJECT}\" -f ${__PUBLISH_TFM} -o $__TOOLRUNTIME_DIR"
$__DOTNET_CMD publish "${__TOOLRUNTIME_PROJECT}" -f ${__PUBLISH_TFM} -o $__TOOLRUNTIME_DIR

# Copy Portable Targets Over to ToolRuntime
if [ ! -d "${__PACKAGES_DIR}/generated" ]; then mkdir "${__PACKAGES_DIR}/generated"; fi
__PORTABLETARGETS_PROJECT=${__PACKAGES_DIR}/generated/project.csproj

echo $__PORTABLETARGETS_PROJECT_CONTENT > "${__PORTABLETARGETS_PROJECT}"

echo "Running: \"$__DOTNET_CMD\" restore \"${__PORTABLETARGETS_PROJECT}\" $__INIT_TOOLS_RESTORE_ARGS --packages \"${__PACKAGES_DIR}/.\""
$__DOTNET_CMD restore "${__PORTABLETARGETS_PROJECT}" $__INIT_TOOLS_RESTORE_ARGS --packages "${__PACKAGES_DIR}/."

# Copy portable and MicroBuild targets from packages, allowing for lowercased package IDs.
cp -R "${__PACKAGES_DIR}"/[Mm]icrosoft.[Pp]ortable.[Tt]argets/"${__PORTABLETARGETS_VERSION}/contentFiles/any/any/Extensions/." "$__TOOLRUNTIME_DIR/."
cp -R "${__PACKAGES_DIR}"/[Mm]icro[Bb]uild.[Cc]ore/"${__MICROBUILD_VERSION}/build/." "$__TOOLRUNTIME_DIR/."

# Temporary Hacks to fix couple of issues in the msbuild and roslyn nuget packages
# https://github.com/dotnet/buildtools/issues/1464
[ -e "$__TOOLRUNTIME_DIR/Microsoft.CSharp.Targets" ] || mv "$__TOOLRUNTIME_DIR/Microsoft.CSharp.targets" "$__TOOLRUNTIME_DIR/Microsoft.CSharp.Targets"

# Copy some roslyn files over
cp $__TOOLRUNTIME_DIR/runtimes/any/native/* $__TOOLRUNTIME_DIR/

#Temporarily rename roslyn compilers to have exe extension
cp ${__TOOLRUNTIME_DIR}/csc.dll ${__TOOLRUNTIME_DIR}/csc.exe
cp ${__TOOLRUNTIME_DIR}/vbc.dll ${__TOOLRUNTIME_DIR}/vbc.exe

#Copy RID specific assets to the tools dir since we don't have a deps.json for .NETCore msbuild
cp ${__TOOLRUNTIME_DIR}/runtimes/unix/lib/netstandard1.3/*.dll $__TOOLRUNTIME_DIR/

# Override versions in runtimeconfig.json files with highest available runtime version.
__MNCA_FOLDER=$(dirname $__DOTNET_CMD)/shared/Microsoft.NETCore.App
__HIGHEST_RUNTIME_VERSION=`ls $__MNCA_FOLDER | sed 'r/\([0-9]\+\).*/\1/g' | sort -n | tail -1`
sed -i -e "s/1.1.0/$__HIGHEST_RUNTIME_VERSION/g" $__TOOLRUNTIME_DIR/*.runtimeconfig.json

# Restore ILAsm, if requested in the environment.
__ILASM_PACKAGE_VERSION="${ILASMCOMPILER_VERSION:-}"
if [ "$__ILASM_PACKAGE_VERSION" ]; then
    echo "Restoring ILAsm version '$__ILASM_PACKAGE_VERSION'..."
	
    __ILASM_PACKAGE_RID="${NATIVE_TOOLS_RID:-}"
    if [ "$__ILASM_PACKAGE_RID" == "" ]; then
        echo "ERROR: Please specify native package RID."
        exit 1
    fi

    echo "Running: \"$__DOTNET_CMD\" build \"${__TOOLRUNTIME_DIR}/ilasm/ilasm.depproj\""
    $__DOTNET_CMD build "${__TOOLRUNTIME_DIR}/ilasm/ilasm.depproj" --packages "${__PACKAGES_DIR}/." --source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json -r $__ILASM_PACKAGE_RID -p:ILAsmPackageVersion=$__ILASM_PACKAGE_VERSION
fi

# Download the package version props file, if passed in the environment.
__PACKAGE_VERSION_PROPS_URL="${PACKAGEVERSIONPROPSURL:-}"
__PACKAGE_VERSION_PROPS_PATH="$__TOOLRUNTIME_DIR/DownloadedPackageVersions.props"

if [ "$__PACKAGE_VERSION_PROPS_URL" ]; then
    echo "Downloading package version props from '$__PACKAGE_VERSION_PROPS_URL' to '$__PACKAGE_VERSION_PROPS_PATH'..."

    # Copied from CoreFX init-tools.sh
    if command -v curl > /dev/null; then
        curl --retry 10 -sSL --create-dirs -o "$__PACKAGE_VERSION_PROPS_PATH" "$__PACKAGE_VERSION_PROPS_URL"
    else
        wget -q -O "$__PACKAGE_VERSION_PROPS_PATH" "$__PACKAGE_VERSION_PROPS_URL"
    fi

    echo "Downloaded package version props:"
    cat "$__PACKAGE_VERSION_PROPS_PATH"
fi

exit 0
