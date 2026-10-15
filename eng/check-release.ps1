param(
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$RefType = $env:GITHUB_REF_TYPE,
    [string]$RefName = $env:GITHUB_REF_NAME
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RefType -ne 'branch' -or $RefName -ne 'main') {
    throw 'Releases must run from the main branch.'
}
$root = Split-Path $PSScriptRoot -Parent
$propertiesJson = & dotnet msbuild (Join-Path $root 'src/Workers.csproj') -nologo '-getProperty:PackageId,PackageVersion,RepositoryUrl'
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate package metadata.' }
$metadata = ($propertiesJson | Out-String | ConvertFrom-Json).Properties
if ($metadata.PackageVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Only stable package versions can be released.' }
if ($metadata.RepositoryUrl.TrimEnd('/') -ine "https://github.com/$Repository") {
    throw 'Package repository must match the publishing repository.'
}
$tag = "v$($metadata.PackageVersion)"
$existingTag = & git -C $root tag --list $tag
if ($LASTEXITCODE -ne 0) { throw 'Could not check existing tags.' }
$shouldRelease = -not [bool]$existingTag
if ($shouldRelease) {
    $id = $metadata.PackageId.ToLowerInvariant()
    $uri = "https://api.nuget.org/v3-flatcontainer/$id/index.json"
    $response = Invoke-WebRequest -Uri $uri -SkipHttpErrorCheck -TimeoutSec 30
    if ([int]$response.StatusCode -eq 200) {
        $versions = ($response.Content | ConvertFrom-Json).versions
        $shouldRelease = $metadata.PackageVersion -notin $versions
    } elseif ([int]$response.StatusCode -ne 404) {
        throw "NuGet version check failed: HTTP $($response.StatusCode)"
    }
}
if ($env:GITHUB_OUTPUT) {
    @(
        "tag=$tag"
        "package=$($metadata.PackageId).$($metadata.PackageVersion).nupkg"
        "should_release=$($shouldRelease.ToString().ToLowerInvariant())"
    ) | Add-Content -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8
}
if ($shouldRelease) { Write-Host "Ready to release $tag" }
else { Write-Host "Tag or NuGet version $tag already exists; skipping." }
