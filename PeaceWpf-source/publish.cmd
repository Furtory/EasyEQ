@echo off
chcp 65001 >nul
set DOTNET_CLI_UI_LANGUAGE=en
cd /d "%~dp0"

rem 一条命令出发布包：自包含发布 -> 7z 压缩 -> dist\EasyEQ.7z
set OUT=dist\EasyEQ
set PACK=dist\EasyEQ.7z
set SEVENZ="C:\Program Files\7-Zip\7z.exe"

if not exist %SEVENZ% (
    echo 找不到 7-Zip：%SEVENZ%
    echo 先装它：https://www.7-zip.org/
    pause
    exit /b 1
)

echo [1/3] 清理旧产物...
if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%PACK%" del /q "%PACK%"

echo [2/3] 自包含发布 win-x64...
dotnet publish src\PeaceWpf\PeaceWpf.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:PublishReadyToRun=false -o "%OUT%"
if errorlevel 1 goto fail

echo [3/3] 压成 7z...
pushd "%OUT%"
%SEVENZ% a -t7z -mx=9 -mmt=on "..\EasyEQ.7z" * >nul
if errorlevel 1 (popd & goto fail)
popd

echo.
echo 完成：%PACK%
for %%F in ("%PACK%") do echo 大小：%%~zF 字节
pause
exit /b 0

:fail
echo.
echo 失败，看上面的输出。
pause
exit /b 1
