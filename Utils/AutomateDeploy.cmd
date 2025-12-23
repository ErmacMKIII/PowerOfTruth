@echo off
REM --------------------------------------------------------
REM AutomateDeploy.cmd - Enhanced Script for Deployment
REM --------------------------------------------------------

setlocal enabledelayedexpansion

REM Debugging information
echo Current Directory: %cd%
echo Script Location: %~dp0
echo Checking prerequisites...
dotnet --info || (
    echo .NET SDK is not installed or not in PATH.
    pause
    exit /b 1
)

REM Check if WebServer directory exists
if exist "%~dp0WebServer" (
    echo WebServer directory already exists. Skipping extraction.
) else (
    echo WebServer directory not found. Extracting WebServer.zip...
    powershell -Command "Expand-Archive -Path '%~dp0WebServer.zip' -DestinationPath '%~dp0' -Force" || (
        echo Failed to extract WebServer.zip
        pause
        exit /b 1
    )
)

REM Check if WebClient directory exists
if exist "%~dp0WebClient" (
    echo WebClient directory already exists. Skipping extraction.
) else (
    echo WebClient directory not found. Extracting WebClient.zip...
    powershell -Command "Expand-Archive -Path '%~dp0WebClient.zip' -DestinationPath '%~dp0' -Force" || (
        echo Failed to extract WebClient.zip
        pause
        exit /b 1
    )
)

REM Start WebServer in a new command window
echo Starting WebServer in a new Command Prompt...
if exist "%~dp0WebServer\WebServer.dll" (
    start cmd /k "cd /d %~dp0WebServer && set ASPNETCORE_URLS=http://localhost:5000;https://localhost:44343 && dotnet WebServer.dll"
) else (
    echo WebServer.dll not found. Ensure the file exists in the WebServer directory.
    pause
    exit /b 1
)

REM Start WebClient in a new command window
echo Starting WebClient in a new Command Prompt...
if exist "%~dp0WebClient\WebClient.dll" (
    start cmd /k "cd /d %~dp0WebClient && set ASPNETCORE_URLS=http://localhost:5001;https://localhost:44300 && dotnet WebClient.dll"
) else (
    echo WebClient.dll not found. Ensure the file exists in the WebClient directory.
    pause
    exit /b 1
)

REM --------------------------------------------------------
REM End of script
REM --------------------------------------------------------
pause
