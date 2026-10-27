@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title XY工具箱 · 一键安装器

:: 自动处理网络共享路径 (UNC) 或本地路径，锁定当前目录
pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 官方原生绿色一键安装器
echo ========================================================
echo.
echo [*] 正在执行安装配置，请稍候...
echo.

if not exist "install.ps1" (
    echo [错误] 未在当前目录找到 install.ps1 脚本！
    echo 当前运行路径: %CD%
    echo.
    goto failed
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\install.ps1"
if %errorlevel% neq 0 (
    echo.
    echo [警告] PowerShell 运行受限，正在为您切换为纯 CMD 原生部署...
    
    set "BUNDLE_DIR=%APPDATA%\Autodesk\ApplicationPlugins\XY_Toolbox.bundle"
    if not exist "!BUNDLE_DIR!\Contents" mkdir "!BUNDLE_DIR!\Contents"
    
    set "CUR_DIR=%CD%"
    set "DIR_SLASH=!CUR_DIR:\=/!"
    
    > "!BUNDLE_DIR!\PackageContents.xml" (
        echo ^<?xml version="1.0" encoding="utf-8"?^>
        echo ^<ApplicationPackage SchemaVersion="1.0" AutodeskProduct="AutoCAD" ProductType="Application" Name="XY_Toolbox" AppVersion="1.0.0" Description="XY工具箱深化提资自动化套件" Author="XY"^>
        echo     ^<CompanyDetails Name="XY" /^>
        echo     ^<Components^>
        echo         ^<RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R19.0" SeriesMax="R26.0" /^>
        echo         ^<ComponentEntry AppName="XY_Toolbox_Loader" ModuleName="./Contents/loader.lsp" LoadOnAutoCADStartup="True" /^>
        echo     ^</Components^>
        echo ^</ApplicationPackage^>
    )
    
    > "!BUNDLE_DIR!\Contents\loader.lsp" (
        echo ;; XY_Toolbox Loader
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
    echo   [OK] 纯 CMD 原生模式部署成功！
)

goto finish

:failed
echo ========================================================
echo   [!] 安装未能成功完成，请检查上述错误信息。
echo ========================================================
echo.

:finish
popd
pause
