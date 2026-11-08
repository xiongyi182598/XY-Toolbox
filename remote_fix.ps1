# ========================================================
# XY工具箱 · 远端电脑全自动深度修复与一键洗白升级脚本
# ========================================================
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  ★ 正在为远端电脑执行 XY工具箱 v1.0.5 深度修复与极速升级 ★" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. 强制关闭 AutoCAD 解除 DLL 文件占用
Write-Host "[1/6] 检查并关闭 AutoCAD 进程以解除文件占用..." -ForegroundColor Yellow
taskkill /f /im acad.exe /t 2>$null | Out-Null
Start-Sleep -Seconds 1

# 2. 智能探测远端电脑安装目录
Write-Host "[2/6] 智能探测 XY工具箱 所在目录..." -ForegroundColor Yellow
$targetDir = $null

# 方案 A: 从注册表探测实际加载的 autoload.lsp 路径
try {
    $regRoots = @("HKCU:\Software\Autodesk\AutoCAD")
    foreach ($r in $regRoots) {
        if (Test-Path $r) {
            Get-ChildItem $r -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "Startup$" } | ForEach-Object {
                $prop = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
                foreach ($p in $prop.PSObject.Properties) {
                    if ($p.Value -is [string] -and $p.Value -like "*autoload.lsp*") {
                        $candidate = Split-Path -Parent $p.Value
                        if (Test-Path $candidate) {
                            $targetDir = $candidate
                            break
                        }
                    }
                }
            }
        }
    }
} catch { }

# 方案 B: 常用盘符探测
if (-not $targetDir) {
    $commonDirs = @(
        "D:\XY-Toolbox",
        "E:\XY-Toolbox",
        "C:\XY-Toolbox",
        "F:\XY-Toolbox",
        "$([Environment]::GetFolderPath('Desktop'))\XY-Toolbox",
        "$([Environment]::GetFolderPath('MyDocuments'))\XY-Toolbox"
    )
    foreach ($d in $commonDirs) {
        if (Test-Path "$d\autoload.lsp" -or (Test-Path "$d\XY_Toolbox.dll")) {
            $targetDir = $d
            break
        }
    }
}

if (-not $targetDir) {
    $targetDir = "D:\XY-Toolbox"
}

Write-Host "  -> 目标安装目录: $targetDir" -ForegroundColor Green
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# 3. 清理旧版本遗留的 cache 与 pending_update 死锁目录
Write-Host "[3/6] 清理旧版历史残留缓存与更新死锁目录..." -ForegroundColor Yellow
try {
    $cacheDir = Join-Path $targetDir "cache"
    if (Test-Path $cacheDir) { Remove-Item -Path $cacheDir -Recurse -Force -ErrorAction SilentlyContinue }
    $pendDir = Join-Path $targetDir "pending_update"
    if (Test-Path $pendDir) { Remove-Item -Path $pendDir -Recurse -Force -ErrorAction SilentlyContinue }
} catch { }

# 4. 从官方国内极速镜像下载最新 v1.0.5 完整更新包
Write-Host "[4/6] 正在下载最新官方正式版 v1.0.5 核心组件..." -ForegroundColor Yellow
$tempZip = Join-Path $env:TEMP ("XY_Update_" + [System.Guid]::NewGuid().ToString("N") + ".zip")
$urls = @(
    "https://ghfast.top/https://github.com/xiongyi182598/XY-Toolbox/releases/latest/download/XY_Toolbox_Update.zip",
    "https://mirror.ghproxy.com/https://github.com/xiongyi182598/XY-Toolbox/releases/latest/download/XY_Toolbox_Update.zip",
    "https://ghproxy.net/https://github.com/xiongyi182598/XY-Toolbox/releases/latest/download/XY_Toolbox_Update.zip",
    "https://github.com/xiongyi182598/XY-Toolbox/releases/latest/download/XY_Toolbox_Update.zip"
)

$downloadOk = $false
foreach ($u in $urls) {
    try {
        Write-Host "  -> 尝试连接节点: $u" -ForegroundColor Gray
        $wc = New-Object System.Net.WebClient
        $wc.Proxy = $null
        $wc.Headers.Add("User-Agent", "Mozilla/5.0 XY_Fixer/1.0")
        $wc.DownloadFile($u, $tempZip)
        if ((Test-Path $tempZip) -and ((Get-Item $tempZip).Length -gt 10240)) {
            $downloadOk = $true
            Write-Host "  [OK] 下载成功 (文件大小: $((Get-Item $tempZip).Length) 字节)" -ForegroundColor Green
            break
        }
    } catch {
        Write-Host "  [!] 当前节点重试中..." -ForegroundColor DarkGray
    }
}

if (-not $downloadOk) {
    Write-Host "[-] 下载失败，请检查网络连接！" -ForegroundColor Red
    return
}

# 5. 解压并覆盖到安装目录
Write-Host "[5/6] 正在执行全自动覆盖与文件解锁..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempExtract = Join-Path $env:TEMP ("XY_Extract_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $tempExtract)

Get-ChildItem -Path $tempExtract -Recurse | ForEach-Object {
    if (-not $_.PSIsContainer) {
        $rel = $_.FullName.Substring($tempExtract.Length).TrimStart('\', '/')
        $dest = Join-Path $targetDir $rel
        $destParent = Split-Path -Parent $dest
        if (-not (Test-Path $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
        Copy-Item -Path $_.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
    }
}

# 彻底解除 Windows 文件安全锁定
Get-ChildItem -Path $targetDir -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue

# 清理临时文件
Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

# 6. 执行本地原生一键安装注册
Write-Host "[6/6] 写入 AutoCAD 原生启动项与配置..." -ForegroundColor Yellow
$installScript = Join-Path $targetDir "install.ps1"
if (Test-Path $installScript) {
    & powershell.exe -ExecutionPolicy Bypass -File $installScript
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "★ 恭喜！远端电脑已成功升级为最新正式版 XY工具箱 v1.0.5！" -ForegroundColor Green
Write-Host "★ 核心升级成果：" -ForegroundColor Green
Write-Host "  1. 首次打开面板默认停靠吸附于 CAD 右侧，彻底记忆停靠习惯" -ForegroundColor White
Write-Host "  2. 智能输入法纯净隔离，彻底消除外部微信/浏览器污染" -ForegroundColor White
Write-Host "  3. 彻底修复历史在线更新死锁，后续升级全自动无感生效" -ForegroundColor White
Write-Host "========================================================" -ForegroundColor Green
Write-Host "您现在可以直接启动 AutoCAD 畅快使用了！" -ForegroundColor Cyan
