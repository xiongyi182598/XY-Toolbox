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
    "LayerMappingWorkbench\Commands\ExtensionCommands.cs",
    "TextFilterWorkbench\Models\TextFilterCriteria.cs",
    "TextFilterWorkbench\Services\TextFilterService.cs",
    "TextFilterWorkbench\Views\TextFilterDialog.cs",
    "TextFilterWorkbench\Commands\TextFilterCommands.cs",
    "AttributeWorkbench\Services\AttributeModifyService.cs",
    "AttributeWorkbench\Views\AttributeModifyDialog.cs",
    "FontWorkbench\Models\FontIssueItem.cs",
    "FontWorkbench\Services\FontDiagnosisService.cs",
    "FontWorkbench\ViewModels\FontFixViewModel.cs",
    "FontWorkbench\Views\FontFixDialog.cs"
)

$existing = Get-ChildItem -Path "$PSScriptDir\XY_Toolbox_*.dll" -ErrorAction SilentlyContinue | Where-Object { $_.BaseName -match '^XY_Toolbox_(\d+)$' } | ForEach-Object { [int]$Matches[1] }
$nextVer = 120
if ($existing) {
    $nextVer = ($existing | Measure-Object -Maximum).Maximum + 1
}

$csFile = "$PSScriptDir\XY_Toolbox.cs"
if (Test-Path $csFile) {
    $csContent = [System.IO.File]::ReadAllText($csFile, [System.Text.Encoding]::UTF8)
    $csContent = [System.Text.RegularExpressions.Regex]::Replace($csContent, 'CurrentBuildVersion\s*=\s*\d+;', "CurrentBuildVersion = $nextVer;")
    [System.IO.File]::WriteAllText($csFile, $csContent, [System.Text.Encoding]::UTF8)
}

$argsList = @("/target:library", "/platform:x64", "/codepage:65001", "/out:XY_Toolbox.dll") + $refs + $sources

Write-Host "Compiling XY_Toolbox.dll (Build Version: $nextVer)..."
& $csc $argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build success! Output: XY_Toolbox.dll" -ForegroundColor Green
    try {
        Copy-Item -Path "XY_Toolbox.dll" -Destination "XY_Toolbox_$nextVer.dll" -Force -ErrorAction Stop
        Write-Host "Synced: XY_Toolbox_$nextVer.dll" -ForegroundColor Green
    } catch {
        Write-Host "Warning: Could not copy to XY_Toolbox_$nextVer.dll: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
}
