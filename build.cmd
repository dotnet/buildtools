@if "%_echo%" neq "on" echo off
call executor.cmd %* build
exit /b %ERRORLEVEL%