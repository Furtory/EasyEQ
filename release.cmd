@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

rem ============================================================
rem  release.cmd [version]
rem
rem  Writes the version into the csproj, publishes self-contained
rem  win-x64, then builds dist\EasyEQ.7z and dist\EasyEQ.zip.
rem  It does NOT commit to git and does NOT back up anything.
rem
rem  Usage:
rem    release.cmd          - prompts for the version
rem    release.cmd 1.0.2    - version given on the command line
rem ============================================================

set CSPROJ=src\PeaceWpf\PeaceWpf.csproj
set OUT=dist\EasyEQ
set PACK7Z=dist\EasyEQ.7z
set PACKZIP=dist\EasyEQ.zip
set SEVENZ=C:\Program Files\7-Zip\7z.exe
set DOTNET_CLI_UI_LANGUAGE=en

set VER=%~1
if "%VER%"=="" (
    set /p VER=Version ^(no v prefix, e.g. 1.0.2^): 
)
if "%VER%"=="" (
    echo No version entered. Aborted.
    pause
    exit /b 1
)

rem Tolerate a stray "v" prefix.
if /i "!VER:~0,1!"=="v" set VER=!VER:~1!

echo.
echo [1/5] Checking version and writing it into the csproj ...
rem 版本改写放在 tools\set-version.ps1：cmd 会把参数里的尖括号当成重定向，内联 PowerShell 传不进去。
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\set-version.ps1" "!VER!"
if errorlevel 4 (
    echo No Version tag found in the csproj. Nothing changed.
    pause
    exit /b 1
)
if errorlevel 3 (
    echo Invalid version: !VER!
    echo Use 1.0 / 1.0.2 / 1.0.2.3 - digits and dots only.
    pause
    exit /b 1
)
if errorlevel 1 (
    echo Failed to write the csproj.
    pause
    exit /b 1
)

if not exist "%SEVENZ%" (
    echo 7-Zip not found: %SEVENZ%
    echo Install it first: https://www.7-zip.org/
    pause
    exit /b 1
)

echo [2/5] Cleaning old artifacts ...
if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%PACK7Z%" del /q "%PACK7Z%"
if exist "%PACKZIP%" del /q "%PACKZIP%"

echo [3/5] Publishing self-contained win-x64 (Release) ...
dotnet publish "%CSPROJ%" -c Release -r win-x64 --self-contained true -p:DebugType=none -p:PublishReadyToRun=false -o "%OUT%"
if errorlevel 1 goto fail
if not exist "%OUT%\EasyEQ.exe" goto fail

echo [4/5] Packing 7z and the update zip ...
pushd "%OUT%"
"%SEVENZ%" a -t7z -mx=9 -mmt=on "..\EasyEQ.7z" * >nul
if errorlevel 1 (popd & goto fail)
"%SEVENZ%" a -tzip -mx=9 -mmt=on "..\EasyEQ.zip" * >nul
if errorlevel 1 (popd & goto fail)
popd

echo [5/5] Verifying the built version ...
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\check-version.ps1" "!VER!" "dist\EasyEQ\EasyEQ.exe"
if errorlevel 6 (
    echo The published exe is missing.
    goto fail
)
if errorlevel 5 (
    echo The built exe reports a different version than !VER!. Do not upload it.
    goto fail
)

echo.
echo ============================================================
echo  Release artifacts ready (version !VER!)
echo ============================================================
for %%F in ("%PACK7Z%") do echo   %PACK7Z%   %%~zF bytes
for %%F in ("%PACKZIP%") do echo   %PACKZIP%   %%~zF bytes
echo.
echo Manual steps left (the script does not do these):
echo   1. GitHub - Releases - Draft a new release
echo   2. Tag: V!VER!   Title: V!VER!
echo   3. Upload both files. The name EasyEQ.zip must stay exactly as is.
echo   4. Commit the csproj version change.
echo.
pause
exit /b 0

:fail
echo.
echo FAILED. See the output above.
pause
exit /b 1
