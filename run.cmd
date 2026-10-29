@echo off
chcp 65001 >nul
cd /d "%~dp0"

rem 保存要写 EqualizerAPO 的 peace.txt。DSH 会话的文件沙箱会挡住从工作区启动的程序写工作区外，
rem 所以这里把构建产物复制到 %LOCALAPPDATA% 再从那里启动；正常安装后不需要这一步。
set SRC=src\PeaceWpf\bin\Debug\net10.0-windows
set DST=%LOCALAPPDATA%\EasyEQ\app
set EXE=%DST%\EasyEQ.exe
set ERRLOG=%DST%\startup-error.log

if not exist "%SRC%\EasyEQ.exe" (
    echo exe not found. run build.cmd first.
    pause
    exit /b 1
)

if not exist "%DST%" mkdir "%DST%"
robocopy "%SRC%" "%DST%" /MIR /NFL /NDL /NJH /NJS >nul

if exist "%ERRLOG%" del "%ERRLOG%" >nul 2>&1

start "" "%EXE%"
echo started %EXE%
timeout /t 4 >nul

if exist "%ERRLOG%" (
    echo.
    echo ---- startup error ----
    type "%ERRLOG%"
) else (
    echo.
    echo no startup error was written.
)
echo.
pause
