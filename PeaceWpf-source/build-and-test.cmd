@echo off
rem English only. This is just a launcher; the real work is in build-test.ps1.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-test.ps1" %*
echo.
pause
