Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "workers-version-$([guid]::NewGuid().ToString('N'))"
$versionFile = Join-Path $testDirectory 'Directory.Build.props'
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    foreach ($case in @(
        @{ Before = '0.6.0'; Bump = 'patch'; After = '0.6.1' },
        @{ Before = '0.6.9'; Bump = 'patch'; After = '0.6.10' },
        @{ Before = '0.6.1'; Bump = 'minor'; After = '0.7.0' },
        @{ Before = '0.6.1'; Bump = 'major'; After = '1.0.0' },
        @{ Before = '1.9.9'; Bump = 'minor'; After = '1.10.0' },
        @{ Before = '1.9.9'; Bump = 'major'; After = '2.0.0' },
        @{ Before = '0.6.1'; Bump = 'current'; After = '0.6.1' }
    )) {
        $before = "<Project>`n  <PropertyGroup><WorkersVersion>$($case.Before)</WorkersVersion></PropertyGroup>`n</Project>`n"
        [System.IO.File]::WriteAllText($versionFile, $before)
        & (Join-Path $PSScriptRoot 'bump-version.ps1') -Bump $case.Bump -VersionFile $versionFile
        $expected = $before.Replace("<WorkersVersion>$($case.Before)</WorkersVersion>", "<WorkersVersion>$($case.After)</WorkersVersion>")
        if ([System.IO.File]::ReadAllText($versionFile) -cne $expected) { throw "Incorrect $($case.Bump) bump from $($case.Before)" }
    }
    foreach ($invalid in @(
        '<Project />',
        '<WorkersVersion>0.6.1-preview.1</WorkersVersion>',
        '<WorkersVersion>0.6.1</WorkersVersion><WorkersVersion>0.6.2</WorkersVersion>'
    )) {
        [System.IO.File]::WriteAllText($versionFile, $invalid)
        $failed = $false
        try { & (Join-Path $PSScriptRoot 'bump-version.ps1') -Bump patch -VersionFile $versionFile }
        catch { $failed = $true }
        if (-not $failed) { throw 'Invalid version metadata was accepted.' }
        if ([System.IO.File]::ReadAllText($versionFile) -cne $invalid) { throw 'Invalid version metadata was modified.' }
    }
    Write-Host 'All 10 version bump checks passed.'
} finally {
    $resolvedTestDirectory = [System.IO.Path]::GetFullPath($testDirectory)
    if ([System.IO.Path]::GetDirectoryName($resolvedTestDirectory) -ne [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) -or
        [System.IO.Path]::GetFileName($resolvedTestDirectory) -notlike 'workers-version-*') {
        throw 'Unexpected temporary test directory.'
    }
    Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
}
