@echo off
setlocal

set PROJECT_DIR=%~1
set DOTNET_CMD=%~2
set TOOLRUNTIME_DIR=%~3
set BUILDTOOLS_PACKAGE_DIR=%~dp0
set MICROBUILD_VERSION=0.2.0
set ROSLYNCOMPILERS_VERSION=1.2.0-beta1-20160202-02
set NuProj_Version=0.10.4-beta-gf7fc34e7d8
set MSBUILD_CONTENT_JSON={"dependencies": { "NuProj": "%NuProj_Version%", "MicroBuild.Core": "%MICROBUILD_VERSION%", "Microsoft.Net.Compilers": "%ROSLYNCOMPILERS_VERSION%"},"frameworks": {"dnxcore50": {},"net46": {}}}

if not exist "%DOTNET_CMD%" (
  echo ERROR: Cannot find dotnet cli at [%DOTNET_CMD%]. Please pass in the path to dotnet.exe as the 1st parameter.
  exit /b 1
)

if [%TOOLRUNTIME_DIR%]==[] (
  echo ERROR: You have to pass in the ToolRuntime directory as the 2nd parameter.
  exit /b 1
)

:: Copy Roslyn Compilers Over to ToolRuntime, Micro Build and NuProj
if not exist "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage" mkdir "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage"
echo %MSBUILD_CONTENT_JSON% > "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage\project.json"
cd "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage\"
call "%DOTNET_CMD%" restore --source http://www.nuget.org/api/v2/ --packages "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage\packages"
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage\packages\Microsoft.Net.Compilers\%ROSLYNCOMPILERS_VERSION%\." "%TOOLRUNTIME_DIR%\net45\roslyn\." /E
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage\packages\MicroBuild.Core\%MICROBUILD_VERSION%\build\." "%TOOLRUNTIME_DIR%\." /E
Robocopy "%BUILDTOOLS_PACKAGE_DIR%\roslynPackage\packages\NuProj\%NuProj_Version%\tools\." "%TOOLRUNTIME_DIR%\NuProj\." /E

exit /b %ERRORLEVEL%
