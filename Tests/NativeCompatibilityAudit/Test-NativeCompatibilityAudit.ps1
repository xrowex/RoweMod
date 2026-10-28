$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$auditScript = Join-Path $repo 'Scripts/Test-StreetsBuildCompatibility.ps1'
$cecil = 'C:\Program Files (x86)\Steam\steamapps\common\BMX Streets\MelonLoader\net35\Mono.Cecil.dll'
Add-Type -Path $cecil
$temp = Join-Path ([IO.Path]::GetTempPath()) ('rowemod-compatibility-audit-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
$script:checks = 0
function Check([bool]$Value, [string]$Message) { if (!$Value) { throw $Message }; $script:checks++ }
function Fixture([string]$Name, [int]$Counter = 112, [switch]$Shared, [switch]$ChangedParameter, [switch]$Static, [switch]$Ambiguous, [switch]$OutsideImage, [switch]$NoAddress, [string]$TargetRva = '0x1100', [switch]$WrongCase) {
    $folder = Join-Path $temp $Name
    $metadata = Join-Path $folder 'metadata'
    New-Item -ItemType Directory -Path $metadata -Force | Out-Null
    $assembly = [Mono.Cecil.AssemblyDefinition]::CreateAssembly([Mono.Cecil.AssemblyNameDefinition]::new('Fixture', [Version]'1.0.0.0'), 'Fixture', [Mono.Cecil.ModuleKind]::Dll)
    try {
        $module = $assembly.MainModule
        $attribute = [Mono.Cecil.TypeDefinition]::new('Cpp2ILInjected', 'AddressAttribute', [Mono.Cecil.TypeAttributes]::Public, $module.TypeSystem.Object)
        $module.Types.Add($attribute)
        $attribute.Fields.Add([Mono.Cecil.FieldDefinition]::new('RVA', [Mono.Cecil.FieldAttributes]::Public, $module.TypeSystem.String))
        $ctor = [Mono.Cecil.MethodDefinition]::new('.ctor', ([Mono.Cecil.MethodAttributes]::Public -bor [Mono.Cecil.MethodAttributes]::SpecialName -bor [Mono.Cecil.MethodAttributes]::RTSpecialName), $module.TypeSystem.Void)
        $ctor.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ret)
        $attribute.Methods.Add($ctor)
        $owner = [Mono.Cecil.TypeDefinition]::new('Fixture', 'EquipSlot', [Mono.Cecil.TypeAttributes]::Public, $module.TypeSystem.Object)
        $module.Types.Add($owner)
        foreach ($index in 0..([int]$Ambiguous.IsPresent)) {
            $prefix = if ($WrongCase) { '<enumequip>d__' } else { '<EnumEquip>d__' }
            $nested = [Mono.Cecil.TypeDefinition]::new('', ($prefix + ($Counter + $index)), [Mono.Cecil.TypeAttributes]::NestedPublic, $module.TypeSystem.Object)
            $owner.NestedTypes.Add($nested)
            $attributes = [Mono.Cecil.MethodAttributes]::Public
            if ($Static) { $attributes = $attributes -bor [Mono.Cecil.MethodAttributes]::Static }
            $method = [Mono.Cecil.MethodDefinition]::new('MoveNext', $attributes, $module.TypeSystem.Boolean)
            if ($ChangedParameter) { $method.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($module.TypeSystem.Int32)) }
            $method.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0)
            $method.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ret)
            $nested.Methods.Add($method)
            if (!$NoAddress) {
                $address = [Mono.Cecil.CustomAttribute]::new($ctor)
                $rva = if ($OutsideImage) { '0x7FFFFFFF' } elseif ($index -eq 0) { $TargetRva } else { '0x1200' }
                $address.Fields.Add([Mono.Cecil.CustomAttributeNamedArgument]::new('RVA', [Mono.Cecil.CustomAttributeArgument]::new($module.TypeSystem.String, $rva)))
                $method.CustomAttributes.Add($address)
            }
        }
        if ($Shared) {
            $alias = [Mono.Cecil.MethodDefinition]::new('UnrelatedGetter', [Mono.Cecil.MethodAttributes]::Public, $module.TypeSystem.Boolean)
            $alias.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0)
            $alias.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ret)
            $address = [Mono.Cecil.CustomAttribute]::new($ctor)
            $address.Fields.Add([Mono.Cecil.CustomAttributeNamedArgument]::new('RVA', [Mono.Cecil.CustomAttributeArgument]::new($module.TypeSystem.String, '0x1100')))
            $alias.CustomAttributes.Add($address); $owner.Methods.Add($alias)
        }
        # A different coroutine with a valid MoveNext is never a substitute.
        $other = [Mono.Cecil.TypeDefinition]::new('', '<OtherRoutine>d__999', [Mono.Cecil.TypeAttributes]::NestedPublic, $module.TypeSystem.Object)
        $owner.NestedTypes.Add($other)
        $method = [Mono.Cecil.MethodDefinition]::new('MoveNext', [Mono.Cecil.MethodAttributes]::Public, $module.TypeSystem.Boolean)
        $method.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0); $method.Body.GetILProcessor().Emit([Mono.Cecil.Cil.OpCodes]::Ret)
        $other.Methods.Add($method)
        $assembly.Write((Join-Path $metadata 'Fixture.dll'))
    } finally { $assembly.Dispose() }
    Copy-Item -LiteralPath (Join-Path $metadata 'Fixture.dll') -Destination (Join-Path $folder 'GameAssembly.dll')
    $baseline = [ordered]@{ gameAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $folder 'GameAssembly.dll')).Hash; gameVersion='fixture'; steamBuildId='fixture'; targets=@(@{signature='System.Boolean Fixture.EquipSlot/<EnumEquip>d__112::MoveNext()';originalRva='0x1000';updatedRva='0x1100'}) }
    $baseline | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $folder 'baseline.json')
    $folder
}
function Run-Audit([string]$Folder, [switch]$Discover, [switch]$MustFail, [string]$ReportName = 'report') {
    $failed = $false
    $reportPath = Join-Path (Join-Path $temp 'reports') ((Split-Path $Folder -Leaf) + '-' + $ReportName)
    try { & $auditScript -GameRoot $Folder -MetadataRoot (Join-Path $Folder 'metadata') -BaselineProfile (Join-Path $Folder 'baseline.json') -ReportDirectory $reportPath -CecilAssemblyPath $cecil -AllowUnknownBuildForReport:$Discover | Out-Null }
    catch { $failed = $true }
    Check ($failed -eq $MustFail.IsPresent) ('Unexpected audit outcome: ' + $Folder)
    Get-Content -LiteralPath (Join-Path $reportPath 'compatibility-report.json') -Raw | ConvertFrom-Json
}
try {
    $current = Fixture 'current'
    $report = Run-Audit $current
    Check ($report.status -eq 'approved-profile-metadata-matches' -and $report.targets.Count -eq 1) 'Approved metadata is verified'
    Check (!$report.runtimeProfileModified -and $report.runtimeValidationRequired) 'Audit cannot enable runtime hooks'
    Check ($report.metadataFiles.Count -eq 1 -and $report.metadataSetSha256.Length -eq 64) 'Metadata inventory is fingerprinted'
    $failed = $false
    try { & $auditScript -GameRoot $current -MetadataRoot (Join-Path $current 'metadata') -BaselineProfile (Join-Path $current 'baseline.json') -ReportDirectory (Join-Path $temp 'reports/current-report') -CecilAssemblyPath $cecil | Out-Null } catch { $failed = $true }
    Check $failed 'Prior audit reports are never overwritten'
    foreach ($case in @('shared', 'parameter', 'static', 'ambiguous', 'outside', 'missing-address')) {
        $options = @{}
        switch ($case) { 'shared' { $options.Shared=$true }; 'parameter' { $options.ChangedParameter=$true }; 'static' { $options.Static=$true }; 'ambiguous' { $options.Ambiguous=$true }; 'outside' { $options.OutsideImage=$true }; 'missing-address' { $options.NoAddress=$true } }
        $folder = Fixture $case @options
        $report = Run-Audit $folder -MustFail
        Check ($report.status -eq 'blocked' -and $report.errors.Count -gt 0) ('Reject unsafe metadata: ' + $case)
        $candidatePath = Join-Path $temp ('reports/' + $case + '-report/candidate-native-profile.json')
        Check (!(Test-Path -LiteralPath $candidatePath)) ('No candidate for ' + $case)
    }
    $renamed = Fixture 'renamed' -Counter 113
    $report = Run-Audit $renamed -MustFail
    Check (@($report.errors -match 'Metadata differs').Count -gt 0) 'Counter drift in supposedly approved metadata is rejected'
    $baseline = Get-Content -LiteralPath (Join-Path $renamed 'baseline.json') -Raw | ConvertFrom-Json
    $baseline.gameAssemblySha256 = '0' * 64
    $baseline | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $renamed 'baseline.json')
    $report = Run-Audit $renamed -MustFail -ReportName 'unknown-blocked'
    Check (@($report.errors -match 'GameAssembly changed').Count -gt 0) 'Unknown build requires an explicit report-only request'
    $before = (Get-FileHash -LiteralPath (Join-Path $renamed 'baseline.json')).Hash
    $report = Run-Audit $renamed -Discover -ReportName 'discovered'
    Check ($report.status -eq 'candidate-requires-review' -and $report.renamedCoroutines -eq 1) 'Changed iterator counter is discovered by its exact semantic signature'
    Check ($report.targets[0].signature -eq 'System.Boolean Fixture.EquipSlot/<EnumEquip>d__113::MoveNext()') 'Unrelated coroutine is never selected'
    Check ((Get-FileHash -LiteralPath (Join-Path $renamed 'baseline.json')).Hash -eq $before) 'Discovery preserves the approved profile'
    $candidate = Get-Content -LiteralPath (Join-Path $temp 'reports/renamed-discovered/candidate-native-profile.json') -Raw | ConvertFrom-Json
    Check ($candidate.audit.candidateOnly -and $candidate.audit.requiresNativeRuntimeValidation) 'Candidate stays explicitly review-only'
    $moved = Fixture 'moved' -TargetRva '0x1500'
    $baseline = Get-Content -LiteralPath (Join-Path $moved 'baseline.json') -Raw | ConvertFrom-Json
    $baseline.gameAssemblySha256 = '0' * 64
    $baseline | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $moved 'baseline.json')
    $report = Run-Audit $moved -Discover
    Check ($report.changedAddresses -eq 1 -and $report.targets[0].updatedRva -eq '0x1500') 'Moved function is located without guessing a global offset'
    $different = Fixture 'case-sensitive' -WrongCase
    $baseline = Get-Content -LiteralPath (Join-Path $different 'baseline.json') -Raw | ConvertFrom-Json
    $baseline.gameAssemblySha256 = '0' * 64
    $baseline | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $different 'baseline.json')
    $report = Run-Audit $different -Discover -MustFail
    Check ($report.targets.Count -eq 0) 'Case-different routine is not silently treated as the expected method'
    $baseline = Get-Content -LiteralPath (Join-Path $current 'baseline.json') -Raw | ConvertFrom-Json
    $baseline | Add-Member -NotePropertyName audit -NotePropertyValue @{assemblies=2;methods=99}
    $baseline | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $current 'baseline.json')
    $report = Run-Audit $current -MustFail -ReportName 'incomplete'
    Check (@($report.errors -match 'inventory differs').Count -gt 0) 'Incomplete metadata cannot certify unique ownership'
    $failed = $false
    try { & $auditScript -GameRoot $current -MetadataRoot (Join-Path $current 'metadata') -BaselineProfile (Join-Path $current 'baseline.json') -ReportDirectory (Join-Path $current 'metadata/forbidden') -CecilAssemblyPath $cecil | Out-Null } catch { $failed = $true }
    Check $failed 'Reports cannot be written into game or metadata directories'
    Write-Output "PASS: $script:checks compatibility-audit fixture checks. Real Cpp2IL metadata is tested separately."
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    if (!$resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or
        ![IO.Path]::GetFileName($resolved).StartsWith('rowemod-compatibility-audit-test-', [StringComparison]::Ordinal)) { throw 'Unsafe fixture cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
