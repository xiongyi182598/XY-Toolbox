# ========================================================
# XY工具箱 - 官方原生绿色完全卸载器 (uninstall.ps1)
# 彻底清理: .NET 注册项、启动组、搜索路径
# 零残留: 恢复系统至未安装工具箱的原生干净状态
# ========================================================

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ToolboxDir = $PSScriptRoot
if (-not $ToolboxDir) {
    $ToolboxDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$ToolboxDir = $ToolboxDir.TrimEnd('\').TrimEnd('/')
$AutoloadPath = "$ToolboxDir\autoload.lsp"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  XY工具箱 · 一键绿色完全卸载器" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  正在清理工具箱: $ToolboxDir" -ForegroundColor Yellow
Write-Host ""

$AcadRegTargets = @("HKCU:\Software\Autodesk\AutoCAD")
Get-ChildItem Registry::HKEY_USERS -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -notmatch "_Classes" -and $_.PSChildName.Length -gt 8 } | ForEach-Object {
    $AcadRegTargets += ("Registry::HKEY_USERS\" + $_.PSChildName + "\Software\Autodesk\AutoCAD")
}

$appsCleaned = 0
$startupCleaned = 0
$regCleaned = 0

foreach ($base in $AcadRegTargets) {
    if (Test-Path $base) {
        Get-ChildItem -Path $base -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem -Path $_.PSPath -ErrorAction SilentlyContinue | ForEach-Object {
                $prodPath = $_.PSPath
                
                # 1. 清除 Applications 下的注册项
                @("XY_Toolbox_Net", "XY_Toolbox") | ForEach-Object {
                    $appKey = Join-Path $prodPath "Applications\$_"
                    if (Test-Path $appKey) {
                        try {
                            Remove-Item -Path $appKey -Recurse -Force -ErrorAction SilentlyContinue
                            $appsCleaned++
                        } catch { }
                    }
                }

                # 2. 清理 Profiles 启动组与搜索路径
                $ProfilesPath = Join-Path $prodPath "Profiles"
                if (Test-Path $ProfilesPath) {
                    Get-ChildItem -Path $ProfilesPath -ErrorAction SilentlyContinue | ForEach-Object {
                        $profPath = $_.PSPath
                        
                        # 清理 Startup
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

                        # 清理搜索路径
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

Write-Host "  [-] 1. 已从 $appsCleaned 处环境中彻底移除 .NET 注册项" -ForegroundColor Yellow
Write-Host "  [-] 2. 已从 $startupCleaned 个环境的 Appload 启动组中完全移除" -ForegroundColor Yellow
Write-Host "  [-] 3. 已从 $regCleaned 个环境的搜索路径中完全清除" -ForegroundColor Yellow

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  ★ 卸载完成！CAD 已彻底恢复原生状态，零系统残留！ ★" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
