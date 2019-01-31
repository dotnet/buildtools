@if not defined _echo @echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
set PACKAGES_DIR=%~4
set BUILDTOOLS_PACKAGE_DIR=%~dp0
set MICROBUILD_VERSION=0.2.0
set ROSLYNCOMPILERS_VERSION=3.0.0-beta2-final

:: Default to x64 native tools if nothing was specified.
if [%NATIVE_TOOLS_RID%]==[] (
  set NATIVE_TOOLS_RID=win-x64
)

set MSBUILD_PROJECT_CONTENT= ^
 ^^^<Project^^^> ^
  ^^^<PropertyGroup^^^> ^
    ^^^<ImportDirectoryBuildProps^^^>false^^^</ImportDirectoryBuildProps^^^> ^
    ^^^<ImportDirectoryBuildTargets^^^>false^^^</ImportDirectoryBuildTargets^^^> ^
    ^^^<TargetFrameworks^^^>netcoreapp1.0;net46^^^</TargetFrameworks^^^> ^
    ^^^<DisableImplicitFrameworkReferences^^^>true^^^</DisableImplicitFrameworkReferences^^^> ^
  ^^^</PropertyGroup^^^> ^
  ^^^<Import Project=^"Sdk.props^" Sdk=^"Microsoft.NET.Sdk^" /^^^> ^
  ^^^<ItemGroup^^^> ^
    ^^^<PackageReference Include=^"MicroBuild.Core^" Version=^"%MICROBUILD_VERSION%^" /^^^> ^
    ^^^<PackageReference Include=^"Microsoft.Net.Compilers^" Version=^"%ROSLYNCOMPILERS_VERSION%^" /^^^> ^
  ^^^</ItemGroup^^^> ^
  ^^^<Import Project=^"Sdk.targets^" Sdk=^"Microsoft.NET.Sdk^" /^^^> ^
 ^^^</Project^^^>

set PUBLISH_TFM=netcoreapp2.0

set DEFAULT_RESTORE_ARGS=--no-cache --packages "%PACKAGES_DIR%"
set INIT_TOOLS_RESTORE_ARGS=%DEFAULT_RESTORE_ARGS% --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://api.nuget.org/v3/index.json %INIT_TOOLS_RESTORE_ARGS%
set TOOLRUNTIME_RESTORE_ARGS=--source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json %INIT_TOOLS_RESTORE_ARGS%

if not exist "%PROJECT_DIR%" (
  echo ERROR: Cannot find project root path at [%PROJECT_DIR%]. Please pass in the source directory as the 1st parameter.
  exit /b 1
)

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 2nd parameter.
  exit /b 1
)

if "%TOOLRUNTIME_DIR%" == "" (
  echo ERROR: Please pass in the tools directory as the 3rd parameter.
  exit /b 1
)

if "%PACKAGES_DIR%" == "" (
  echo ERROR: Please pass in the packages directory as the 4th parameter.
  exit /b 1
)

ROBOCOPY "%BUILDTOOLS_PACKAGE_DIR%\." "%TOOLRUNTIME_DIR%" /E

set TOOLRUNTIME_PROJECT=%BUILDTOOLS_PACKAGE_DIR%\tool-runtime\project.csproj

@echo on
call "%DOTNET_CMD%" restore "%TOOLRUNTIME_PROJECT%" %TOOLRUNTIME_RESTORE_ARGS%
set RESTORE_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_ERROR_LEVEL%]==[0] (
  echo ERROR: An error occured when running: '"%DOTNET_CMD%" restore "%TOOLRUNTIME_PROJECT%" %TOOLRUNTIME_RESTORE_ARGS%'. Please check above for more details.
  exit /b %RESTORE_ERROR_LEVEL%
)
@echo on
call "%DOTNET_CMD%" publish --no-restore "%TOOLRUNTIME_PROJECT%" -f %PUBLISH_TFM% -o "%TOOLRUNTIME_DIR%"
set TOOLRUNTIME_PUBLISH_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%TOOLRUNTIME_PUBLISH_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" publish "%TOOLRUNTIME_PROJECT%" -f %PUBLISH_TFM%'. Please check above for more details.
  exit /b %TOOLRUNTIME_PUBLISH_ERROR_LEVEL%
)
@echo on
call "%DOTNET_CMD%" publish --no-restore "%TOOLRUNTIME_PROJECT%" -f net46 -o "%TOOLRUNTIME_DIR%\net46"
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
if not exist "%TOOLRUNTIME_DIR%\generated" mkdir "%TOOLRUNTIME_DIR%\generated"
set PORTABLETARGETS_PROJECT=%TOOLRUNTIME_DIR%\generated\project.csproj
echo %MSBUILD_PROJECT_CONTENT% > "%PORTABLETARGETS_PROJECT%"
@echo on
call "%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECT%" %INIT_TOOLS_RESTORE_ARGS%
set RESTORE_PORTABLETARGETS_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_PORTABLETARGETS_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" restore "%PORTABLETARGETS_PROJECT%"'. Please check above for more details.
  exit /b %RESTORE_PORTABLETARGETS_ERROR_LEVEL%
)
Robocopy "%PACKAGES_DIR%\MicroBuild.Core\%MICROBUILD_VERSION%\build\." "%TOOLRUNTIME_DIR%\." /E

:: Copy Roslyn Compilers Over to ToolRuntime
Robocopy "%PACKAGES_DIR%\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net46\roslyn\." /E

:: Restore ILAsm if the caller asked for it by setting the environment variable
if [%ILASMCOMPILER_VERSION%]==[] goto :afterILAsmRestore

@echo on
call "%DOTNET_CMD%" build "%TOOLRUNTIME_DIR%\ilasm\ilasm.depproj" %DEFAULT_RESTORE_ARGS% -r %NATIVE_TOOLS_RID% --source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json /p:ILAsmPackageVersion=%ILASMCOMPILER_VERSION%
set RESTORE_ILASM_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%RESTORE_ILASM_ERROR_LEVEL%]==[0] (
  echo ERROR: An error ocurred when running: '"%DOTNET_CMD%" build "%TOOLRUNTIME_DIR%\ilasm\ilasm.depproj"'. Please check above for more details.
  exit /b %RESTORE_ILASM_ERROR_LEVEL%
)
if not exist "%TOOLRUNTIME_DIR%\ilasm\ilasm.exe" (
  echo ERROR: Failed to restore ilasm.exe
  exit /b 1
)
:afterILAsmRestore

@echo on
powershell -NoProfile -ExecutionPolicy unrestricted -File "%BUILDTOOLS_PACKAGE_DIR%\init-tools.ps1" -ToolRuntimePath "%TOOLRUNTIME_DIR%" -DotnetCmd "%DOTNET_CMD%" -BuildToolsPackageDir "%BUILDTOOLS_PACKAGE_DIR%"
set POWERSHELL_INIT_TOOLS_ERROR_LEVEL=%ERRORLEVEL%
@echo off
if not [%POWERSHELL_INIT_TOOLS_ERROR_LEVEL%]==[0] (
  echo ERROR: An error occurred when running: 'powershell -NoProfile -ExecutionPolicy unrestricted -File "%BUILDTOOLS_PACKAGE_DIR%\init-tools.ps1" -ToolRuntimePath "%TOOLRUNTIME_DIR%" -DotnetCmd "%DOTNET_CMD%" -BuildToolsPackageDir "%BUILDTOOLS_PACKAGE_DIR%"'. Please check above for more details.
  exit /b %POWERSHELL_INIT_TOOLS_ERROR_LEVEL%
)

exit /b 0
