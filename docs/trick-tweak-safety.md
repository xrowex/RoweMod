# Trick-tweak guard: IL2CPP safety audit (2026-08-27)

## Root cause

The previous native hook on `TweakState.get_CanEnterState()` was unsafe. In the
installed game's original Cpp2IL metadata, that method and
`Fusion.NetworkAssetSourceStatic<T>.get_IsCompleted()` both have native RVA
`0x66E640`, offset `0x66D840`, length 3. Their code is just `mov al,1; ret`.
The complete metadata scan found **492 methods** at that RVA. Forcing that shared
body to return false also made static network prefabs appear unfinished.

The log's `Blocked legacy TweakState transition` lines before a rider existed
were NOT proof of blocking real tricks. Fusion then failed to spawn prefabs 29
and 4. Removing the shared hook corrects the identified patching error; a fresh
in-map spawn still needs verification. Existing missing mod.io asset warnings
are a separate observation, not something this change claims to fix.

## Selected approach

Use MelonLoader's installed **HarmonyX + Il2CppInterop native backend**, not a
managed-wrapper-only patch or a handwritten native detour:

- Register only two explicit, non-generic void-method prefixes after IL2CPP
  support is initialized. No property, legacy-state, or Awake patches remain.
- `TrickControllerV2.RequestTweak()` gates manual requests; `Tweak()` gates the
  actual manual/automatic transition. Native `Update()` calls the latter, so
  the transition guard also covers an inlined or otherwise bypassed request.
- Return false only for the cached **local rider's** controller while the option
  is enabled. Leave remote riders, unbound objects, menus and normal trick
  enter/loop/exit/custom clips alone.
- Bind from the existing local-player-spawn event. Capture and disable local
  `autoTweak` once on enabling/spawn, and restore its original value on disable,
  scene change or cleanup. No scene-wide searches, new Update callback, setter
  hook or per-frame override. Block logs are capped before formatting.
- Use no argument-name-dependent setter patch, ref-bool native signature,
  generic-state-machine patch, transpiler, or Fusion workaround.

Filtering by `__instance` is useful on an already verified unique target. It is
not our recovery strategy for a shared native address: the interop trampoline
itself is created for one signature/type before the prefix can filter anything.
Likewise, `OnFiredTrick` is a notification, not an established cancellable tweak
request; a callback after playback starts cannot safely replace the gate.

## Compatibility policy

Audited `GameAssembly.dll` SHA256:
`3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1`.

| Target | RVA | Metadata owners |
| --- | --- | --- |
| `TrickControllerV2.RequestTweak()` | `0x8B0A50` | 1 |
| `TrickControllerV2.Tweak()` | `0x8B0E30` | 1 |

The audit traversed 193,019 methods in 164 original metadata assemblies,
including nested types. This is metadata-backed uniqueness for this binary,
not a guarantee for future game builds.

Startup checks the binary hash once, both live native RVAs (relative to the
loaded module, so ASLR is accounted for), signatures and native patcher type
before installing either prefix. Unknown binaries/metadata disable only this
feature and emit a diagnostic; no unsafe fallback is installed. A partial
installation removes only our specific prefixes. A game update therefore
requires regenerating metadata, re-auditing addresses and updating the verified
profile. Do not substitute a broad 'version >= ...' check.

## Verification

Read-only native audit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Scripts\Test-TrickTweakNativeTargets.ps1
```

Guard logic tests (real guard source linked against test doubles):

```powershell
dotnet run --project Tests\TrickTweakGuard\TrickTweakGuard.Tests.csproj -c Release
```

24 assertions cover unverified builds, unbound/missing local player, local vs
remote identity, pending-request clearing, manual and direct gates, bounded
logging, one-time writes, toggle restoration (original true AND false), scene
exit, replacement player, mod disable and cleanup. These do not simulate native
trampolines, Unity object destruction or animation playback.

Remaining in-game acceptance, after a complete restart:

1. Spawn in a map with Disable Trick Tweaking ON; confirm a real rider and the
   `Bound local V2 controller` log. There must be no legacy-state hook marker.
2. Perform and manually tweak a known tweak-capable trick; expect local block
   markers and unchanged normal/custom enter/loop/exit animation.
3. Turn the option OFF, perform the same trick, and verify its normal tweak
   behavior returns. An automatic-tweak case also needs a real enabled source.
4. Respawn/change map and repeat; verify no missing rider or remote-rider impact.

No in-map result is claimed from a successful build or these unit tests.

## Primary research

- [Il2CppInterop native patcher source](https://github.com/BepInEx/Il2CppInterop/blob/master/Il2CppInterop.HarmonySupport/Il2CppDetourMethodPatcher.cs): wraps the native MethodInfo and applies its detour to `MethodPointer`. The address, not merely the managed method name, determines what gets intercepted. Installed APIs were checked locally before use.
- [HarmonyX custom patch backend](https://github.com/BepInEx/HarmonyX/wiki/Custom-MethodPatcher): IL2CPP needs the appropriate resolver/backend registered before patching. Ordinary Harmony's native-method limitations must not be confused with HarmonyX's IL2CPP support.
- [Harmony prefix behavior](https://harmony.pardeike.net/articles/patching-prefix.html): returning false skips the original; returning true leaves the original call intact.
- [Harmony edge cases](https://harmony.pardeike.net/articles/patching-edgecases.html): inlining, shared generics and startup timing are hazards. Our unrelated non-generic identical-body collision is established by this game's native metadata, not inferred solely from the generic warning.

No dependency upgrade or third-party code copying was needed.
