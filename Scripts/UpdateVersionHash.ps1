param(
    [Parameter(Mandatory = $true)]
    [string]$VersionJson,

    [Parameter(Mandatory = $true)]
    [string]$DllPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $VersionJson)) {
    throw "Version manifest not found: $VersionJson"
}

if (-not (Test-Path -LiteralPath $DllPath)) {
    throw "DLL not found: $DllPath"
}

$hash = (Get-FileHash -LiteralPath $DllPath -Algorithm SHA256).Hash.ToLowerInvariant()
$resolvedManifestPath = (Resolve-Path -LiteralPath $VersionJson).Path
$jsonText = [System.IO.File]::ReadAllText($resolvedManifestPath)
$manifest = $jsonText | ConvertFrom-Json

if ($null -eq $manifest.PSObject.Properties['sha256']) {
    throw "Version manifest is missing the sha256 property: $VersionJson"
}

$shaPattern = '("sha256"\s*:\s*")[^"]*(")'
$match = [regex]::Match($jsonText, $shaPattern)

if (-not $match.Success) {
    throw "Unable to locate sha256 value in version manifest: $VersionJson"
}

$json = $jsonText.Substring(0, $match.Index) +
    $match.Groups[1].Value +
    $hash +
    $match.Groups[2].Value +
    $jsonText.Substring($match.Index + $match.Length)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($resolvedManifestPath, $json, $utf8NoBom)

Write-Host "Updated version.json sha256=$hash"
