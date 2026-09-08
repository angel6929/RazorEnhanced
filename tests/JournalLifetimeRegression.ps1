param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Win32\Release\RazorEnhanced.exe'),
    [switch]$IncludePressure
)

$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4) { throw 'Run with x86 Windows PowerShell.' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
Add-Type -Path (Join-Path $PSScriptRoot 'JournalLifetimeRegression.cs')
exit [JournalLifetimeRegression]::Run($AssemblyPath, $IncludePressure.IsPresent)
