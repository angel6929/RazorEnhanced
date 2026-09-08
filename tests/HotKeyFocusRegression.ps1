param(
    [string]$HotKeySourcePath = (Join-Path $PSScriptRoot '..\Razor\RazorEnhanced\HotKey.cs'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\bin\audit\hotkey-focus')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$hotKey = [IO.File]::ReadAllText($HotKeySourcePath)
$focus = [IO.File]::ReadAllText((Join-Path $root 'Razor\UI\Other\Hotkey.cs'))
$client = [IO.File]::ReadAllText((Join-Path $root 'Razor\Client\ClassicUO.cs'))
$controls = [IO.File]::ReadAllText((Join-Path $root 'Razor\RazorEnhanced\UI\ScriptListView.cs'))
function Slice-Source([string]$source, [string]$start, [string]$end) {
    $first = $source.IndexOf($start)
    $last = $source.IndexOf($end, $first)
    if ($first -lt 0 -or $last -lt 0) { throw "Production method boundary missing: $start" }
    return $source.Substring($first, $last - $first)
}
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'HotKeyFocusRegression.cs'))
$harness = $harness.Replace('__HOTKEY_TYPES__', (Slice-Source $hotKey '    public enum ModKeys' '    internal class HotKey'))
$harness = $harness.Replace('__HOTKEY_METHODS__', (Slice-Source $hotKey '        internal static Keys NormalKey' '        private static void ProcessGroup'))
$harness = $harness.Replace('__FOCUS_ENUM__', (Slice-Source $focus '    internal enum HotKeyFocusTarget' '    public partial class MainForm'))
$harness = $harness.Replace('__FOCUS_METHODS__', (Slice-Source $focus '        private volatile HotKeyFocusTarget m_hotKeyFocus;' '        private void hotkeySetButton_Click'))
$harness = $harness.Replace('__CLIENT_FOCUS_GAINED__', (Slice-Source $client '        public void OnFocusGained()' '        public void OnFocusLost()'))
$harness = $harness.Replace('__HOTKEY_TEXTBOX__', (Slice-Source $controls '    public class RazorHotKeyTextBox' '    public class RazorAgentNumOnlyTextBox'))
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$generated = Join-Path $OutputDirectory 'HotKeyFocusRegression.generated.cs'
$executable = Join-Path $OutputDirectory 'HotKeyFocusRegression.exe'
[IO.File]::WriteAllText($generated, $harness, (New-Object Text.UTF8Encoding($true)))
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$compiler = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
if (!$compiler) { throw 'The Visual Studio C# compiler was not found.' }
& $compiler /nologo /target:exe /platform:x86 /langversion:9.0 "/out:$executable" /r:System.Windows.Forms.dll /r:System.Drawing.dll $generated (Join-Path $root 'Razor\UI\Ext.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $executable
exit $LASTEXITCODE
