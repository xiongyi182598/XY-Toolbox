@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title XY工具箱 - 商业级纯净发布与更新包生成器

pushd "%~dp0"

echo ========================================================
echo   XY工具箱 · 商业级纯净发布与更新包自动化构建器
echo   功能: 固化正式内核 + 源码严格物理隔离 + 自动生成发布ZIP
echo ========================================================
echo.

:check_cad
tasklist /fi "imagename eq acad.exe" | find /i "acad.exe" >nul
if %errorlevel% equ 0 (
    echo [!] 检测到 AutoCAD 正在运行中...
    echo     为了确保核心 DLL 不被系统锁定并能顺利固化与打包，
    echo     请保存图纸后退出 AutoCAD。
    echo.
    echo 退出 CAD 后请按任意键继续...
    pause >nul
    goto check_cad
)

echo [*] AutoCAD 已关闭，正在执行发布构建流水线...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& {
    $ToolboxDir = Get-Location
    $stdDll = Join-Path $ToolboxDir 'XY_Toolbox.dll'
    
    # 1. 固化最新内核
    $vDlls = Get-ChildItem -Path $ToolboxDir -Filter 'XY_Toolbox_v*.dll' | Sort-Object LastWriteTime -Descending
    if ($vDlls -and $vDlls.Count -gt 0) {
        $latest = $vDlls[0]
        Write-Host ('  [+] 提取最新内核: ' + $latest.Name) -ForegroundColor Cyan
        Copy-Item -Path $latest.FullName -Destination $stdDll -Force
        Write-Host '  [OK] 已成功固化为官方标准 XY_Toolbox.dll' -ForegroundColor Green
        
        # 清理多余版本
        $delCount = 0
        $vDlls | ForEach-Object {
            Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            $delCount++
        }
        Write-Host ('  [-] 已清理 ' + $delCount + ' 个调试版本 (XY_Toolbox_v*.dll)') -ForegroundColor Yellow
    }
    
    # 2. 解除 Windows 安全区域锁定
    Get-ChildItem -Path $ToolboxDir -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
    Write-Host '  [OK] 已递归解除 Windows 跨机安全锁定 (Zone.Identifier)' -ForegroundColor Green

    # 3. 准备输出目录
    $outDir = Join-Path $ToolboxDir '发布成果物'
    if (Test-Path $outDir) { Remove-Item -Path $outDir -Recurse -Force }
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
    
    $tempStage = Join-Path $outDir 'XY_Toolbox_Release'
    New-Item -Path $tempStage -ItemType Directory -Force | Out-Null

    # 4. 白名单式复制运行时必需文件 (严密阻绝所有 .cs 源码与私有配置)
    $whiteListFiles = @(
        'XY_Toolbox.dll',
        'autoload.lsp',
        'shortcuts.lsp',
        'shortcuts.ini',
        'xy_block_erase.lsp',
        'cmd_config.ini',
        'version.json',
        'app_logo_white.png',
        'app_pure_transparent.ico',
        'install.ps1',
        'uninstall.ps1',
        '【一键安装】双击即可永久使用.bat',
        '【一键卸载】彻底清理还原.bat',
        '使用说明与快捷键速查.txt',
        'README.txt'
    )

    $copiedCount = 0
    foreach ($f in $whiteListFiles) {
        $src = Join-Path $ToolboxDir $f
        if (Test-Path $src) {
            Copy-Item -Path $src -Destination (Join-Path $tempStage $f) -Force
            $copiedCount++
        }
    }
    Write-Host ('  [OK] 成功打包 ' + $copiedCount + ' 个核心运行文件 (100% 隔离源码)') -ForegroundColor Green

    # 5. 生成供 GitHub Release 和 在线更新使用的纯净补丁包
    $updateZip = Join-Path $outDir 'XY_Toolbox_Update.zip'
    if (Test-Path $updateZip) { Remove-Item $updateZip -Force }
    Compress-Archive -Path (Join-Path $tempStage '*') -DestinationPath $updateZip -Force
    Write-Host '  [★] 成功生成在线升级包: 发布成果物\XY_Toolbox_Update.zip' -ForegroundColor Green

    # 6. 生成供初次分发给新用户的完整商业绿色版压缩包
    $fullZip = Join-Path $outDir 'XY_Toolbox_v1.0.1_完整商业独立版.zip'
    if (Test-Path $fullZip) { Remove-Item $fullZip -Force }
    Compress-Archive -Path $tempStage -DestinationPath $fullZip -Force
    Write-Host '  [★] 成功生成整套分发包: 发布成果物\XY_Toolbox_v1.0.1_完整商业独立版.zip' -ForegroundColor Green

    # 7. 清理中间临时暂存目录
    Remove-Item -Path $tempStage -Recurse -Force
}"

echo.
echo ========================================================
echo   ★ 恭喜！商业级发布构建已全部完成！★
echo ========================================================
echo   输出目录位于: 【发布成果物】 文件夹中
echo.
echo   【GitHub 在线发布与更新分发指引】:
echo   1. 打开您的 GitHub 仓库: https://github.com/xiongyi182598/XY-Toolbox
echo   2. 将根目录的 version.json 推送或更新至 main 分支
echo   3. 在仓库右侧点击 [Releases] -> [Create a new release]
echo      标签填 v1.0.1，将【XY_Toolbox_Update.zip】作为附件上传发布
echo   4. 全国所有用户启动 CAD 时将自动检测到更新并一键生效！
echo ========================================================
echo.
popd
pause
