$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$candidateDirs = @(
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
Write-Host "Using AutoCAD directory: $acadDir"

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
    "HailongService.cs"
)

$argsList = @("/target:library", "/platform:x64", "/out:XY_Toolbox.dll") + $refs + $sources

Write-Host "Compiling XY_Toolbox.dll..."
& $csc $argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build success! Output: XY_Toolbox.dll" -ForegroundColor Green
    Copy-Item -Path "XY_Toolbox.dll" -Destination "XY_Toolbox_108.dll" -Force -ErrorAction SilentlyContinue
    Write-Host "Synced: XY_Toolbox_108.dll" -ForegroundColor Green
} else {
    Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
}
