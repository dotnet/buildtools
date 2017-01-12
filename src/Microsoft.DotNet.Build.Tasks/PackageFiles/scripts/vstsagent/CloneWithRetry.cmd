@echo off

if "%1" == ""   goto :usage
if "%2" == ""   goto :usage
goto :main

:usage
echo Usage: %~n0 [repository url] [destination folder]
echo.
echo Clones a git repo using the specified URL with depth 1 (for performance), 
echo retrying up to 10x if there is a failure.
goto :eof

:main
set REPO_URL=%1
set FOLDER_NAME=%2
set /a RETRY_COUNT=10
set /a SLEEP_TIME=10

IF NOT DEFINED GIT SET "GIT=%PROGRAMFILES(X86)%\Git\cmd\git.exe"

:while
call "%GIT%" clone %REPO_URL% %FOLDER_NAME%
if %ERRORLEVEL% EQU 0 goto :done

:failed
set /a RETRY_COUNT-=1
echo Clone failed.  Removing %FOLDER_NAME% in an attempt to get clone working, will retry in ~%SLEEP_TIME% seconds.
rd /s /q %FOLDER_NAME%
ping -n %SLEEP_TIME% 127.0.0.1 > NUL

if %RETRY_COUNT% EQU 0 goto :done
goto :while

:done
