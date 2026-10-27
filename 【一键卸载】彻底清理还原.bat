@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title XY工具箱 · 一键完全卸载器

pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 一键完全卸载器
echo ========================================================
echo.
echo [*] 正在彻底清理工具箱，请稍候...
echo.

if exist "uninstall.ps1" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\uninstall.ps1"
) else (
    set "BUNDLE_DIR=%APPDATA%\Autodesk\ApplicationPlugins\XY_Toolbox.bundle"
    if exist "!BUNDLE_DIR!" (
        rd /s /q "!BUNDLE_DIR!"
        echo   [-] 成功删除官方插件包: !BUNDLE_DIR!
    )
)

popd
pause
