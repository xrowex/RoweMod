param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\BMX Streets',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\bin\Release\net6.0\rowemod.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameRoot 'MelonLoader\net35\Mono.Cecil.dll')
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $ModAssembly).Path)
$wrappers = [System.Collections.Generic.List[Mono.Cecil.AssemblyDefinition]]::new()
try {
    foreach ($file in @('Assembly-CSharp.dll', 'Il2CppMashBox.Core.Runtime.dll', 'Il2CppFusion.Runtime.dll')) {
        $wrappers.Add([Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameRoot "MelonLoader\Il2CppAssemblies\$file")))
    }
    if (-not ($mod.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'MelonLoader.HarmonyDontPatchAllAttribute' })) {
        throw 'MelonLoader automatic blanket patching is not disabled.'
    }
    foreach ($type in $mod.MainModule.GetTypes()) {
        foreach ($attribute in $type.CustomAttributes) {
            if ($attribute.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch') { throw "Unexpected automatic patch class: $($type.FullName)" }
        }
        foreach ($method in $type.Methods) {
            foreach ($instruction in $method.Body.Instructions) {
                if ($instruction.Operand -is [Mono.Cecil.MethodReference] -and
                    $instruction.Operand.DeclaringType.FullName -eq 'HarmonyLib.Harmony' -and
                    $instruction.Operand.Name -eq 'PatchAll') {
                    throw "Blanket PatchAll remains in $($method.FullName)"
                }
            }
        }
    }
    $main = $mod.MainModule.GetType('rowemod.Main')
    $early = $main.Methods | Where-Object Name -eq 'OnEarlyInitializeMelon'
    if ($early.Body.Instructions | Where-Object { $_.Operand -match '::(Install|InstallNativeHooks|Patch)\(' }) {
        throw 'Native installation still reachable directly from early initialization.'
    }
    $late = $main.Methods | Where-Object Name -eq 'OnLateInitializeMelon'
    foreach ($name in @('TrickTweakGuard', 'HangFiveControl', 'LateNativeHooks')) {
        $calls = @($late.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq $name
        })
        if ($calls.Count -ne 1) { throw "Expected one late registration for $name, found $($calls.Count)." }
    }

    $specs = @(
        @('HostPlayerLimitHostSetupPatch', 'FusionBootstrap', 'DrawServerBrowserHostMapControls', 1),
        @('HostPlayerLimitStartGamePatch', 'NetworkRunner', 'StartGame', 1),
        @('PlayerUserNameTargetSpawnPatch', 'NetworkPlayer', 'Spawned', 0),
        @('CustomHairRenderPolicyPatch', 'EquipSlot', 'ApplyRenderPolicy', 0),
        @('CustomEquipCompletionPatch', '_EnumEquip_d__80', 'MoveNext', 0),
        @('ManualIkHumanPatch', 'HumanIK', 'OnAnimatorIK', 1),
        @('ManualIkNativeLimbPatch', 'UnityIKLimb', 'UpdateIK', 1),
        @('ManualIkPedalRigPatch', 'VehicleFootPedalAnimationRig', 'LateUpdate', 0),
        @('BikeGrindPoserInputRemapPatch', 'BikeGrindPoser', 'SetInputData', 2),
        @('OnePointOhQuaternionDrivePatch', 'QuaternionPDDrive', 'Tick', 2),
        @('DroneControllerLocalSpawnBulletPatch', 'DroneController', 'LocalSpawnBullet', 3),
        @('DroneControllerRpcFireBulletPatch', 'DroneController', 'RPC_FireBullet', 3)
    )
    $registrar = $mod.MainModule.GetType('rowemod.Mods.LateNativeHooks')
    $install = $registrar.Methods | Where-Object Name -eq 'Install'
    $newHooks = @($install.Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'newobj' -and $_.Operand.DeclaringType.FullName -eq 'rowemod.Mods.LateNativeHooks/Hook'
    })
    if ($newHooks.Count -ne $specs.Count) { throw 'Registry target count differs from audited coverage.' }
    $patchCount = 0
    foreach ($spec in $specs) {
        $patch = $mod.MainModule.GetType("rowemod.Mods.$($spec[0])")
        if ($null -eq $patch) { throw "Missing patch class $($spec[0])" }
        $references = @($install.Body.Instructions | Where-Object {
            $_.OpCode.Name -eq 'ldtoken' -and $_.Operand.FullName -eq $patch.FullName
        })
        if ($references.Count -ne 1) { throw "Patch not registered exactly once: $($patch.Name)" }
        $targets = @(
            foreach ($wrapper in $wrappers) {
                foreach ($type in $wrapper.MainModule.GetTypes()) {
                    if ($type.Name -ne $spec[1]) { continue }
                    foreach ($method in $type.Methods) {
                        if ($method.Name -eq $spec[2] -and $method.Parameters.Count -eq $spec[3]) { $method }
                    }
                }
            }
        )
        if ($targets.Count -ne 1) { throw "Ambiguous/missing target $($spec[1]).$($spec[2])" }
        $target = $targets[0]
        foreach ($method in $patch.Methods | Where-Object { $_.Name -in @('Prefix', 'Postfix') }) {
            $patchCount++
            if (-not $method.IsStatic) { throw "Non-static patch: $($method.FullName)" }
            foreach ($parameter in $method.Parameters) {
                $actual = $parameter.ParameterType.FullName.TrimEnd('&')
                if ($parameter.Name -eq '__instance') { $expected = $target.DeclaringType.FullName }
                elseif ($parameter.Name -eq '__runOriginal') { $expected = 'System.Boolean' }
                elseif ($parameter.Name -eq '__result') { $expected = $target.ReturnType.FullName }
                elseif ($parameter.Name -match '^__(\d+)$') {
                    $index = [int]$Matches[1]
                    if ($index -ge $target.Parameters.Count) { throw "Argument index out of bounds: $($method.FullName)" }
                    $expected = $target.Parameters[$index].ParameterType.FullName.TrimEnd('&')
                }
                else { throw "Fragile named argument '$($parameter.Name)' in $($method.FullName)" }
                if ($actual -ne $expected) { throw "Patch argument mismatch: $($method.FullName) $actual vs $expected" }
            }
        }
    }
    Write-Output "PASS: no blanket/automatic/early patching; all $($specs.Count) late targets covered; $patchCount real compiled prefix/postfix signatures match installed wrappers."
}
finally {
    $mod.Dispose()
    foreach ($wrapper in $wrappers) { $wrapper.Dispose() }
}
