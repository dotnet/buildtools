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

# Determine if the CLI supports MSBuild projects. This controls whether csproj files are used for initialization and package restore.
__CLI_VERSION=`$__DOTNET_CMD --version`
# Check the first character in the version string. Version 2 and above supports MSBuild.
__CLI_VERSION=${__CLI_VERSION:0:1}
if [ "$__CLI_VERSION" -ge "2" ]; then
  BUILDTOOLS_USE_CSPROJ=true
fi

if [ -z "${__BUILDTOOLS_USE_CSPROJ:-}" ]; then
    __PORTABLETARGETS_PROJECT_CONTENT="
{
  \"dependencies\":
  {
    \"MicroBuild.Core\": \"${__MICROBUILD_VERSION}\",
    \"Microsoft.Portable.Targets\": \"${__PORTABLETARGETS_VERSION}\"
  },
  \"frameworks\": {\"netcoreapp1.0\": {},\"net46\": {}
  }
}"
    __PROJECT_EXTENSION=json
    __PUBLISH_TFM=netcoreapp1.0
else
    __PORTABLETARGETS_PROJECT_CONTENT="
<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <TargetFrameworks>netcoreapp1.0;net46</TargetFrameworks>
    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=\"MicroBuild.Core\" Version=\"$__MICROBUILD_VERSION\" />
    <PackageReference Include=\"Microsoft.Portable.Targets\" Version=\"$__PORTABLETARGETS_VERSION\" />
  </ItemGroup>
</Project>"
    __PROJECT_EXTENSION=csproj
    __PUBLISH_TFM=netcoreapp2.0
fi

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

__TOOLRUNTIME_PROJECT=$__TOOLS_DIR/tool-runtime/project.$__PROJECT_EXTENSION

echo "Running: $__DOTNET_CMD restore \"${__TOOLRUNTIME_PROJECT}\" $__TOOLRUNTIME_RESTORE_ARGS"
$__DOTNET_CMD restore "${__TOOLRUNTIME_PROJECT}" $__TOOLRUNTIME_RESTORE_ARGS

echo "Running: $__DOTNET_CMD publish \"${__TOOLRUNTIME_PROJECT}\" -f ${__PUBLISH_TFM} -o $__TOOLRUNTIME_DIR"
$__DOTNET_CMD publish "${__TOOLRUNTIME_PROJECT}" -f ${__PUBLISH_TFM} -o $__TOOLRUNTIME_DIR

# Copy Portable Targets Over to ToolRuntime
if [ ! -d "${__PACKAGES_DIR}/generated" ]; then mkdir "${__PACKAGES_DIR}/generated"; fi
__PORTABLETARGETS_PROJECT=${__PACKAGES_DIR}/generated/project.$__PROJECT_EXTENSION

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

# Override versions in runtimeconfig.json files with highest available runtime version.
__MNCA_FOLDER=$(dirname $__DOTNET_CMD)/shared/Microsoft.NETCore.App
__HIGHEST_RUNTIME_VERSION=`ls $__MNCA_FOLDER | sed 'r/\([0-9]\+\).*/\1/g' | sort -n | tail -1`
sed -i -e "s/1.1.0/$__HIGHEST_RUNTIME_VERSION/g" $__TOOLRUNTIME_DIR/*.runtimeconfig.json

exit 0
