param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Win32\Release\RazorEnhanced.exe'),
    [string]$DependencyDirectory = (Join-Path $PSScriptRoot '..\bin\Win32\Release'),
    [string]$OutputDirectory = (Join-Path $env:TEMP "RAStatusPacketUiRegression-$PID")
)
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$probe = Join-Path $OutputDirectory 'StatusPacketUiRegression.exe'
& $compiler /nologo /target:exe /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/out:$probe" (Join-Path $PSScriptRoot 'StatusPacketUiRegression.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>
'@ | Set-Content -LiteralPath "$probe.config" -Encoding UTF8
& $probe $AssemblyPath $DependencyDirectory
exit $LASTEXITCODE
