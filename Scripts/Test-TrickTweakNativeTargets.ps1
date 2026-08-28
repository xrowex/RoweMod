param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\BMX Streets'
)

# Read-only audit of Cpp2IL's native-address metadata, including nested/generic types.
# These are original metadata assemblies, NOT MelonLoader's managed wrappers.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameRoot 'MelonLoader\net35\Mono.Cecil.dll')
$metadataRoot = Join-Path $GameRoot 'MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out'
$expectedHash = '3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1'
$gameHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $GameRoot 'GameAssembly.dll')).Hash
if ($gameHash -ne $expectedHash) { throw 'GameAssembly changed. Regenerate Cpp2IL metadata and re-audit targets before enabling native hooks.' }

$targets = @{
    '0x8B0A50' = 'System.Void MashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickControllerV2::RequestTweak()'
    '0x8B0E30' = 'System.Void MashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2.TrickControllerV2::Tweak()'
    '0x85BE70' = 'System.Void MashBox.BMX_Physics_Development.VehicleAnimationInputHandler::SetInteger(System.String,System.Int32)'
    '0xA61F70' = 'System.Void MashBox.Netorking.FusionBootstrap::DrawServerBrowserHostMapControls(System.Single)'
    '0xD53AA0' = 'System.Threading.Tasks.Task`1<Fusion.StartGameResult> Fusion.NetworkRunner::StartGame(Fusion.StartGameArgs)'
    '0x9F47C0' = 'System.Void MashBox.Addons.NetworkingFusion.NetworkPlayer::Spawned()'
    '0x2983750' = 'System.Void MashBox.Core.Runtime.InverseKinematics.HumanIK::OnAnimatorIK(System.Int32)'
    '0x298C9E0' = 'System.Void MashBox.Core.Runtime.InverseKinematics.UnityIKLimb::UpdateIK(System.Boolean)'
    '0x89A960' = 'System.Void MashBox.BMX_Physics_Development.VehicleFootPedalAnimationRig::LateUpdate()'
    '0x8077B0' = 'System.Void MashBox.BMX_Physics_Development.BikeGrindPoser::SetInputData(System.Int32,MashBoxBridge.Common.Interfaces.HookGrind)'
    '0x837760' = 'System.Void MashBox.BMX_Physics_Development.QuaternionPDDrive::Tick(UnityEngine.Rigidbody,UnityEngine.Quaternion)'
    '0x90DA80' = 'System.Void MashBox.Addons.ProtoDrone.DroneController::LocalSpawnBullet(UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)'
    '0x90E100' = 'System.Void MashBox.Addons.ProtoDrone.DroneController::RPC_FireBullet(UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)'
    '0x8544D0' = 'System.Void MashBox.BMX_Physics_Development.TrickDetection::RecordTrickToHistory(System.String)'
    '0x9F3220' = 'System.Void MashBox.Addons.NetworkingFusion.NetworkPlayer::Despawned(Fusion.NetworkRunner,System.Boolean)'
}
$matches = @{}
foreach ($rva in $targets.Keys) { $matches[$rva] = [System.Collections.Generic.List[string]]::new() }
$unsafeAliases = [System.Collections.Generic.List[string]]::new()
$assemblyCount = 0
$methodCount = 0
foreach ($file in Get-ChildItem -LiteralPath $metadataRoot -Filter '*.dll') {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($file.FullName)
    try {
        $assemblyCount++
        foreach ($type in $assembly.MainModule.GetTypes()) {
            foreach ($method in $type.Methods) {
                $methodCount++
                foreach ($attribute in $method.CustomAttributes) {
                    if ($attribute.AttributeType.FullName -ne 'Cpp2ILInjected.AddressAttribute') { continue }
                    foreach ($field in $attribute.Fields) {
                        if ($field.Name -ne 'RVA') { continue }
                        $rva = [string]$field.Argument.Value
                        if ($matches.ContainsKey($rva)) { $matches[$rva].Add($method.FullName) }
                        if ($rva -eq '0x66E640') { $unsafeAliases.Add($method.FullName) }
                    }
                }
            }
        }
    }
    finally { $assembly.Dispose() }
}

Write-Output "Audited $methodCount methods in $assemblyCount assemblies. GameAssembly SHA256=$gameHash"
foreach ($rva in $targets.Keys | Sort-Object) {
    $owners = $matches[$rva]
    if ($owners.Count -ne 1 -or $owners[0] -cne $targets[$rva]) {
        throw "Unsafe or missing target at ${rva}: $($owners -join '; ')"
    }
    Write-Output "PASS unique native target ${rva}: $($owners[0])"
}
if (-not ($unsafeAliases | Where-Object { $_ -like '*TweakState::get_CanEnterState()*' }) -or
    -not ($unsafeAliases | Where-Object { $_ -like '*NetworkAssetSourceStatic*::get_IsCompleted()*' })) {
    throw 'Expected shared-function regression evidence missing; confirm metadata is current.'
}
Write-Output "Confirmed rejected RVA 0x66E640 has $($unsafeAliases.Count) aliases, including the tweak-state and prefab-completion checks."
