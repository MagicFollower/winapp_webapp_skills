@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0app"

set "OUT=%~dp0publish"
set "EXE=%OUT%\QuickPanel_self_contained.exe"

set "VER="
for /f "usebackq delims=" %%V in (`dotnet msbuild QuickPanel.csproj -getProperty:Version -nologo`) do if not defined VER set "VER=%%V"
if not defined VER (
  echo [publish] could not read Version from QuickPanel.csproj
  goto :fail
)

echo [1/3] publish single-file, SELF-CONTAINED (runtime bundled), win-x64
echo       code version: v!VER!
echo       target machine needs no .NET installed
echo       first run downloads the .NET WindowsDesktop runtime pack (about 150 MB) from nuget.org
echo       DebugType=embedded / GenerateDocumentationFile=false are global properties: they reach the
echo       referenced lib project too, so no stray StartUI4Controls.pdb / .xml is left next to the exe
dotnet publish QuickPanel.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:ArtifactLabel=self_contained ^
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
set "ART=%OUT%\QuickPanel_v!VER!_self_contained_build_!STAMP!.exe"
move /y "%EXE%" "%ART%" >nul
if errorlevel 1 (
  echo [publish] failed to rename "%EXE%" to "%ART%"
  goto :fail
)
for %%F in ("%ART%") do echo   %%~fF   %%~zF bytes
echo   files in publish\:
dir /b "%OUT%"

echo.
if not "!ST!"=="0" (
  echo WARNING: self-test reported failures, see %APPDATA%\QuickPanel\selftest.txt
)
goto :end

:fail
echo.
echo PUBLISH FAILED, dotnet exit code %ERRORLEVEL%

:end
echo.
pause
