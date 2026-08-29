param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\BMX Streets',
    [Parameter(Mandatory=$true)][string]$OutputPath
)
# Read-only metadata snapshot; never loads game code or installs a hook.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameRoot 'MelonLoader\net35\Mono.Cecil.dll')
$root = Join-Path $GameRoot 'MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out'
$methods = @{}
$types = @()
foreach ($file in Get-ChildItem -LiteralPath $root -Filter '*.dll') {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($file.FullName)
    try {
        foreach ($type in $assembly.MainModule.GetTypes()) {
            $selected = $type.Name -in @('TrickControllerV2','TrickAnimator','SyncTrickAnimationData','TrickAnimationData')
            if ($selected) {
                $fields = @($type.Fields | ForEach-Object {
                    $offset = (($_.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'FieldOffsetAttribute'}).Fields | Where-Object Name -eq Offset).Argument.Value
                    @{name=$_.Name; type=$_.FieldType.FullName; offset=$offset}
                })
                $types += @{name=$type.FullName; fields=$fields}
            }
            foreach ($method in $type.Methods) {
                $rva = (($method.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AddressAttribute'}).Fields | Where-Object Name -eq RVA).Argument.Value
                if ($rva) { if (!$methods.ContainsKey($rva)) {$methods[$rva]=@()}; $methods[$rva] += $method.FullName }
            }
        }
    } finally { $assembly.Dispose() }
}
@{hash=(Get-FileHash -LiteralPath (Join-Path $GameRoot 'GameAssembly.dll')).Hash; types=$types; methods=$methods} |
    ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output "Saved animation audit metadata to $OutputPath"
