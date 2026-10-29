@echo off
chcp 65001 >nul
set DOTNET_CLI_UI_LANGUAGE=en
set VSLANG=1033
set DOTNET_NOLOGO=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1

cd /d "%~dp0"
set LOG=%~dp0build.log
set PROJECT=src\PeaceWpf\PeaceWpf.csproj

echo started > "%LOG%"

set DOTNET_EXE=
where dotnet >nul 2>&1
if not errorlevel 1 set DOTNET_EXE=dotnet
if not defined DOTNET_EXE if exist "%ProgramFiles%\dotnet\dotnet.exe" set DOTNET_EXE="%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_EXE goto end

echo dotnet-exe=%DOTNET_EXE% >> "%LOG%"
echo ---- sdks ---- >> "%LOG%"
%DOTNET_EXE% --list-sdks >> "%LOG%" 2>&1

echo ---- restore (may take minutes) ---- >> "%LOG%"
%DOTNET_EXE% restore "%PROJECT%" --verbosity minimal > "%~dp0restore.log" 2>&1
echo restore-exit=%ERRORLEVEL% >> "%LOG%"
type "%~dp0restore.log" >> "%LOG%"

echo ---- build ---- >> "%LOG%"
%DOTNET_EXE% build "%PROJECT%" -c Debug --no-restore --nologo -v minimal > "%~dp0build-out.log" 2>&1
echo build-exit=%ERRORLEVEL% >> "%LOG%"
type "%~dp0build-out.log" >> "%LOG%"

:end
echo ==== done ==== >> "%LOG%"
type "%LOG%"
pause
