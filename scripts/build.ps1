[CmdletBinding()]
param([switch]$Test, [switch]$Package, [switch]$SkipOcrTests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repo 'src'
$dist = Join-Path $repo 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path "$fx\csc.exe")) { $fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$md = Join-Path $env:WINDIR 'System32\WinMetadata'
$exe = Join-Path $dist 'MaskMitra.exe'
$options = @('/nologo','/target:winexe','/optimize+',"/out:$exe","/win32manifest:$src\app.manifest","/win32icon:$src\MaskMitra.ico",'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
foreach ($name in @('System.Runtime','System.Threading.Tasks','System.Runtime.WindowsRuntime','System.ObjectModel','System.Runtime.InteropServices.WindowsRuntime')) { $options += "/reference:$fx\$name.dll" }
foreach ($name in @('Windows.Foundation','Windows.Globalization','Windows.Media','Windows.Graphics','Windows.Storage')) { $options += "/reference:$md\$name.winmd" }
& "$fx\csc.exe" @options "$src\Core.cs" "$src\Ocr.cs" "$src\App.cs" "$src\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $exe"
$qa = $null
if ($Test) {
    $qa = Join-Path $repo ('artifacts\qa-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $qa | Out-Null
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $exe
    $info.Arguments = '--self-test "' + $qa + '"'
    if ($SkipOcrTests) { $info.Arguments += ' --skip-ocr' }
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $p = [Diagnostics.Process]::Start($info)
    try {
        if (-not $p.WaitForExit(60000)) { $p.Kill(); throw 'Tests timed out.' }
        if ($p.ExitCode -ne 0 -or -not (Test-Path "$qa\PASSED.txt")) {
            if (Test-Path "$qa\FAILED.txt") { Get-Content "$qa\FAILED.txt" }
            throw 'Tests failed.'
        }
        Get-Content "$qa\PASSED.txt"
    } finally { $p.Dispose() }
}
if ($Package) {
    $stage = Join-Path $repo ('artifacts\package-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item -LiteralPath $exe -Destination $stage
    Copy-Item -LiteralPath "$repo\docs\START-HERE.txt" -Destination $stage
    Copy-Item -LiteralPath "$repo\LICENSE" -Destination $stage
    if ($qa) { Copy-Item -LiteralPath "$qa\PASSED.txt" -Destination "$stage\Validation.txt" }
    $zip = Join-Path $dist 'MaskMitra-Windows.zip'
    Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
    $hashes = foreach ($asset in @($exe,$zip)) { '{0}  {1}' -f (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $asset) }
    $hashes | Set-Content -LiteralPath "$dist\SHA256SUMS.txt" -Encoding ascii
    Write-Output "Packaged $zip"
}
