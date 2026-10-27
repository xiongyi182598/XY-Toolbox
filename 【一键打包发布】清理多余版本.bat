@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title XY工具箱 - 商业级纯净绿色版打包发布整理工具

pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 商业级发布包一键整理工具
echo   功能: 固化最新内核为 XY_Toolbox.dll，清理历史调试冗余
echo ========================================================
echo.

:check_cad
tasklist /fi "imagename eq acad.exe" | find /i "acad.exe" >nul
if %errorlevel% equ 0 (
    echo [!] 检测到 AutoCAD 正在运行中...
    echo     系统正锁定核心动态库，请先保存图纸并退出 AutoCAD。
    echo.
    echo 退出 CAD 后，请按任意键继续...
    pause >nul
    goto check_cad
)

echo [*] AutoCAD 已关闭，正在执行发布包自动化整理与纯净化...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& {
    `$ToolboxDir = Get-Location
    `$stdDll = Join-Path `$ToolboxDir 'XY_Toolbox.dll'
    
    # 1. 寻找版本号最高的增量内核
    `$vDlls = Get-ChildItem -Path `$ToolboxDir -Filter 'XY_Toolbox_v*.dll' | Sort-Object LastWriteTime -Descending
    if (`$vDlls -and `$vDlls.Count -gt 0) {
        `$latest = `$vDlls[0]
        Write-Host ('  [+] 发现最新编译内核: ' + `$latest.Name) -ForegroundColor Cyan
        Copy-Item -Path `$latest.FullName -Destination `$stdDll -Force
        Write-Host '  [OK] 已成功将最新内核固化并部署为标准 XY_Toolbox.dll' -ForegroundColor Green
        
        # 2. 清理所有历史调试增量库
        `$delCount = 0
        `$vDlls | ForEach-Object {
            Remove-Item -Path `$_.FullName -Force -ErrorAction SilentlyContinue
            `$delCount++
        }
        Write-Host ('  [-] 已清理 ' + `$delCount + ' 个开发调试增量库文件 (XY_Toolbox_v*.dll)') -ForegroundColor Yellow
    } else {
        Write-Host '  [OK] 当前标准库 XY_Toolbox.dll 已处于纯净状态' -ForegroundColor Green
    }
    
    # 3. 解除 Windows 跨机安全区域锁定
    Get-ChildItem -Path `$ToolboxDir -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
    Write-Host '  [OK] 已递归解除整个目录的 Windows 安全锁定 (Zone.Identifier)' -ForegroundColor Green
}"

echo.
echo ========================================================
echo   ★ 恭喜！发布包整理完毕！
echo   当前文件夹已纯净化为零冗余的【商业级纯净独立绿色版】，
echo   您可以直接将此文件夹打包压缩 (ZIP)，发给任何电脑安装使用！
echo ========================================================
echo.
popd
pause
