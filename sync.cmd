@if "%_echo%" neq "on" echo off
setlocal

set _args=%*
if /I [%1]==[/?] goto Usage
if [%1]==[]  set _args=-p

echo Running executor.cmd %_args%
call executor.cmd %_args%
if NOT [%ERRORLEVEL%]==[0] (
  echo ERROR: An error occurred. There may have been networking problems so please try again in a few minutes.
  exit /b 1
)

echo Done Syncing.
exit /b 0

:Usage
echo.
echo Repository syncing script.
echo.
echo Options:
echo     -p     - Restores all nuget packages for repository
echo.
echo If no option is specified then sync.cmd -p is implied.