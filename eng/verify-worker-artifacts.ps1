[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxRawBytes = 65536,

    [ValidateRange(1, [int]::MaxValue)]
    [int] $MaxGzipBytes = 20480,

    [switch] $SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$examplesRoot = Join-Path $repositoryRoot "examples"
$fixturesRoot = Join-Path $repositoryRoot "tests\runtime\fixtures"
$projectRoots = @($examplesRoot, $fixturesRoot)
$projects = @($projectRoots | ForEach-Object {
        Get-ChildItem -LiteralPath $_ -Directory | ForEach-Object {
            Get-ChildItem -LiteralPath $_.FullName -Filter "*.csproj" -File
        }
    } |
    Sort-Object FullName)

if ($projects.Count -eq 0) {
    throw "No Worker projects were found under the configured artifact roots."
}

$forbiddenMarkers = @(
    "_framework",
    "createDotnetRuntime",
    "dotnet.native",
    "getAssemblyExports",
    "mono_wasm",
    "RuntimeAdapter"
)

function Get-GzipLength([string] $path) {
    $inputBytes = [System.IO.File]::ReadAllBytes($path)
    $compressed = [System.IO.MemoryStream]::new()
    try {
        $gzip = [System.IO.Compression.GZipStream]::new(
            $compressed,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $true)
        try {
            $gzip.Write($inputBytes, 0, $inputBytes.Length)
        }
        finally {
            $gzip.Dispose()
        }

        return $compressed.Length
    }
    finally {
        $compressed.Dispose()
    }
}

$failures = [System.Collections.Generic.List[string]]::new()
$results = [System.Collections.Generic.List[object]]::new()

foreach ($project in $projects) {
    $exampleName = $project.Directory.Name
    $distDirectory = Join-Path $project.Directory.FullName "dist"

    if (-not $SkipPublish) {
        Write-Host "Publishing $exampleName..."
        & dotnet publish $project.FullName -c $Configuration --nologo -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing '$($project.FullName)' failed with exit code $LASTEXITCODE."
        }
    }

    if (-not (Test-Path -LiteralPath $distDirectory -PathType Container)) {
        $failures.Add("${exampleName}: dist directory was not generated.")
        continue
    }

    $files = @(Get-ChildItem -LiteralPath $distDirectory -Recurse -File)
    $relativeFiles = @($files | ForEach-Object {
        $_.FullName.Substring($distDirectory.Length).TrimStart('\', '/').Replace('\', '/')
    })

    if ($relativeFiles.Count -ne 1 -or $relativeFiles[0] -cne "worker.js") {
        $actual = if ($relativeFiles.Count -eq 0) { "<empty>" } else { $relativeFiles -join ", " }
        $failures.Add("${exampleName}: dist must contain only worker.js; found $actual.")
        continue
    }

    $workerPath = Join-Path $distDirectory "worker.js"
    $worker = Get-Item -LiteralPath $workerPath
    $source = [System.IO.File]::ReadAllText($workerPath)
    $gzipBytes = Get-GzipLength $workerPath

    foreach ($marker in $forbiddenMarkers) {
        if ($source.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $failures.Add("${exampleName}: worker.js contains forbidden legacy runtime marker '$marker'.")
        }
    }

    if ($exampleName -ceq "RuntimeIntrinsics") {
        if ($source.IndexOf('Helpers$ReachableMessage$', [System.StringComparison]::Ordinal) -lt 0) {
            $failures.Add("${exampleName}: reachable cross-file helper was not emitted.")
        }
        if ($source.IndexOf("UNUSED_HELPER_SENTINEL", [System.StringComparison]::Ordinal) -ge 0) {
            $failures.Add("${exampleName}: unreachable helper leaked into worker.js.")
        }
    }

    if ($worker.Length -gt $MaxRawBytes) {
        $failures.Add("${exampleName}: worker.js is $($worker.Length) raw bytes (limit: $MaxRawBytes).")
    }

    if ($gzipBytes -gt $MaxGzipBytes) {
        $failures.Add("${exampleName}: worker.js is $gzipBytes gzip bytes (limit: $MaxGzipBytes).")
    }

    $results.Add([pscustomobject]@{
        Example = $exampleName
        RawBytes = $worker.Length
        GzipBytes = $gzipBytes
    })
}

Write-Host ""
$results | Sort-Object Example | Format-Table -AutoSize

if ($failures.Count -gt 0) {
    Write-Error ("Worker artifact verification failed:`n- " + ($failures -join "`n- "))
    exit 1
}

Write-Host "Verified $($results.Count) minimal Worker artifacts (raw <= $MaxRawBytes bytes; gzip <= $MaxGzipBytes bytes)."
