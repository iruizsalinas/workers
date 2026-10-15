param(
    [Parameter(Mandatory)]
    [ValidateSet('patch', 'minor', 'major', 'current')]
    [string]$Bump,
    [string]$VersionFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Directory.Build.props')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$text = [System.IO.File]::ReadAllText($VersionFile)
$matches = [regex]::Matches($text, '<WorkersVersion>(\d+)\.(\d+)\.(\d+)</WorkersVersion>')
if ($matches.Count -ne 1) { throw 'Expected exactly one stable WorkersVersion.' }
$versionMatch = $matches[0]
[long]$major = $versionMatch.Groups[1].Value
[long]$minor = $versionMatch.Groups[2].Value
[long]$patch = $versionMatch.Groups[3].Value
switch ($Bump) {
    'major' { $major++; $minor = 0; $patch = 0 }
    'minor' { $minor++; $patch = 0 }
    'patch' { $patch++ }
}
$version = "$major.$minor.$patch"
$changes = [ordered]@{}
if ($Bump -ne 'current') {
    $changes[[System.IO.Path]::GetFullPath($VersionFile)] = $text.Remove($versionMatch.Index, $versionMatch.Length).Insert($versionMatch.Index, "<WorkersVersion>$version</WorkersVersion>")
}
$root = Split-Path ([System.IO.Path]::GetFullPath($VersionFile)) -Parent
$directories = [System.Collections.Generic.Stack[string]]::new()
$directories.Push($root)
$referencePattern = '(?s)<PackageReference\b(?=[^>]*\b(?:Include|Update)\s*=\s*["'']Workers["''])[^>]*>'
$versionPattern = '(\bVersion\s*=\s*["''])\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(["''])'
while ($directories.Count -gt 0) {
    $directory = $directories.Pop()
    foreach ($item in Get-ChildItem -LiteralPath $directory) {
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { continue }
        if ($item.PSIsContainer) {
            if ($item.Name -notin @('.git', 'node_modules', 'bin', 'obj', '.artifacts', 'dist', '.wrangler')) {
                $directories.Push($item.FullName)
            }
        } elseif ($item.Extension -in @('.csproj', '.props', '.targets')) {
            $original = [System.IO.File]::ReadAllText($item.FullName)
            $referenceSource = if ($changes.Contains($item.FullName)) { $changes[$item.FullName] } else { $original }
            $updated = [regex]::Replace($referenceSource, $referencePattern, [System.Text.RegularExpressions.MatchEvaluator]{
                param($reference)
                [regex]::Replace($reference.Value, $versionPattern, [System.Text.RegularExpressions.MatchEvaluator]{
                    param($pin)
                    $pin.Groups[1].Value + $version + $pin.Groups[2].Value
                })
            })
            if ($updated -cne $original) { $changes[$item.FullName] = $updated }
        }
    }
}
foreach ($file in $changes.Keys) {
    [System.IO.File]::WriteAllText($file, $changes[$file], [System.Text.UTF8Encoding]::new($false))
}
if ($env:GITHUB_OUTPUT) {
    $relativePaths = @($changes.Keys | ForEach-Object { [System.IO.Path]::GetRelativePath($root, $_).Replace('\', '/') })
    @('files<<WORKERS_VERSION_FILES'; $relativePaths; 'WORKERS_VERSION_FILES') |
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8
}
Write-Host "WorkersVersion $version; updated $($changes.Count) files."
