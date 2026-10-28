@echo off
chcp 936 >nul
title XY工具箱 - 商业发布包一键构建器

pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 商业级纯净发布与更新包一键生成
echo ========================================================
echo.

where python >nul 2>nul
if %errorlevel% equ 0 (
    python make_release.py
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_release.ps1"
)

echo.
popd
pause
