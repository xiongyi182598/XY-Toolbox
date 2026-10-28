@echo off
chcp 936 >nul
title XY工具箱 - GitHub 代码与发布包一键推送器

pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 一键同步推送到 GitHub 远程仓库
echo   目标仓库: https://github.com/xiongyi182598/XY-Toolbox
echo ========================================================
echo.

echo [*] 正在检查本地分支状态...
git branch -M main

echo [*] 正在尝试直连推送至 GitHub (main 分支)...
git push -u origin main

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo   [OK] ★ 恭喜！代码与版本已 100%% 成功推送到 GitHub！
    echo ========================================================
    echo   请刷新您的 GitHub 页面查看:
    echo   https://github.com/xiongyi182598/XY-Toolbox
    echo.
    goto finish
)

echo.
echo [!] 直连推送超时或失败，正在尝试通过本地代理 (10808) 重试推送...
git config --local http.proxy socks5://127.0.0.1:10808
git config --local https.proxy socks5://127.0.0.1:10808
git push -u origin main

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo   [OK] ★ 通过代理成功推送到 GitHub！
    echo ========================================================
    echo   请刷新您的 GitHub 页面查看:
    echo   https://github.com/xiongyi182598/XY-Toolbox
    echo.
    git config --local --unset http.proxy
    git config --local --unset https.proxy
    goto finish
)

echo.
echo [!] 提示: 推送时如果弹出 GitHub 登录网页窗口，请在浏览器中点击【Authorize】授权即可！
echo.

:finish
popd
pause
