@echo off
setlocal EnableDelayedExpansion 

:: Note: We've disabled node reuse because it causes file locking issues.
::       The issue is that we extend the build with our own targets which
::       means that that rebuilding cannot successfully delete the task
::       assembly. 

if not defined VisualStudioVersion (
    if defined VS140COMNTOOLS (
        call "%VS140COMNTOOLS%\VsDevCmd.bat"
        goto :EnvSet
    )

    if defined VS120COMNTOOLS (
        call "%VS120COMNTOOLS%\VsDevCmd.bat"
        goto :EnvSet
    )

    echo Error: build.cmd requires Visual Studio 2013 or 2015.  
    echo        Please see https://github.com/dotnet/corefx/blob/master/Documentation/developer-guide.md for build instructions.
    exit /b 1
)

REM Process arguments passed in to build.cmd
set "__args= %*"
set __ConfigurationGroup=Debug
set processedArgs=
set __Platform=AnyCPU
:Arg_Loop
if "%1" == "" goto ArgsDone

if /i "%1" == "-release"                 (set __ConfigurationGroup=Release&set processedArgs=!processedArgs! %1&shift&goto Arg_Loop)
if /i "%1" == "-debug"                 (set __ConfigurationGroup=Debug&set processedArgs=!processedArgs! %1&shift&goto Arg_Loop)
if /i "%1" == "-platform"                 (set __Platform=%2&set processedArgs=!processedArgs! %1 %2&shift&shift&goto Arg_Loop)
if /i "%1" == "--"                 (set processedArgs=!processedArgs! %1&shift&goto Arg_Loop)

if [!processedArgs!]==[] (
  set __UnprocessedBuildArgs=%__args%
) else (
  set __UnprocessedBuildArgs=%__args%
  for %%t in (!processedArgs!) do (
    set __UnprocessedBuildArgs=!__UnprocessedBuildArgs:*%%t=!
  )
)
:ArgsDone

:EnvSet

call %~dp0init-tools.cmd

:: Log build command line
set _buildproj=%~dp0build.proj
set _buildlog=%~dp0msbuild.log
set _buildprefix=echo
set _buildpostfix=^> "%_buildlog%"
call :build %*

:: Build
set _buildprefix=
set _buildpostfix=
call :build %*

goto :AfterBuild

:build
%_buildprefix% msbuild "%_buildproj%" /nologo /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=normal;LogFile="%_buildlog%";Append /p:ConfigurationGroup=%__ConfigurationGroup% /p:Platform=%__Platform% !__UnprocessedBuildArgs! %_buildpostfix%
set BUILDERRORLEVEL=%ERRORLEVEL%
goto :eof

:AfterBuild

echo.
:: Pull the build summary from the log file
findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%_buildlog%"
echo Build Exit Code = %BUILDERRORLEVEL%

exit /b %BUILDERRORLEVEL%