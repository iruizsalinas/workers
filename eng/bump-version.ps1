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
    'current' { return }
}
$version = "$major.$minor.$patch"
$text = $text.Remove($versionMatch.Index, $versionMatch.Length).Insert($versionMatch.Index, "<WorkersVersion>$version</WorkersVersion>")
[System.IO.File]::WriteAllText($VersionFile, $text, [System.Text.UTF8Encoding]::new($false))
Write-Host "Updated WorkersVersion to $version"
