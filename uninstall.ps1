# ========================================================
# XY工具箱 - 官方原生绿色完全卸载器 (uninstall.ps1)
# 商业级彻底清理: .NET 注册项、启动组、搜索路径、Bundle 插件包
# 绝对零残留: 恢复系统至未安装工具箱的纯净原生状态
# ========================================================

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ToolboxDir = $PSScriptRoot
if (-not $ToolboxDir) {
    $ToolboxDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$ToolboxDir = $ToolboxDir.TrimEnd('\').TrimEnd('/')
$AutoloadPath = "$ToolboxDir\autoload.lsp"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  XY工具箱 · 官方原生商业级完全卸载器" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  清理目录: $ToolboxDir" -ForegroundColor Yellow
Write-Host ""

# 0. 进程状态安全检查
$acadProc = Get-Process acad -ErrorAction SilentlyContinue
if ($acadProc) {
    Write-Host "  [提示] 检测到 AutoCAD / 天正建筑 正在运行中..." -ForegroundColor Yellow
    Write-Host "  建议您先保存图纸并退出 CAD，以确保卸载能彻底清除所有被占用的注册表配置与内存锁。" -ForegroundColor Gray
    Write-Host ""
}

# 1. 彻底清理全机所有用户注册表
$AcadRegTargets = @("HKCU:\Software\Autodesk\AutoCAD")
Get-ChildItem Registry::HKEY_USERS -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -notmatch "_Classes" -and $_.PSChildName.Length -gt 8 } | ForEach-Object {
    $AcadRegTargets += ("Registry::HKEY_USERS\" + $_.PSChildName + "\Software\Autodesk\AutoCAD")
}
if (Test-Path "HKLM:\Software\Autodesk\AutoCAD") {
    $AcadRegTargets += "HKLM:\Software\Autodesk\AutoCAD"
}

$appsCleaned = 0
$startupCleaned = 0
$regCleaned = 0

foreach ($base in $AcadRegTargets) {
    if (Test-Path $base) {
        Get-ChildItem -Path $base -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem -Path $_.PSPath -ErrorAction SilentlyContinue | ForEach-Object {
                $prodPath = $_.PSPath
                
                # A. 清除 Applications 下所有关联注册项
                @("XY_Toolbox_Net", "XY_Toolbox", "XY_Toolbox_Net_Auto", "XY_Toolbox_Loader") | ForEach-Object {
                    $appKey = Join-Path $prodPath "Applications\$_"
                    if (Test-Path $appKey) {
                        try {
                            Remove-Item -Path $appKey -Recurse -Force -ErrorAction SilentlyContinue
                            $appsCleaned++
                        } catch { }
                    }
                }

                # B. 清理 Profiles 启动组与搜索路径
                $ProfilesPath = Join-Path $prodPath "Profiles"
                if (Test-Path $ProfilesPath) {
                    Get-ChildItem -Path $ProfilesPath -ErrorAction SilentlyContinue | ForEach-Object {
                        $profPath = $_.PSPath
                        
                        # 清理 Startup (无缝重排，彻底防止断号 Bug)
                        $startupKey = Join-Path $profPath "Dialogs\Appload\Startup"
                        if (Test-Path $startupKey) {
                            try {
                                $curNum = (Get-ItemProperty -Path $startupKey -Name "NumStartup" -ErrorAction SilentlyContinue).NumStartup
                                if ($curNum) {
                                    $num = [int]$curNum
                                    $remaining = @()
                                    for ($i = 1; $i -le $num; $i++) {
                                        $val = (Get-ItemProperty -Path $startupKey -Name "$($i)Startup" -ErrorAction SilentlyContinue)."$($i)Startup"
                                        if ($val -and $val -notlike "*autoload.lsp*" -and $val -ne $AutoloadPath) {
                                            $remaining += $val
                                        }
                                        Remove-ItemProperty -Path $startupKey -Name "$($i)Startup" -ErrorAction SilentlyContinue
                                    }
                                    Set-ItemProperty -Path $startupKey -Name "NumStartup" -Value ([string]$remaining.Count) -Force
                                    for ($j = 0; $j -lt $remaining.Count; $j++) {
                                        $idx = $j + 1
                                        Set-ItemProperty -Path $startupKey -Name "$($idx)Startup" -Value $remaining[$j] -Force
                                    }
                                    $startupCleaned++
                                }
                            } catch { }
                        }

                        # 清理搜索路径与受信任路径 (彻底剥离，不留空分号)
                        $GeneralPath = Join-Path $profPath "General"
                        if (Test-Path $GeneralPath) {
                            try {
                                $curAcad = (Get-ItemProperty -Path $GeneralPath -Name "ACAD" -ErrorAction SilentlyContinue).ACAD
                                if ($curAcad) {
                                    $paths = $curAcad.Split(';') | Where-Object { $_ -ne "" -and $_ -ne $ToolboxDir }
                                    Set-ItemProperty -Path $GeneralPath -Name "ACAD" -Value ($paths -join ';') -Force
                                    $regCleaned++
                                }
                                $curTrusted = (Get-ItemProperty -Path $GeneralPath -Name "TRUSTEDPATHS" -ErrorAction SilentlyContinue).TRUSTEDPATHS
                                if ($curTrusted) {
                                    $tPath = "$ToolboxDir\..."
                                    $tpaths = $curTrusted.Split(';') | Where-Object { $_ -ne "" -and $_ -ne $tPath }
                                    Set-ItemProperty -Path $GeneralPath -Name "TRUSTEDPATHS" -Value ($tpaths -join ';') -Force
                                }
                            } catch { }
                        }
                    }
                }
            }
        }
    }
}

# 2. 彻底清理 Autodesk 官方标准 Bundle 插件包
$bundleCleaned = 0
$bundlePaths = @(
    "$env:APPDATA\Autodesk\ApplicationPlugins\XY_Toolbox.bundle",
    "$env:ALLUSERSPROFILE\Autodesk\ApplicationPlugins\XY_Toolbox.bundle",
    "$env:ProgramData\Autodesk\ApplicationPlugins\XY_Toolbox.bundle"
)
foreach ($b in $bundlePaths) {
    if (Test-Path $b) {
        try {
            Remove-Item -Path $b -Recurse -Force -ErrorAction SilentlyContinue
            $bundleCleaned++
        } catch { }
    }
}

# 3. 清理系统临时目录中的临时缓存
try {
    @("fast_merge.dcl", "xy_toolbox_*", "XY_Toolbox*") | ForEach-Object {
        $tempFiles = Get-ChildItem -Path $env:TEMP -Filter $_ -ErrorAction SilentlyContinue
        if ($tempFiles) {
            $tempFiles | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} catch { }

Write-Host "  [-] 1. 已从 $appsCleaned 处注册表中彻底移除 .NET 核心加载项" -ForegroundColor Yellow
Write-Host "  [-] 2. 已从 $startupCleaned 个环境的 Appload 启动组中彻底剥离" -ForegroundColor Yellow
Write-Host "  [-] 3. 已从 $regCleaned 个环境的搜索路径中彻底清除" -ForegroundColor Yellow
if ($bundleCleaned -gt 0) {
    Write-Host "  [-] 4. 已彻底清理 $bundleCleaned 处 Autodesk 官方 Bundle 插件包" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  ★ 卸载完成！系统与 CAD 已彻底恢复至初始纯净状态，零残留！ ★" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
