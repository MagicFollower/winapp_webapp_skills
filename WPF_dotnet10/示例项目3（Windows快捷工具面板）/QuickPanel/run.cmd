@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [run] dotnet was not found in PATH.
    pause
    exit /b 1
)

echo [run] Building app\QuickPanel.csproj ...
dotnet build "app\QuickPanel.csproj" -nologo -v:minimal
if errorlevel 1 (
    echo.
    echo [run] Build failed. If QuickPanel is already running, close it and try again.
    pause
    exit /b 1
)

set "APP=%~dp0app\bin\Debug\net10.0-windows\QuickPanel.exe"
if not exist "%APP%" (
    echo [run] Built exe not found: %APP%
    pause
    exit /b 1
)

if /i "%~1"=="selftest" goto selftest
if /i "%~1"=="--selftest" goto selftest

start "" /d "%~dp0app\bin\Debug\net10.0-windows" "%APP%"
echo [run] QuickPanel launched from the latest Debug build.
exit /b 0

:selftest
echo [run] Running selftest ...
"%APP%" --selftest
set "RC=%ERRORLEVEL%"
echo [run] selftest exit=%RC%
if exist "%APPDATA%\QuickPanel\selftest.txt" type "%APPDATA%\QuickPanel\selftest.txt"
pause
exit /b %RC%
