# ========================================================
# XY工具箱 - 官方原生绿色一键安装器 (install.ps1)
# 官方标准 .NET 注册架构 (MANAGED=1, LOADCTRLS=2)
# 零冲突: 完美支持 AutoCAD (2020-2026) / 天正建筑 (T20/T30 全系列) / 海龙工具箱
# ========================================================

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ToolboxDir = $PSScriptRoot
if (-not $ToolboxDir) {
    $ToolboxDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$ToolboxDir = $ToolboxDir.TrimEnd('\').TrimEnd('/')
$DllPath = "$ToolboxDir\XY_Toolbox.dll"
$AutoloadPath = "$ToolboxDir\autoload.lsp"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  XY工具箱 · 官方原生绿色一键安装器 (全环境自动挂接版)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  当前工具箱物理路径: $ToolboxDir" -ForegroundColor Yellow
Write-Host ""

# 0. 自动解除 Windows 跨电脑拷贝安全区域锁定 (解除 DLL 阻止)
try {
    Get-ChildItem -Path $ToolboxDir -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
    Write-Host "  [OK] 0. 已解除 Windows 文件安全区域锁定 (DLL/LSP 已全部解禁)" -ForegroundColor Green
} catch {
    Write-Host "  [!] 解锁文件提示: $_" -ForegroundColor Gray
}

# 0.1 自动同步最新的编译版本库为标准 XY_Toolbox.dll
try {
    $latestVerDll = Get-ChildItem -Path $ToolboxDir -Filter "XY_Toolbox_v*.dll" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestVerDll -and (Test-Path $latestVerDll.FullName)) {
        if ((-not (Test-Path $DllPath)) -or ($latestVerDll.LastWriteTime -gt (Get-Item $DllPath).LastWriteTime)) {
            Copy-Item -Path $latestVerDll.FullName -Destination $DllPath -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] 0.1 已自动将最新编译内核 ($($latestVerDll.Name)) 部署为标准 XY_Toolbox.dll" -ForegroundColor Green
        }
    }
} catch { }

# 1. 穿透全机所有用户注册表，向 Applications\XY_Toolbox_Net 写入官方标准托管 .NET 启动项
$AcadRegTargets = @("HKCU:\Software\Autodesk\AutoCAD")
Get-ChildItem Registry::HKEY_USERS -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -notmatch "_Classes" -and $_.PSChildName.Length -gt 8 } | ForEach-Object {
    $AcadRegTargets += ("Registry::HKEY_USERS\" + $_.PSChildName + "\Software\Autodesk\AutoCAD")
}

$netAppsInstalled = 0
$startupUpdated = 0
$pathsUpdated = 0

foreach ($base in $AcadRegTargets) {
    if (Test-Path $base) {
        Get-ChildItem -Path $base -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem -Path $_.PSPath -ErrorAction SilentlyContinue | ForEach-Object {
                $prodPath = $_.PSPath
                
                # A. 写入 Applications\XY_Toolbox_Net (最核心：打开图纸时由 AutoCAD 原生 CLR 自动加载)
                $appKey = Join-Path $prodPath "Applications\XY_Toolbox_Net"
                try {
                    if (-not (Test-Path $appKey)) {
                        New-Item -ItemType Directory -Path $appKey -Force -ErrorAction SilentlyContinue | Out-Null
                    }
                    if (Test-Path $appKey) {
                        Set-ItemProperty -Path $appKey -Name "DESCRIPTION" -Value "XY_Toolbox_Net" -Type String -Force
                        Set-ItemProperty -Path $appKey -Name "LOADER" -Value $DllPath -Type String -Force
                        Set-ItemProperty -Path $appKey -Name "LOADCTRLS" -Value 2 -Type DWord -Force
                        Set-ItemProperty -Path $appKey -Name "MANAGED" -Value 1 -Type DWord -Force
                        $netAppsInstalled++
                    }
                } catch { }

                # B. 穿透挂接 Profiles (天正建筑与 AutoCAD 原生配置)
                $ProfilesPath = Join-Path $prodPath "Profiles"
                if (Test-Path $ProfilesPath) {
                    Get-ChildItem -Path $ProfilesPath -ErrorAction SilentlyContinue | ForEach-Object {
                        $profPath = $_.PSPath
                        
                        # 挂接 Startup 启动组 (双重保障)
                        $startupKey = Join-Path $profPath "Dialogs\Appload\Startup"
                        if (-not (Test-Path $startupKey)) {
                            New-Item -ItemType Directory -Path $startupKey -Force -ErrorAction SilentlyContinue | Out-Null
                        }
                        if (Test-Path $startupKey) {
                            try {
                                $curNum = (Get-ItemProperty -Path $startupKey -Name "NumStartup" -ErrorAction SilentlyContinue).NumStartup
                                if (-not $curNum) { $curNum = 0 }
                                $curNum = [int]$curNum
                                $hasLsp = $false
                                for ($i = 1; $i -le $curNum; $i++) {
                                    $val = (Get-ItemProperty -Path $startupKey -Name "$($i)Startup" -ErrorAction SilentlyContinue)."$($i)Startup"
                                    if ($val -like "*autoload.lsp*") {
                                        Set-ItemProperty -Path $startupKey -Name "$($i)Startup" -Value $AutoloadPath -Force
                                        $hasLsp = $true
                                        break
                                    }
                                }
                                if (-not $hasLsp) {
                                    $newIdx = $curNum + 1
                                    Set-ItemProperty -Path $startupKey -Name "$($newIdx)Startup" -Value $AutoloadPath -Force
                                    Set-ItemProperty -Path $startupKey -Name "NumStartup" -Value ([string]$newIdx) -Force
                                    $startupUpdated++
                                } else {
                                    $startupUpdated++
                                }
                            } catch { }
                        }

                        # 挂接 Support 搜索路径与受信任路径 (三重保障)
                        $GeneralPath = Join-Path $profPath "General"
                        if (Test-Path $GeneralPath) {
                            try {
                                $curAcad = (Get-ItemProperty -Path $GeneralPath -Name "ACAD" -ErrorAction SilentlyContinue).ACAD
                                if ($curAcad) {
                                    $paths = $curAcad.Split(';') | Where-Object { $_ -ne "" }
                                    if (-not ($paths -contains $ToolboxDir)) {
                                        $newAcad = ($paths + $ToolboxDir) -join ';'
                                        Set-ItemProperty -Path $GeneralPath -Name "ACAD" -Value $newAcad -Force
                                        $pathsUpdated++
                                    } else {
                                        $pathsUpdated++
                                    }
                                }
                                $curTrusted = (Get-ItemProperty -Path $GeneralPath -Name "TRUSTEDPATHS" -ErrorAction SilentlyContinue).TRUSTEDPATHS
                                if ($curTrusted) {
                                    $tPath = "$ToolboxDir\..."
                                    $tpaths = $curTrusted.Split(';') | Where-Object { $_ -ne "" }
                                    if (-not ($tpaths -contains $tPath)) {
                                        $newTrusted = ($tpaths + $tPath) -join ';'
                                        Set-ItemProperty -Path $GeneralPath -Name "TRUSTEDPATHS" -Value $newTrusted -Force
                                    }
                                }
                            } catch { }
                        }
                    }
                }
            }
        }
    }
}

Write-Host "  [OK] 1. 原生 .NET 核心加载器已注入 (已关联 $netAppsInstalled 处 AutoCAD/天正核心引擎)" -ForegroundColor Green
Write-Host "  [OK] 2. 原生 Appload 启动组已挂接 (已保障 $startupUpdated 个 AutoCAD/天正配置)" -ForegroundColor Green
Write-Host "  [OK] 3. Support 搜索路径与受信任路径已挂接 (已关联 $pathsUpdated 个配置环境)" -ForegroundColor Green

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  ★ 恭喜！XY工具箱已成功安装并永久挂接就绪！ ★" -ForegroundColor Green
Write-Host ""
Write-Host "  【温馨提示】:" -ForegroundColor White
Write-Host "  打开 AutoCAD 或 天正建筑，进入图纸即可直接使用！" -ForegroundColor Yellow
Write-Host "  ★ 免输入快捷安装法: 直接把文件夹内的 autoload.lsp 拖入 CAD 绘图区亦可秒级就绪！" -ForegroundColor Cyan
Write-Host ""
Write-Host "  【常用快捷键与功能】:" -ForegroundColor White
Write-Host "  - XGJ       : 打开/隐藏侧边工作台" -ForegroundColor Gray
Write-Host "  - XYCOMPARE : 图层与线型规范深度对比与智能合并系统 (LYCOMPARE / XQTC)" -ForegroundColor Yellow
Write-Host "  - XDJ       : 穿透冻结图层 (支持嵌套块/视口冻结/U微步撤销)" -ForegroundColor Gray
Write-Host "  - XJD       : 逐步解冻图层 (按冻结顺序逐步恢复)" -ForegroundColor Gray
Write-Host "  - XHF       : 一键恢复图层 (一键全部解冻)" -ForegroundColor Gray
Write-Host "  - XJH       : 极速图层合并 (免弹窗秒级点选合并，绝不冲突海龙)" -ForegroundColor Gray
Write-Host "  - XHB       : 图层规范合并器面板" -ForegroundColor Gray
Write-Host "  - XSY       : 属性块文字独立穿透平移" -ForegroundColor Gray
Write-Host "  - XGL       : 图块一键归0层随层" -ForegroundColor Gray
Write-Host "  - XKJ       : 自定义快捷键配置面板" -ForegroundColor Gray
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
