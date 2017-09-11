@echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
set PACKAGES_DIR=%4
if [%PACKAGES_DIR%] == [] set PACKAGES_DIR=%TOOLRUNTIME_DIR%
:: Remove quotes to the packages directory
set PACKAGES_DIR=%PACKAGES_DIR:"=%
set BUILDTOOLS_PACKAGE_DIR=%~dp0
set MICROBUILD_VERSION=0.2.0
set PORTABLETARGETS_VERSION=0.1.1-dev
set ROSLYNCOMPILERS_VERSION=2.0.0-rc

:: Determine if the CLI supports MSBuild projects. This controls whether csproj files are used for initialization and package restore.
set CLI_VERSION=
for /f "delims=" %%a in ('%DOTNET_CMD% --version') do @set CLI_VERSION=%%a
:: Check the first character in the version string. Version 2 and above supports MSBuild.
set CLI_VERSION=%CLI_VERSION:~0,1%
if %CLI_VERSION% geq 2 (
  set BUILDTOOLS_USE_CSPROJ=true
  echo "Detected a 2.0-capable CLI."
)

if [%BUILDTOOLS_USE_CSPROJ%]==[] (
  set MSBUILD_PROJECT_CONTENT= ^
{ ^
  "dependencies": ^
    { ^
      "MicroBuild.Core": "%MICROBUILD_VERSION%", ^
      "Microsoft.Portable.Targets": "%PORTABLETARGETS_VERSION%", ^
      "Microsoft.Net.Compilers": "%ROSLYNCOMPILERS_VERSION%" ^
    }, ^
  "frameworks": {"netcoreapp1.0": {},"net46": {}} ^
}
  set PROJECT_EXTENSION=json
  set PUBLISH_TFM=netcoreapp1.0
) ELSE (
  set MSBUILD_PROJECT_CONTENT= ^
 ^^^<Project Sdk=^"Microsoft.NET.Sdk^"^^^> ^
  ^^^<PropertyGroup^^^> ^
    ^^^<TargetFrameworks^^^>netcoreapp1.0;net46^^^</TargetFrameworks^^^> ^
    ^^^<DisableImplicitFrameworkReferences^^^>true^^^</DisableImplicitFrameworkReferences^^^> ^
  ^^^</PropertyGroup^^^> ^
  ^^^<ItemGroup^^^> ^
    ^^^<PackageReference Include=^"MicroBuild.Core^" Version=^"%MICROBUILD_VERSION%^" /^^^> ^
    ^^^<PackageReference Include=^"Microsoft.Portable.Targets^" Version=^"%PORTABLETARGETS_VERSION%^" /^^^> ^
    ^^^<PackageReference Include=^"Microsoft.Net.Compilers^" Version=^"%ROSLYNCOMPILERS_VERSION%^" /^^^> ^
  ^^^</ItemGroup^^^> ^
 ^^^</Project^^^>
  set PROJECT_EXTENSION=csproj
  set PUBLISH_TFM=netcoreapp2.0
)
set INIT_TOOLS_RESTORE_ARGS=--source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json %INIT_TOOLS_RESTORE_ARGS%
set TOOLRUNTIME_RESTORE_ARGS=--source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json %INIT_TOOLS_RESTORE_ARGS%

if not exist "%PROJECT_DIR%" (
  echo ERROR: Cannot find project root path at [%PROJECT_DIR%]. Please pass in the source directory as the 1st parameter.
  exit /b 1
)

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 2nd parameter.
  exit /b 1
)

ROBOCOPY "%BUILDTOOLS_PACKAGE_DIR%\." "%TOOLRUNTIME_DIR%" /E

set TOOLRUNTIME_PROJECT=%BUILDTOOLS_PACKAGE_DIR%\tool-runtime\project.%PROJECT_EXTENSION%

@echo on
call "%DOTNET_CMD%" restore "%TOOLRUNTIME_PROJECT%" %TOOLRUNTIME_RESTORE_ARGS%
set RESTORE_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_ERROR_LEVEL%]==[0] (
  echo ERROR: An error occured when running: '"%DOTNET_CMD%" restore "%TOOLRUNTIME_PROJECT%"'. Please check above for more details.
  exit /b %RESTORE_ERROR_LEVEL%
)
@echo on
call "%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECT%" -f %PUBLISH_TFM% -o "%TOOLRUNTIME_DIR%"
set TOOLRUNTIME_PUBLISH_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%TOOLRUNTIME_PUBLISH_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECT%" -f %PUBLISH_TFM%'. Please check above for more details.
  exit /b %TOOLRUNTIME_PUBLISH_ERROR_LEVEL%
)
@echo on
call "%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECT%" -f net46 -o "%TOOLRUNTIME_DIR%\net46"
set NET46_PUBLISH_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%NET46_PUBLISH_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECT%" -f net46'. Please check above for more details.
  exit /b %NET46_PUBLISH_ERROR_LEVEL%
)

:: Copy some roslyn files which are published into runtimes\any\native to the root
Robocopy "%TOOLRUNTIME_DIR%\runtimes\any\native" "%TOOLRUNTIME_DIR%\."

:: Microsoft.Build.Runtime dependency is causing the MSBuild.runtimeconfig.json buildtools copy to be overwritten - re-copy the buildtools version.
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\." "%TOOLRUNTIME_DIR%\." "MSBuild.runtimeconfig.json"

:: Copy Portable Targets Over to ToolRuntime
if not exist "%PACKAGES_DIR%\generated" mkdir "%PACKAGES_DIR%\generated"
set PORTABLETARGETS_PROJECT=%PACKAGES_DIR%\generated\project.%PROJECT_EXTENSION%
echo %MSBUILD_PROJECT_CONTENT% > "%PORTABLETARGETS_PROJECT%"
@echo on
call "%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECT%" %INIT_TOOLS_RESTORE_ARGS% --packages "%PACKAGES_DIR%\."
set RESTORE_PORTABLETARGETS_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_PORTABLETARGETS_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECT%"'. Please check above for more details.
  exit /b %RESTORE_PORTABLETARGETS_ERROR_LEVEL%
)
Robocopy "%PACKAGES_DIR%\Microsoft.Portable.Targets\%PORTABLETARGETS_VERSION%\contentFiles\any\any\Extensions." "%TOOLRUNTIME_DIR%\." /E
Robocopy "%PACKAGES_DIR%\MicroBuild.Core\%MICROBUILD_VERSION%\build\." "%TOOLRUNTIME_DIR%\." /E

:: Copy Roslyn Compilers Over to ToolRuntime
Robocopy "%PACKAGES_DIR%\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net46\roslyn\." /E

@echo on
powershell -NoProfile -ExecutionPolicy unrestricted %BUILDTOOLS_PACKAGE_DIR%\init-tools.ps1 -ToolRuntimePath %TOOLRUNTIME_DIR% -DotnetCmd %DOTNET_CMD% -BuildToolsPackageDir %BUILDTOOLS_PACKAGE_DIR%
set POWERSHELL_INIT_TOOLS_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%POWERSHELL_INIT_TOOLS_ERROR_LEVEL%]==[0] (
  echo ERROR: An error occurred when running: 'powershell -NoProfile -ExecutionPolicy unrestricted %BUILDTOOLS_PACKAGE_DIR%\init-tools.ps1 -ToolRuntimePath %TOOLRUNTIME_DIR% -DotnetCmd %DOTNET_CMD% -BuildToolsPackageDir %BUILDTOOLS_PACKAGE_DIR%'. Please check above for more details.
  exit /b %POWERSHELL_INIT_TOOLS_ERROR_LEVEL%
)

exit /b 0
