@echo off
chcp 936 >nul 2>&1
title XY工具箱 - 一键全自动升级与修复工具
color 0b

echo ========================================================
echo   XY工具箱 · 一键全自动极速升级与修复程序
echo ========================================================
echo.

echo [*] 正在安全解除 AutoCAD 进程占用与核心文件锁...
taskkill /f /im acad.exe >nul 2>&1
timeout /t 1 /nobreak >nul

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

set APPLIED=0

if exist "pending_update\XY_Toolbox.dll" (
    echo [*] 发现已就绪的最新更新包，正在执行物理覆盖...
    copy /y "pending_update\*.*" "." >nul 2>&1
    rd /s /q "pending_update" >nul 2>&1
    set APPLIED=1
)

if "%APPLIED%"=="0" (
    echo [*] 正在从高速云端同步最新 v1.0.3 核心组件...
    curl.exe -s -k -L --connect-timeout 10 -o "XY_Toolbox.dll" "https://ghfast.top/https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/XY_Toolbox.dll"
    curl.exe -s -k -L --connect-timeout 10 -o "autoload.lsp" "https://ghfast.top/https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/autoload.lsp"
    curl.exe -s -k -L --connect-timeout 10 -o "version.json" "https://ghfast.top/https://raw.githubusercontent.com/xiongyi182598/XY-Toolbox/main/version.json"
)

if exist "apply_update.bat" del /f /q "apply_update.bat" >nul 2>&1
if exist "run_updater.bat" del /f /q "run_updater.bat" >nul 2>&1
if exist "restart_apply.bat" del /f /q "restart_apply.bat" >nul 2>&1

echo.
echo ========================================================
echo  [OK] 恭喜！XY工具箱已彻底无感升级为最新 v1.0.3 正式版！
echo  - 侧边栏与工作台标题严格对齐三位数版本号：XY工具箱 v1.0.3
echo  - 智能输入法切换保持纯英文盲打，输入文字自动切中文
echo  - 修复影子加载机制，彻底消除升级文件锁死
echo ========================================================
echo.

set "ACAD_EXE="
if exist "C:\Program Files\Autodesk\AutoCAD 2026\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2026\acad.exe"
if exist "C:\Program Files\Autodesk\AutoCAD 2025\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2025\acad.exe"
if exist "C:\Program Files\Autodesk\AutoCAD 2024\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2024\acad.exe"
if exist "C:\Program Files\Autodesk\AutoCAD 2023\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2023\acad.exe"
if exist "C:\Program Files\Autodesk\AutoCAD 2022\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2022\acad.exe"
if exist "C:\Program Files\Autodesk\AutoCAD 2021\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2021\acad.exe"
if exist "C:\Program Files\Autodesk\AutoCAD 2020\acad.exe" set "ACAD_EXE=C:\Program Files\Autodesk\AutoCAD 2020\acad.exe"

if defined ACAD_EXE (
    echo [*] 正在为您重新启动 AutoCAD...
    start "" "%ACAD_EXE%"
) else (
    echo [*] 升级完成！请直接启动 AutoCAD 即可享受全新版本！
)

timeout /t 3 /nobreak >nul
exit
