@echo off
setlocal
title WordStudy - build portable exe
cd /d "%~dp0.."

rem ---- locate node.exe (PATH first, then the usual install folder) ----
set "NODE="
for /f "delims=" %%i in ('where node.exe 2^>nul') do if not defined NODE set "NODE=%%i"
if not defined NODE if exist "C:\Program Files\nodejs\node.exe" set "NODE=C:\Program Files\nodejs\node.exe"
if not defined NODE goto :nonode
for %%i in ("%NODE%") do set "NODEDIR=%%~dpi"
set "NPMCLI=%NODEDIR%node_modules\npm\bin\npm-cli.js"

rem ---- binary download mirrors; delete these 2 lines to use official sources ----
set "ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/"
set "ELECTRON_BUILDER_BINARIES_MIRROR=https://npmmirror.com/mirrors/electron-builder-binaries/"

echo.
echo [1/2] npm run build   (vite renderer + tsc main)
call "%NODE%" "%NPMCLI%" run build
if errorlevel 1 goto :fail

echo.
echo [2/2] electron-builder --win portable
call "node_modules\.bin\electron-builder.cmd" --win portable
if errorlevel 1 goto :fail

echo.
echo [OK] finished.
if exist "release\WordStudy_portable.exe" echo      %CD%\release\WordStudy_portable.exe

goto :end

:fail
echo.
echo [FAIL] build stopped with an error - scroll up for the tool output.
goto :end

:nonode
echo [FAIL] node.exe not found in PATH nor in "C:\Program Files\nodejs".
echo        Install Node.js first, or run this script from a terminal where node works.

:end
echo.
pause
endlocal
