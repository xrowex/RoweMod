# Streets update compatibility check, 2026-10-22

Steam build 26117410 reports game version 2.0.0.3068.0 (2026-10-21).
Its GameAssembly SHA256 is `3EBD5AB08931C3BB35E1264739975F0EB6FD2AD56EDFD99E5FA48DA7EABD6E85`.

The installed RoweMod build rejected this unknown native binary. A fresh startup
confirmed seven skipped hook groups, unavailable tweak/nose-manual guards, and
Bust/Hat/Hair material completion timeouts. There was no recurrence of the old
hard-reference coroutine TypeLoadException.

Added a separate, exact-hash native profile after regenerating interop and Cpp2IL
metadata. All 17 targets moved; their signatures, including the equip coroutine
and drone methods, are unchanged from the preceding update. Each address has
one owner across 201,418 methods in 162 assemblies. The unsafe shared getter at
0x6BE680 has 500 aliases and remains rejected. Earlier profiles remain supported.
See `native-hook-audit-20261022.json` for the target list.

Validation:

- Isolated Release build: zero errors, 51 existing warnings.
- 235 automated assertions/checks passed across native registration (original,
  updated, and missing-coroutine layouts), trick guards, menu lifecycle, emote
  downloads/cache, and animation packages.
- All 1,645 compiled wrapper member references resolve against both the preceding
  and current interop. All 16 compiled patch signatures match both wrapper sets.
- Fresh fixed-build startup: 7/7 native groups, tweak guard, and nose-manual guard
  ready. Bust, Hat, and Hair native equips completed and their materials applied.
  Hair settled active/enabled with forceOff=False and hiddenFlag=False.
- No exceptions, skipped groups, or equip timeouts in the fixed startup log.

The 3.3.11 release includes the per-trick speed UI. The original local DLL,
configuration, interop, and startup log are backed up under
`outputs/streets-update-20261022/installed-backup/`. The test process was stopped
and the original configuration restored byte-for-byte. This check was initially
installed locally; the subsequent 3.3.11 release packages the compatibility fix
and per-trick speed UI. See `releases/3.3.11.md` for release scope and validation.

Local validation receipt and before/after logs are under
`outputs/streets-update-20261022/`. Startup and outfit checks do not establish
riding/controller, per-trick-speed behavior, or multiplayer acceptance.
