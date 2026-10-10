# Generals and Zero Hour single-player roadmap

The acceptance target is defined in [PLAN.md](../PLAN.md). No milestone gate has
passed. Changes in this working tree are the first incremental implementation;
no PR has been opened and no original-game gameplay comparison has been recorded.

| Milestone | Status | Remaining completion evidence |
| --- | --- | --- |
| 1. Content inventory and test harness | In progress: resolved asset index, official campaign routes, enabled Challenge personas, official skirmish maps, playable factions, map objects, script usage, movie resolution | GUI and cinematic dependency traversal, object modules and orders, transitive dependencies, audio resolution, bounded headless gameplay scenarios |
| 2. Session lifecycle and basic play | Pending | Lifecycle cleanup, controls, persistent settings, crash regressions, 20 transitions |
| 3. Shared gameplay simulation | Pending | Stock mechanics, faction features, behavior scenarios and snapshot support |
| 4. Mission scripting and campaigns | Pending | Script semantics, training and USA mission slice, all campaigns and difficulties |
| 5. Skirmish AI and Challenge | Pending | Definition-driven AI, skirmish setup/results, every selectable Challenge route |
| 6. Saves, profiles and presentation | Pending | Versioned snapshots/profiles, save/reload comparisons, video/audio and GUI flows |

## First increment: filesystem and content inventory

The inventory uses `GameInstallation.CreateFileSystem`, `IniParser` and `MapFile`;
it creates no window, renderer or audio device. It indexes the winning resolved
asset paths with source disk/archive paths and lengths. The same filesystem fixes
apply during gameplay:

- Loose-file and directory lookup ignores case on Linux.
- Composite enumeration deduplicates asset names regardless of case or slash style.
- Uppercase `.BIG` archives are included; base-game loose files are available to
  expansion installations after expansion overrides.
- Degenerate polygon triggers consume their point data before being ignored. This
  fixes map-parser alignment errors found in training and other official maps.

The initial campaign roots are the stock menu identifiers `TRAINING`, `USA`, `GLA`
and `China`. Enabled Challenge personas add their configured campaigns. The
inventory follows `FirstMission` and `NextMission`, reports missing links/cycles,
and excludes other campaigns and unreachable mission definitions from required
maps. Menu callback traversal is still needed to verify all entry points. Official
multiplayer map-cache entries supply skirmish maps and player-slot counts.

Script usage includes map, player, group/script path, true/false/condition branch,
feature type, internal name and enabled flag. Status values are:

- `MissingHandler`: no handler applies to this game.
- `EmptyHandler`: a registered void handler has an empty compiled body.
- `RegisteredUnverified`: a handler exists; behavior has **not** been verified.

Game-specific handler overloads take precedence over general fallbacks. These
statuses do not establish script reachability at runtime or implementation
correctness. Constant-returning conditions, partial implementations and throwing
handlers remain unverified. Object types are inventoried but their modules and
behavior are not certified. Ignored polygon geometry can still produce parser
warnings. No simulation scenario runner exists yet.

Referenced campaign/script movies resolve through video definitions and the
composite filesystem. Missing definitions/files produce structured failures.
Speech, music and sound names are collected with `Resolved: null`; audio file
resolution is pending. The manifest includes `PendingCoverage` and always sets
`CompletionGatePassed: false` so an inventory cannot imply full-content acceptance.

## Running the inventory

Assets are external, read-only and never copied into the repository. Run locally:

```sh
dotnet run --project src/OpenSage.Tools.ContentInventory -- \
  '/home/elmeri/Projects/ZeroHour/Command and Conquer Generals' \
  '/home/elmeri/Projects/ZeroHour/Command and Conquer Generals Zero Hour' \
  /tmp/opensage-content-manifest.json
```

The JSON contains one versioned manifest per installation. Exit code `0` means
inventory generation completed without file/definition/map/movie failures;
`1` means inventory failures were found or generation failed; `2` means invalid
arguments or missing installations. Missing installations are explicitly reported
as skipped asset validation. These exit codes never certify gameplay support.
Generated reports contain local installation paths and should stay outside the
repository.

## Automated coverage and local evidence

Public regression tests use temporary loose files, synthetic BIG archives and an
existing repository map fixture; they require no game installation or GPU:

```sh
dotnet test src/OpenSage.Game.Tests --filter \
  'FullyQualifiedName~FileSystemResolutionTests|FullyQualifiedName~ContentInventoryTests|FullyQualifiedName~PolygonTriggerTests'
```

- [Filesystem resolution tests](../src/OpenSage.Game.Tests/IO/FileSystemResolutionTests.cs):
  case-insensitive lookup/enumeration, loose/patch/expansion/base precedence,
  uppercase archives, base loose dependencies and enumeration/lookup consistency.
- [Inventory tests](../src/OpenSage.Game.Tests/Diagnostics/ContentInventoryTests.cs):
  nonconsecutive mission routes, unreachable definitions, missing movie references,
  broken/cyclic routes, enabled Challenge routes, map script traversal and empty
  handler classification.
- [Polygon trigger tests](../src/OpenSage.Game.Tests/Logic/Map/PolygonTriggerTests.cs):
  zero-, one- and two-point ignored triggers leave the next trigger aligned.

Local validation: the inventory tool and game test project build with zero warnings
and errors. The noninteractive game suite passes 655 tests with 3 existing skips,
including all 11 new regression cases. The CLI also reports skipped validation
and returns exit code `2` for a missing installation directory. Windows and macOS
builds have not been run locally.

Local asset-backed scan on 2026-10-10, following the polygon fix:

| Installation | Resolved assets | Required campaigns | Required maps | Official skirmish maps |
| --- | ---: | ---: | ---: | ---: |
| Generals | 14,191 | 4 (21 campaign missions plus training) | 45 | 23 |
| Zero Hour | 25,661 | 13 (15 campaign missions, training, 9 Challenge routes) | 88 | 64 |

All 133 inventoried map entries parse. The scan finds 17 unresolved Zero Hour movie
references in this installation; the report retains each originating campaign or
mission. Many script usages have missing or unverified handlers. These are
inventory observations, not gameplay acceptance evidence. Full transitive content
coverage, headless simulation, Windows behavior comparisons, all-difficulty
playthroughs, save/reload state hashes and long stability runs remain outstanding.

Next: extend dependency traversal and implement a bounded scenario runner using
the same simulation code as gameplay, before declaring milestone 1 complete.
