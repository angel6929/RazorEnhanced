# Apply translation dictionaries to a target C# file (or multiple files).
# Saves output as UTF-8 with BOM (required for C# compiler to correctly read Chinese).
param(
    [Parameter(Mandatory=$true)][string[]]$Target,
    [string[]]$Dict = @(
        (Join-Path $PSScriptRoot "translation.json"),
        (Join-Path $PSScriptRoot "translation_extra.json")
    )
)

# Merge all dicts into one hashtable
$translations = @{}
foreach ($d in $Dict) {
    if (-not (Test-Path $d)) { Write-Warning "Dict not found, skipping: $d"; continue }
    $raw = Get-Content $d -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($prop in $raw.PSObject.Properties) {
        if ($prop.Name.StartsWith('_')) { continue }
        $translations[$prop.Name] = $prop.Value
    }
}
Write-Output "Loaded $($translations.Count) translation entries from $($Dict.Count) dict file(s)"

$pattern = '(\.(?:Text|HeaderText|ToolTipText|Title))(\s*=\s*)"([^"\\]*(?:\\.[^"\\]*)*)"'
$utf8WithBom = New-Object System.Text.UTF8Encoding $true

# Expand wildcards
$expanded = @()
foreach ($t in $Target) {
    if ($t -match '\*') {
        $expanded += Get-ChildItem -Path $t -Recurse | ForEach-Object { $_.FullName }
    } else {
        $expanded += $t
    }
}

$totalChanged = 0
$totalMiss = 0
$allMissed = @{}

foreach ($file in $expanded) {
    if (-not (Test-Path $file)) { Write-Warning "Skip (not found): $file"; continue }
    $content = [System.IO.File]::ReadAllText($file)
    if (-not $content) { continue }

    $script:fileChanged = 0
    $evaluator = {
        param($m)
        $prop = $m.Groups[1].Value
        $eq   = $m.Groups[2].Value
        $en   = $m.Groups[3].Value
        if ($translations.ContainsKey($en)) {
            $zh = $translations[$en]
            if ($zh -ne $en) {
                $script:fileChanged++
                return "$prop$eq`"$zh`""
            }
        } else {
            if ($en.Length -gt 0 -and $en -notmatch '^[\d\s\.\-+%X#:,/]+$') {
                if (-not $allMissed.ContainsKey($en)) { $allMissed[$en] = 0 }
                $allMissed[$en]++
                $script:totalMiss++
            }
        }
        return $m.Value
    }

    $newContent = [regex]::Replace($content, $pattern, $evaluator)
    if ($script:fileChanged -gt 0) {
        [System.IO.File]::WriteAllText($file, $newContent, $utf8WithBom)
        Write-Output "  [$($script:fileChanged) changes] $file"
        $totalChanged += $script:fileChanged
    }
}

Write-Output "=========================================="
Write-Output "Total replacements: $totalChanged"
Write-Output "Total missed occurrences (still English): $totalMiss across $($allMissed.Count) unique strings"
if ($allMissed.Count -gt 0 -and $allMissed.Count -le 50) {
    Write-Output "--- Missed unique strings ---"
    $allMissed.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
        Write-Output "  [$($_.Value)x] $($_.Key)"
    }
}
