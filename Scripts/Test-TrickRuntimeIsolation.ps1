param(
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\bin\Release\net6.0\rowemod.dll'),
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\BMX Streets'
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameRoot 'MelonLoader\net35\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $ModAssembly).Path)
try {
    foreach ($name in @('rowemod.Mods.TrickMods','rowemod.Mods.TrickRuntimeContext')) {
        $type = $assembly.MainModule.GetType($name)
        if ($null -eq $type) { throw "Missing runtime type $name" }
        foreach ($method in $type.Methods) {
            foreach ($instruction in $method.Body.Instructions) {
                $target = $instruction.Operand
                if ($target -isnot [Mono.Cecil.MethodReference]) { continue }
                $owner = $target.DeclaringType.FullName
                if (($owner -eq 'UnityEngine.Behaviour' -and $target.Name -eq 'set_enabled') -or
                    ($owner -eq 'UnityEngine.Component' -and $target.Name -eq 'SendMessage') -or
                    ($owner -in @('UnityEngine.InputSystem.InputAction','UnityEngine.InputSystem.InputActionMap') -and $target.Name -in @('Enable','Disable'))) {
                    throw "Preset refresh must not change component/input enabled states: $($method.FullName) calls $target"
                }
            }
        }
    }
    Write-Output 'PASS: compiled trick preset/runtime code does not toggle components, broadcast initialization, or enable/disable action maps/actions.'
} finally { $assembly.Dispose() }
