@if "%_echo%" neq "on" echo off
setlocal EnableDelayedExpansion

set cleanlog=%~dp0clean.log
echo Running Clean.cmd %* > %cleanlog%

set unprocessedBuildArgs=
set allargs=%*
set thisArgs=
set clean_successful=true

if [%1] == [] (
  set clean_bin=true;
  goto Begin
)

set clean_bin=
set clean_pgk=
set clean_pgkcache=
set clean_src=
set clean_tools=
set clean_environment=
set clean_all=

:Loop
if [%1] == [] goto Begin

if /I [%1] == [/?] goto Usage

if /I [%1] == [-b] (
  set clean_bin=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if /I [%1] == [-p] (
  set clean_pgk=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if /I [%1] == [-c] (
  set clean_pgkcache=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if /I [%1] == [-s] (
  set clean_src=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if /I [%1] == [-t] (
  set clean_tools=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if /I [%1] == [-e] (
  set clean_environment=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if /I [%1] == [/all] (
  set clean_src=
  set clean_tools=
  set clean_environment=
  set clean_bin=true
  set clean_pgk=true
  set clean_pgkcache=true
  set clean_all=true
  set thisArgs=!thisArgs!%1
  goto Next
)

if [!thisArgs!]==[] (
  set unprocessedBuildArgs=!allargs!
) else (
  call set unprocessedBuildArgs=%%allargs:*!thisArgs!=%%
)

:Next
shift /1
goto Loop

:Begin
if /I [%clean_environment%] == [true] (
  call :CleanEnvironment
)

if /I [%clean_src%] == [true] (
  echo Cleaning src directory ...
  echo. >> %cleanlog% && echo git clean -xdf %~dp0src >> %cleanlog%
  call git clean -xdf %~dp0src >> %cleanlog%
  call :CheckErrorLevel
)

if /I [%clean_bin%] == [true] (
  echo Cleaning bin directory ...
  echo. >> %cleanlog% && echo %~dp0executor.cmd -clean -b !unprocessedBuildArgs!>> %cleanlog%
  call %~dp0executor.cmd -clean -b !unprocessedBuildArgs!>> %cleanlog%
  call :CheckErrorLevel
)

if /I [%clean_pgk%] == [true] (
  echo Cleaning package directory ...
  echo. >> %cleanlog% && echo %~dp0executor.cmd -clean -p !unprocessedBuildArgs!>> %cleanlog%
  call %~dp0executor.cmd -clean -p !unprocessedBuildArgs!>> %cleanlog%
  call :CheckErrorLevel
)

if /I [%clean_pgkcache%] == [true] (
  echo Cleaning package cache directory ...
  echo. >> %cleanlog% && echo %~dp0executor.cmd -clean -c !unprocessedBuildArgs!>> %cleanlog%
  call %~dp0executor.cmd -clean -c !unprocessedBuildArgs!>> %cleanlog%
  call :CheckErrorLevel
)

if /I [%clean_tools%] == [true] (
  call :CleanEnvironment
  echo Cleaning tools directory ...
  echo. >> %cleanlog% && echo rmdir /s /q %~dp0tools >> %cleanlog%
  rmdir /s /q %~dp0tools >> %cleanlog%
  REM Don't call CheckErrorLevel because if the Tools directory didn't exist when this script was
  REM invoked, then it sometimes exits with error level 3 despite successfully deleting the directory.
)

if /I [%clean_all%] == [true] (
  call :CleanEnvironment
  echo Cleaning entire working directory ...
  echo. >> %cleanlog% && echo git clean -xdf -e clean.log %~dp0 >> %cleanlog%
  call git clean -xdf -e clean.log %~dp0 >> %cleanlog%
  call :CheckErrorLevel
)

if /I [%clean_successful%] == [true] (
  echo Clean completed successfully.
  echo. >> %cleanlog% && echo Clean completed successfully. >> %cleanlog%
  exit /b 0
) else (
  echo An error occured while cleaning; see %cleanlog% for more details.
  echo. >> %cleanlog% && echo Clean completed with errors. >> %cleanlog%
  exit /b 1
)

:Usage
echo.
echo Repository cleaning script.
echo.
echo Options:
echo     -b     - Deletes the binary output directory.
echo     -p     - Deletes the repo-local nuget package directory.
echo     -c     - Deletes the user-local nuget package cache.
echo     -t     - Deletes the tools directory.
echo     -s     - Deletes the untracked files under src directory (git clean src -xdf).
echo     -e     - Kills and/or stops the processes that are still running, for example VBCSCompiler.exe
echo     -all   - Combines all of the above.
echo.
echo If no option is specified then clean.cmd -b is implied.

exit /b 1

:CheckErrorLevel
if NOT [%ERRORLEVEL%]==[0] (
  echo Command exited with ERRORLEVEL %ERRORLEVEL% >> %cleanlog%
  set clean_successful=false
)
exit /b

:CleanEnvironment
REM If VBCSCompiler.exe is running we need to kill it
echo Stop VBCSCompiler.exe execution.
echo. >> %cleanlog% && echo Stop VBCSCompiler.exe execution. >> %cleanlog% 
for /f "tokens=2 delims=," %%F in ('tasklist /nh /fi "imagename eq VBCSCompiler.exe" /fo csv') do taskkill /f /PID %%~F >> %cleanlog%
exit /b