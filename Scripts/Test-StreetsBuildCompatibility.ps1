param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\BMX Streets',
    [string]$MetadataRoot,
    [string]$BaselineProfile = (Join-Path $PSScriptRoot '../docs/native-hook-profile-26257400.json'),
    [string]$ReportDirectory = (Join-Path $PSScriptRoot ("../outputs/streets-compatibility-" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))),
    [string]$CecilAssemblyPath,
    [switch]$AllowUnknownBuildForReport,
    [string]$GameVersion = '',
    [string]$SteamBuildId = ''
)

# Read-only native metadata audit. It never patches a game, replaces wrappers,
# edits approved profiles or enables an unknown build. Discovery output is only
# a candidate for review with fresh Cpp2IL metadata and native/runtime checks.
$ErrorActionPreference = 'Stop'
function Normalize-Signature([string]$Signature) {
    # Preserve owner, method, return type and every argument. Only the compiler's
    # generated iterator counter may vary; similarly named routines stay distinct.
    [regex]::Replace($Signature, '(<[^<>]+>d__)\d+', '$1*')
}
function Method-Family([string]$Signature) {
    $match = [regex]::Match($Signature, '^.* (?<type>[^ ]+)::(?<method>[^(:]+)\(')
    if (!$match.Success) { throw "Cannot parse method identity: $Signature" }
    Normalize-Signature ($match.Groups['type'].Value + '::' + $match.Groups['method'].Value)
}
function Image-Size([string]$Path) {
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'GameAssembly is not a PE image.' }
        $reader.BaseStream.Position = 0x3C
        $offset = $reader.ReadInt32()
        if ($offset -lt 0x40 -or $offset -gt $reader.BaseStream.Length - 84) { throw 'Invalid PE offset.' }
        $reader.BaseStream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid PE signature.' }
        $reader.BaseStream.Position = $offset + 24
        if ($reader.ReadUInt16() -notin @(0x10B, 0x20B)) { throw 'Unsupported PE optional header.' }
        $reader.BaseStream.Position = $offset + 80
        $reader.ReadUInt32()
    } finally { $reader.Dispose() }
}
function Save-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}

$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (!$CecilAssemblyPath) { $CecilAssemblyPath = Join-Path $GameRoot 'MelonLoader/net35/Mono.Cecil.dll' }
if (!$MetadataRoot) { $MetadataRoot = Join-Path $GameRoot 'MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out' }
$MetadataRoot = (Resolve-Path -LiteralPath $MetadataRoot).Path
$ReportDirectory = [IO.Path]::GetFullPath($ReportDirectory)
foreach ($protected in @($GameRoot, $MetadataRoot)) {
    if ($ReportDirectory.TrimEnd('\', '/') -eq $protected.TrimEnd('\', '/') -or
        $ReportDirectory.StartsWith($protected.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Write the audit outside the game and metadata directories.'
    }
}
if (Test-Path -LiteralPath $ReportDirectory) { throw 'Use a new report directory; existing audits are preserved.' }
$baseline = Get-Content -LiteralPath $BaselineProfile -Raw | ConvertFrom-Json
if (!$baseline.gameAssemblySha256 -or @($baseline.targets).Count -eq 0) { throw 'Invalid baseline profile.' }
$binary = Join-Path $GameRoot 'GameAssembly.dll'
$hash = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash
$imageSize = Image-Size $binary
$sameBuild = $hash -eq $baseline.gameAssemblySha256
$errors = [Collections.Generic.List[string]]::new()
$targets = [Collections.Generic.List[object]]::new()
$signatureMatches = [Collections.Generic.Dictionary[string, Collections.Generic.List[object]]]::new([StringComparer]::Ordinal)
$families = [Collections.Generic.Dictionary[string, Collections.Generic.List[string]]]::new([StringComparer]::Ordinal)
$owners = @{}
foreach ($target in $baseline.targets) {
    $key = Normalize-Signature $target.signature
    if ($signatureMatches.ContainsKey($key)) { throw "Ambiguous baseline identity: $key" }
    $signatureMatches[$key] = [Collections.Generic.List[object]]::new()
    $family = Method-Family $target.signature
    if (!$families.ContainsKey($family)) { $families[$family] = [Collections.Generic.List[string]]::new() }
}
$files = @(Get-ChildItem -LiteralPath $MetadataRoot -Filter '*.dll' -File | Sort-Object Name)
$fingerprints = [Collections.Generic.List[string]]::new()
$assemblyCount = 0
$methodCount = 0
if (!$sameBuild -and !$AllowUnknownBuildForReport) {
    $errors.Add('GameAssembly changed. Regenerate Cpp2IL metadata, then use -AllowUnknownBuildForReport for a review-only candidate. Runtime support is not enabled by this tool.')
} else {
    if ($files.Count -eq 0) { $errors.Add('No Cpp2IL metadata assemblies found.') }
    Add-Type -Path $CecilAssemblyPath
    foreach ($file in $files) {
        $before = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($file.FullName)
        try {
            $assemblyCount++
            foreach ($type in $assembly.MainModule.GetTypes()) {
                foreach ($method in $type.Methods) {
                    $methodCount++
                    $signature = [string]$method.FullName
                    $rva = $null
                    foreach ($attribute in $method.CustomAttributes) {
                        if ($attribute.AttributeType.FullName -ne 'Cpp2ILInjected.AddressAttribute') { continue }
                        foreach ($field in $attribute.Fields) {
                            if ($field.Name -eq 'RVA') {
                                $value = [string]$field.Argument.Value
                                if ($value -notmatch '^0x[0-9a-fA-F]+$') { throw "Invalid address metadata on $signature" }
                                $rva = '0x{0:X}' -f [Convert]::ToUInt64($value.Substring(2), 16)
                            }
                        }
                    }
                    if ($null -ne $rva -and $rva -ne '0x0') {
                        if (!$owners.ContainsKey($rva)) { $owners[$rva] = [Collections.Generic.List[string]]::new() }
                        $owners[$rva].Add($signature)
                    }
                    $key = Normalize-Signature $signature
                    if ($signatureMatches.ContainsKey($key)) {
                        $signatureMatches[$key].Add([pscustomobject]@{signature=$signature; rva=$rva; invalidShape=($method.IsStatic -or $method.HasGenericParameters -or $type.HasGenericParameters)})
                    }
                    $family = Method-Family $signature
                    if ($families.ContainsKey($family)) { $families[$family].Add($signature) }
                }
            }
        } finally { $assembly.Dispose() }
        if ($before -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { $errors.Add("Metadata changed during audit: $($file.Name)") }
        $fingerprints.Add($file.Name + '|' + $before)
    }
    foreach ($expected in $baseline.targets) {
        $found = $signatureMatches[(Normalize-Signature $expected.signature)]
        if ($found.Count -ne 1) {
            $alternatives = $families[(Method-Family $expected.signature)] -join '; '
            $errors.Add("Expected one exact or counter-renamed target for $($expected.signature); found $($found.Count). Current overloads: $alternatives")
            continue
        }
        $method = $found[0]
        $validAddress = $method.rva -and $method.rva -ne '0x0' -and [Convert]::ToUInt64($method.rva.Substring(2), 16) -lt $imageSize
        if ($method.invalidShape -or !$validAddress) { $errors.Add("Unsupported instance signature or address: $($method.signature)"); continue }
        $addressOwners = $owners[$method.rva]
        if ($addressOwners.Count -ne 1) {
            $errors.Add("Shared native address $($method.rva) has $($addressOwners.Count) owners; refusing $($method.signature).")
            continue
        }
        if ($sameBuild -and ($method.signature -cne $expected.signature -or $method.rva -ine $expected.updatedRva)) {
            $errors.Add("Metadata differs from the approved profile for this game hash: $($method.signature). Regenerate metadata or investigate the profile.")
            continue
        }
        $targets.Add([ordered]@{signature=$method.signature; originalRva=$expected.originalRva; updatedRva=$method.rva; previousRva=$expected.updatedRva; generatedCounterChanged=($method.signature -cne $expected.signature)})
    }
    if ($sameBuild -and $baseline.audit -and
        ($assemblyCount -ne $baseline.audit.assemblies -or $methodCount -ne $baseline.audit.methods)) {
        $errors.Add('Metadata inventory differs from the approved build. Missing assemblies could hide shared-address aliases; regenerate the complete metadata set.')
    }
    $afterNames = @(Get-ChildItem -LiteralPath $MetadataRoot -Filter '*.dll' -File | Sort-Object Name | ForEach-Object Name)
    if (@(Compare-Object @($files.Name) $afterNames).Count -gt 0) { $errors.Add('Metadata file inventory changed during audit.') }
}
if ((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -ne $hash) { $errors.Add('GameAssembly changed during audit; discard this report.') }
$metadataHash = $null
if ($fingerprints.Count -gt 0) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $metadataHash = [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($fingerprints -join "`n"))) } finally { $sha.Dispose() }
}
New-Item -ItemType Directory -Path $ReportDirectory | Out-Null
$report = [ordered]@{
    status = if ($errors.Count) { 'blocked' } elseif ($sameBuild) { 'approved-profile-metadata-matches' } else { 'candidate-requires-review' }
    gameAssemblySha256=$hash; baselineGameAssemblySha256=$baseline.gameAssemblySha256
    metadataAssemblies=$assemblyCount; metadataMethods=$methodCount; metadataSetSha256=$metadataHash
    metadataFiles=$fingerprints.ToArray(); targets=$targets.ToArray(); errors=$errors.ToArray()
    changedAddresses=@($targets | Where-Object { $_.updatedRva -ine $_.previousRva }).Count
    renamedCoroutines=@($targets | Where-Object generatedCounterChanged).Count
    runtimeProfileModified=$false; runtimeValidationRequired=$true
    metadataFreshness='Fingerprint records the supplied metadata; regenerate it from this game binary before reviewing a new build. A matching signature alone is not native safety proof.'
}
Save-Json (Join-Path $ReportDirectory 'compatibility-report.json') $report
if ($errors.Count -eq 0) {
    Save-Json (Join-Path $ReportDirectory 'candidate-native-profile.json') ([ordered]@{
        gameAssemblySha256=$hash
        gameVersion=if ($GameVersion) { $GameVersion } elseif ($sameBuild) { $baseline.gameVersion } else { '' }
        steamBuildId=if ($SteamBuildId) { $SteamBuildId } elseif ($sameBuild) { $baseline.steamBuildId } else { '' }
        targets=@($targets | ForEach-Object { [ordered]@{signature=$_.signature; originalRva=$_.originalRva; updatedRva=$_.updatedRva} })
        audit=[ordered]@{assemblies=$assemblyCount; methods=$methodCount; uniqueTargets=$targets.Count; metadataSetSha256=$metadataHash; candidateOnly=$true; requiresNativeRuntimeValidation=$true}
    })
}
Write-Output "$($report.status): $($targets.Count)/$(@($baseline.targets).Count) unique targets; $methodCount methods in $assemblyCount assemblies. Report: $ReportDirectory"
if ($errors.Count -gt 0) { throw ($errors -join "`n") }
