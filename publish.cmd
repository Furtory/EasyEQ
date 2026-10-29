@echo off
chcp 65001 >nul
set DOTNET_CLI_UI_LANGUAGE=en
cd /d "%~dp0"

rem 一条命令出发布包：
rem   dist\EasyEQ.7z  —— 手动下载 / 压缩包分发
rem   dist\EasyEQ.zip —— 软件内更新用（程序自己解压，名字必须固定）
set OUT=dist\EasyEQ
set PACK7Z=dist\EasyEQ.7z
set PACKZIP=dist\EasyEQ.zip
set SEVENZ="C:\Program Files\7-Zip\7z.exe"

if not exist %SEVENZ% (
    echo 找不到 7-Zip：%SEVENZ%
    echo 先装它：https://www.7-zip.org/
    pause
    exit /b 1
)

echo [1/4] 清理旧产物...
if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%PACK7Z%" del /q "%PACK7Z%"
if exist "%PACKZIP%" del /q "%PACKZIP%"

echo [2/4] 自包含发布 win-x64...
dotnet publish src\PeaceWpf\PeaceWpf.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:PublishReadyToRun=false -o "%OUT%"
if errorlevel 1 goto fail

echo [3/4] 压成 7z...
pushd "%OUT%"
%SEVENZ% a -t7z -mx=9 -mmt=on "..\EasyEQ.7z" * >nul
if errorlevel 1 (popd & goto fail)

rem 更新包必须是 zip：程序用 .NET 自带的 ZipFile 解压，用户机器上不需要装 7-Zip。
echo [4/4] 压成 zip（软件内更新用）...
%SEVENZ% a -tzip -mx=9 -mmt=on "..\EasyEQ.zip" * >nul
if errorlevel 1 (popd & goto fail)
popd

echo.
echo 完成：%PACK7Z%
for %%F in ("%PACK7Z%") do echo   7z 大小：%%~zF 字节
echo 完成：%PACKZIP%
for %%F in ("%PACKZIP%") do echo   zip 大小：%%~zF 字节
pause
exit /b 0

:fail
echo.
echo 失败，看上面的输出。
pause
exit /b 1
