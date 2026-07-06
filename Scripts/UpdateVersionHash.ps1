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

$repoRoot = Split-Path -Parent $resolvedManifestPath
$patchNotes = $manifest.patchNotes
$patchNotesSource = "manifest"

try {
    $gitStatus = (& git -C $repoRoot status --porcelain 2>$null) -join "`n"
    if ([string]::IsNullOrWhiteSpace($gitStatus)) {
        $gitNotes = (& git -C $repoRoot log -1 --pretty=%B 2>$null) -join "`n"
        $gitNotes = $gitNotes.Trim()
        if (-not [string]::IsNullOrWhiteSpace($gitNotes)) {
            $patchNotes = $gitNotes
            $patchNotesSource = "git-log"
        }
    } else {
        $patchNotesSource = "manifest-dirty-worktree"
    }
} catch {
    Write-Warning "Could not read latest git commit message for patchNotes: $($_.Exception.Message)"
}

if ($null -eq $patchNotes) {
    $patchNotes = ""
}

$orderedManifest = [ordered]@{
    version = $manifest.version
    downloadUrl = $manifest.downloadUrl
    sha256 = $hash
    notesUrl = $manifest.notesUrl
    patchNotes = $patchNotes
    requiredGameVersion = $manifest.requiredGameVersion
}

$json = $orderedManifest | ConvertTo-Json -Depth 8

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($resolvedManifestPath, $json, $utf8NoBom)

Write-Host "Updated version.json sha256=$hash patchNotesSource=$patchNotesSource"
