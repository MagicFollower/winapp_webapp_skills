@echo off
setlocal
cd /d "%~dp0"

echo [1/3] build
dotnet build app\IndexApp.csproj --no-incremental
if errorlevel 1 exit /b 1

echo [2/3] engine tests
dotnet run --project tests\IndexCore.Tests\IndexCore.Tests.csproj
if errorlevel 1 exit /b 1

echo [3/3] StartUI4 selftest
app\bin\Debug\net10.0-windows\IndexApp.exe --selftest
set SELFTEST_EXIT=%errorlevel%
echo selftest_exit=%SELFTEST_EXIT%
exit /b %SELFTEST_EXIT%
