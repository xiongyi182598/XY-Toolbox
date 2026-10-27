@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title XY工具箱 - 官方原生绿色一键安装器

:: 自动解析网络共享路径 (UNC) 或本地路径为当前目录
pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 官方原生商业级一键安装器
echo ========================================================
echo.
echo [*] 正在执行全自动化安装部署，请稍候...
echo.

:: 1. 优先调用商业级 PowerShell 安装器 (支持全机穿透与深度注册)
if exist "install.ps1" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\install.ps1"
    if !errorlevel! equ 0 (
        goto finish
    )
)

:: 2. 降级保底机制 (极端受限机/PowerShell被禁用时自动激活 Autodesk 官方 Bundle 通道)
echo.
echo [!] 正在切换为官方标准 Bundle 绿色安装模式...

set "BUNDLE_DIR=%APPDATA%\Autodesk\ApplicationPlugins\XY_Toolbox.bundle"
if not exist "!BUNDLE_DIR!\Contents" mkdir "!BUNDLE_DIR!\Contents"

set "CUR_DIR=%CD%"
set "DIR_SLASH=!CUR_DIR:\=/!"

> "!BUNDLE_DIR!\PackageContents.xml" (
    echo ^<?xml version="1.0" encoding="utf-8"?^>
    echo ^<ApplicationPackage SchemaVersion="1.0" AutodeskProduct="AutoCAD" ProductType="Application" Name="XY_Toolbox" AppVersion="1.0.0" Description="XY工具箱商业级标准插件包" Author="XY"^>
    echo     ^<CompanyDetails Name="XY" /^>
    echo     ^<Components^>
    echo         ^<RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R19.0" SeriesMax="R26.0" /^>
    echo         ^<ComponentEntry AppName="XY_Toolbox_Loader" ModuleName="./Contents/loader.lsp" LoadOnAutoCADStartup="True" /^>
    echo     ^</Components^>
    echo ^</ApplicationPackage^>
)

> "!BUNDLE_DIR!\Contents\loader.lsp" (
    echo ;; XY_Toolbox Dual-Channel Loader
    echo (vl-load-com^)
    echo (if (getvar "SECURELOAD"^) (setvar "SECURELOAD" 0^)^)
    echo (vl-catch-all-apply
    echo   '(lambda ( / target ^)
    echo      (setq target "!DIR_SLASH!/autoload.lsp"^)
    echo      (if (findfile target^)
    echo        (load target^)
    echo      ^)
    echo    ^)
    echo ^)
)
echo   [OK] 官方 Bundle 绿色加载通道配置成功！

:finish
echo.
popd
pause
