param(
    [Parameter(Mandatory = $true)]
    [string]$VersionJson,

    [Parameter(Mandatory = $true)]
    [string]$DllPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $VersionJson)) {
    throw "Version JSON was not found: $VersionJson"
}

if (-not (Test-Path -LiteralPath $DllPath)) {
    throw "DLL was not found: $DllPath"
}

$hash = (Get-FileHash -LiteralPath $DllPath -Algorithm SHA256).Hash.ToLowerInvariant()
$json = Get-Content -LiteralPath $VersionJson -Raw | ConvertFrom-Json
$json.sha256 = $hash
$json | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $VersionJson -Encoding UTF8
