@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title XY工具箱 - 官方原生绿色完全卸载器

pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 官方原生绿色完全卸载器
echo ========================================================
echo.
echo [*] 正在彻底清理工具箱环境与注册项，请稍候...
echo.

:: 1. 优先调用 PowerShell 深度卸载器
if exist "uninstall.ps1" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\uninstall.ps1"
)

:: 2. 双重保障：无条件清理任何 Autodesk 官方 Bundle 插件包
set "BUNDLE_DIR=%APPDATA%\Autodesk\ApplicationPlugins\XY_Toolbox.bundle"
if exist "!BUNDLE_DIR!" (
    rd /s /q "!BUNDLE_DIR!" 2>nul
    echo   [-] 已彻底移除官方插件包: !BUNDLE_DIR!
)

set "ALL_BUNDLE=%ALLUSERSPROFILE%\Autodesk\ApplicationPlugins\XY_Toolbox.bundle"
if exist "!ALL_BUNDLE!" (
    rd /s /q "!ALL_BUNDLE!" 2>nul
    echo   [-] 已彻底移除系统级插件包: !ALL_BUNDLE!
)

echo.
echo ========================================================
echo   ★ 卸载完成！系统与 CAD 已完全恢复干净状态！ ★
echo ========================================================
echo.

popd
pause
