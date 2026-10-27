@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0app"

set "OUT=%~dp0publish_no_runtime"
set "EXE=%OUT%\IndexApp_no_runtime.exe"

set "VER="
for /f "usebackq delims=" %%V in (`dotnet msbuild IndexApp.csproj -getProperty:Version -nologo`) do if not defined VER set "VER=%%V"
if not defined VER (
  echo [publish] could not read Version from IndexApp.csproj
  goto :fail
)

echo [1/3] publish single-file, FRAMEWORK-DEPENDENT (no runtime bundled), win-x64
echo       code version: v!VER!
echo       small artifact, but the target machine MUST have the .NET 10 Desktop Runtime
echo       check a machine with:  dotnet --list-runtimes   (need Microsoft.WindowsDesktop.App 10.x)
echo       WPF native dlls are taken from that shared runtime at run time, nothing is extracted
dotnet publish IndexApp.csproj -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:ArtifactLabel=no_runtime ^
  -p:DebugType=embedded -p:GenerateDocumentationFile=false ^
  -o "%OUT%"
if errorlevel 1 goto :fail

echo.
echo [2/3] self-test of the packaged exe (exit code = failed assertion count)
start "" /wait "%EXE%" --selftest
set "ST=!ERRORLEVEL!"
echo selftest exit code: !ST!

echo.
echo [3/3] artifact
set "STAMP="
for /f "usebackq delims=" %%T in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMddHHmmss"`) do if not defined STAMP set "STAMP=%%T"
if not defined STAMP (
  echo [publish] could not create build timestamp
  goto :fail
)
set "ART=%OUT%\IndexApp_v!VER!_no_runtime_build_!STAMP!.exe"
move /y "%EXE%" "%ART%" >nul
if errorlevel 1 (
  echo [publish] failed to rename "%EXE%" to "%ART%"
  goto :fail
)
for %%F in ("%ART%") do echo   %%~fF   %%~zF bytes
echo   files in publish_no_runtime\:
dir /b "%OUT%"

echo.
if not "!ST!"=="0" (
  echo WARNING: self-test reported failures, see %APPDATA%\IndexApp\selftest.txt
)
goto :end

:fail
echo.
echo PUBLISH FAILED, dotnet exit code %ERRORLEVEL%
echo If the failure says the runtime pack could not be resolved, this machine lacks the win-x64
echo .NET 10 Desktop targeting/runtime pack - use publish.cmd (self-contained) instead.

:end
echo.
pause
