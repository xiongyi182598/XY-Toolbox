$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$acadDir = 'C:\Program Files\Autodesk\AutoCAD 2024'

Stop-Process -Name acad -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

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

Write-Host "开始执行编译 XY_Toolbox.dll..."
& $csc $argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host ">>> 编译成功！输出文件: XY_Toolbox.dll" -ForegroundColor Green
} else {
    Write-Host ">>> 编译失败，退出码: $LASTEXITCODE" -ForegroundColor Red
}
