@if "%_echo%" neq "on" echo off
setlocal
echo msbuild.exe %*
call msbuild.exe %*
IF ERRORLEVEL 1 (
    echo Error!
    exit /b 1 
)
exit /b