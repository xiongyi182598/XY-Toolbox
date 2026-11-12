$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$PSScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $PSScriptDir) { $PSScriptDir = (Get-Location).Path }

$candidateDirs = @(
    "$PSScriptDir\packages\acad2018",
    'D:\AUTO-CAD2024\AutoCAD 2024',
    'C:\Program Files\Autodesk\AutoCAD 2024',
    'D:\Program Files\Autodesk\AutoCAD 2024'
)
$acadDir = $null
foreach ($dir in $candidateDirs) {
    if (Test-Path "$dir\accoremgd.dll") {
        $acadDir = $dir
        break
    }
}
if (-not $acadDir) {
    Write-Host "AutoCAD dependency directory not found!" -ForegroundColor Red
    exit 1
}
Write-Host "Using AutoCAD reference directory: $acadDir"

$refs = @(
    "/reference:$acadDir\accoremgd.dll",
    "/reference:$acadDir\acdbmgd.dll",
    "/reference:$acadDir\acmgd.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Data.dll",
    "/reference:System.Xml.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/reference:System.Xaml.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll",
    "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll"
)

$sources = @(
    "XY_Toolbox.cs",
    "ViewportLayerForm.cs",
    "UpdateService.cs",
    "LayerCompareForm.cs",
    "LayerCompareEngine.cs",
    "LayerDeepMergeService.cs",
    "HailongService.cs",
    "LayerMappingWorkbench\Models\LayerMapRule.cs",
    "LayerMappingWorkbench\Models\LayerCompareItem.cs",
    "LayerMappingWorkbench\Services\LayerAlignmentEngine.cs",
    "LayerMappingWorkbench\Services\LayerMergeService.cs",
    "LayerMappingWorkbench\Services\ViewportCloneService.cs",
    "LayerMappingWorkbench\ViewModels\LayerMappingViewModel.cs",
    "LayerMappingWorkbench\Views\LayerMappingView.cs",
    "LayerMappingWorkbench\Views\ViewportCloneDialog.cs",
    "LayerMappingWorkbench\Commands\ExtensionCommands.cs"
)

$argsList = @("/target:library", "/platform:x64", "/codepage:65001", "/out:XY_Toolbox.dll") + $refs + $sources

Write-Host "Compiling XY_Toolbox.dll..."
& $csc $argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build success! Output: XY_Toolbox.dll" -ForegroundColor Green
    $existing = Get-ChildItem -Path "XY_Toolbox_*.dll" -ErrorAction SilentlyContinue | Where-Object { $_.BaseName -match '^XY_Toolbox_(\d+)$' } | ForEach-Object { [int]$Matches[1] }
    $startVer = 119
    if ($existing) {
        $maxVer = ($existing | Measure-Object -Maximum).Maximum
        $startVer = $maxVer + 1
    }
    for ($i = $startVer; $i -le ($startVer + 50); $i++) {
        try {
            Copy-Item -Path "XY_Toolbox.dll" -Destination "XY_Toolbox_$i.dll" -Force -ErrorAction Stop
            Write-Host "Synced: XY_Toolbox_$i.dll" -ForegroundColor Green
            break
        } catch {
            continue
        }
    }
} else {
    Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
}
