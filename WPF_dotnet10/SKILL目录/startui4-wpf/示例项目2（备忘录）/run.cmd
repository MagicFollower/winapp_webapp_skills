@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [run] dotnet was not found in PATH.
    pause
    exit /b 1
)

set "PROJ="
for %%P in ("app\*.csproj") do if not defined PROJ set "PROJ=%%~fP"
if not defined PROJ (
    echo [run] app\*.csproj not found. Run this script from the project root.
    pause
    exit /b 1
)
for %%P in ("%PROJ%") do set "APPNAME=%%~nP"

echo [run] Building %PROJ% ...
dotnet build "%PROJ%" -nologo -v:minimal
if errorlevel 1 (
    echo.
    echo [run] Build failed. If the app is already running, close it and try again.
    pause
    exit /b 1
)

set "EXE=%~dp0app\bin\Debug\net10.0-windows\%APPNAME%.exe"
if not exist "%EXE%" (
    echo [run] Built exe not found: %EXE%
    pause
    exit /b 1
)

if /i "%~1"=="selftest" goto selftest
if /i "%~1"=="--selftest" goto selftest

start "" /d "%~dp0app\bin\Debug\net10.0-windows" "%EXE%"
echo [run] %APPNAME% launched from the latest Debug build.
exit /b 0

:selftest
echo [run] Running selftest ...
"%EXE%" --selftest
set "RC=%ERRORLEVEL%"
echo [run] selftest exit=%RC%
if exist "%APPDATA%\%APPNAME%\selftest.txt" type "%APPDATA%\%APPNAME%\selftest.txt"
pause
exit /b %RC%
