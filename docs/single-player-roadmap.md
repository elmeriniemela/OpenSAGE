# Generals and Zero Hour single-player roadmap

The acceptance target is defined in [PLAN.md](../PLAN.md). No milestone gate has
passed. Changes in this working tree are the first incremental implementation;
no PR has been opened and no original-game gameplay comparison has been recorded.

| Milestone | Status | Remaining completion evidence |
| --- | --- | --- |
| 1. Content inventory and test harness | In progress: content index, official routes, script/media inventory, bounded CPU scenario core | GUI and cinematic dependency traversal, object modules and orders, transitive dependencies, audio resolution, official-map headless initialization and gameplay scenarios |
| 2. Session lifecycle and basic play | Pending | Lifecycle cleanup, controls, persistent settings, crash regressions, 20 transitions |
| 3. Shared gameplay simulation | Pending | Stock mechanics, faction features, behavior scenarios and snapshot support |
| 4. Mission scripting and campaigns | In progress: interval/one-shot evaluation, enabled/inverted flags, nested groups | Remaining script semantics, training and USA mission slice, all campaigns and difficulties |
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
warnings. This initial inventory does not run simulation.

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

Next: extend dependency traversal and official-map headless initialization so
bounded scenarios exercise required content before declaring milestone 1 complete.

## Development tooling

[The verification runner](../scripts/verify.py) provides quick, solution-wide and
asset modes, cached dependency restore, serialized builds, bounded command times,
TRX failure extraction and versioned JSON reports. [Content queries](../scripts/content_report.py)
inspect an existing manifest without rescanning the installation. The ignored
local configuration stores installation paths. [Repository instructions](../AGENTS.md)
and [the workflow guide](development-workflow.md) describe the edit/test loop;
[the reference template](behavior-references/README.md) records original-game evidence.

[Tooling regressions](../scripts/tests/test_verification.py) cover failed builds,
missing assets, stale restores, zero-test filters, skipped-test reporting, command
timeouts, overlapping runs and bounded content queries. Local Release validation
passes 703 .NET tests with three existing skips; the tooling suite passes 17 tests.
The separate tooling CI workflow targets Windows, Linux and macOS; only Linux has
been validated locally. These tools support milestone 1; asset-backed headless
gameplay scenarios and full-content acceptance gates remain outstanding.

## Device-independent scripting groundwork

Camera fade state no longer allocates a drawing context during scripting-system
construction. Presentation resources are created on the first rendered fade;
script counters and conditions can now run against the production scripting
system in the CPU-only game fixture, without platform startup or installed assets.

[Script execution tests](../src/OpenSage.Game.Tests/Scripting/ScriptExecutionTests.cs)
cover repeated interval evaluations at each intervening tick for both games,
every-frame evaluation, false-branch retries, successful one-shot deactivation
(including empty action lists), inactive scripts and explicit subroutine calls.
The countdown previously assigned a post-decrement back to itself and never
reached zero after its first evaluation. Evaluation now repeats at the configured
interval in scripting ticks. DeactivateUponSuccess now follows the condition's
success rather than whether the selected branch contains actions.

[Counter comparison tests](../src/OpenSage.Game.Tests/Scripting/ScriptConditionsTests.cs)
now execute all 16 cases using that device-independent fixture instead of a skipped
installation-dependent fixture. Local Linux Release validation on 2026-10-10:
26 focused scripting checks pass; full solution verification passes 729 .NET tests
with two asset-dependent save tests skipped. The 17 tooling regressions also pass.
Reproduce with:

```sh
./scripts/verify.sh quick --configuration Release --filter 'FullyQualifiedName~Scripting.Script'
./scripts/verify.sh full --configuration Release
```

This is groundwork for the milestone 1 runner, not a complete headless session.
Official-map initialization remains outstanding. No gameplay comparison or
full-content acceptance is claimed by these synthetic regressions.

## Bounded CPU scenario core

[The scenario runner](../src/OpenSage.Game/Diagnostics/SimulationScenarioRunner.cs)
uses [the same tick pipeline as windowed gameplay](../src/OpenSage.Game/Logic/SimulationTick.cs):
scheduler, network/player orders, scene/object logic and partition updates, followed
by scripting. It initializes simulation/client RNGs before scenario setup, advances
an integer simulation clock without sleeping and enforces an explicit tick bound.
Scheduled orders preserve insertion order within a tick; assertions run afterward.
Every run requires a fresh host, even if setup fails before its first logic tick.
Zero assertions and out-of-bound schedules are rejected rather than silently passed.

Strict scenario validation fails on missing/empty script actions, missing conditions
and orders reaching the unimplemented default dispatch branch. It restores the
host's validation settings after success, cancellation or failure. Scheduler,
order and script exceptions retain feature/object/script context. Failed assertions
can supply explicit map, script, object and feature origins. Versioned JSON reports
can be written atomically with `SimulationScenarioReport.WriteTo`.

[Public scenario regressions](../src/OpenSage.Game.Tests/Diagnostics/SimulationScenarioRunnerTests.cs)
exercise both games with real selection/control-group orders, object logic, script
counters, reproducible seeded traces, failure origins, cancellation and JSON output.
They also expose and fix ignored script enabled/inverted flags and skipped nested
groups. Disabled conditions contribute no restriction to their AND clause, disabled
actions do not dispatch, and nested groups execute before their parent's scripts.

The current host is a synthetic test scene with no drawing/audio device. Unsupported
scene operations throw; official terrain/navigation, graphics-independent stock
drawables, media interfaces and complete asset-backed session creation are pending.
Partial handlers and explicit empty order cases still need behavior coverage.
Passing these scenarios does not prove official mission playability or original-game
equivalence. See [scenario integration and limitations](headless-scenarios.md).

Local Linux verification on 2026-10-10: all 23 scenario checks pass in Debug;
full Release solution verification passes 752 .NET tests with two asset-dependent
save tests skipped. The 17 Python tooling regressions pass in both runs. No asset
rescan was needed: this increment changes runtime execution, not asset resolution.
Reproduce with the focused scenario command in the linked guide and
`./scripts/verify.sh full --configuration Release`. Windows behavior comparison,
official-map headless runs and milestone completion remain unverified.

## Handoff: next implementation steps

Continue in dependency order. Keep the full PLAN.md completion gates; the checklist
below records the next work rather than replacing those gates.

1. **Create a production headless session.** Separate `Scene3D` simulation services
   from its GPU terrain, render buckets, particle systems, tactical view and input
   handlers. Add injectable audio/video services and a drawable path that preserves
   simulation-required model state. Use the same player, object, navigation,
   scheduler, scripting and order implementations as normal gameplay. Fail with a
   named feature when required simulation data still depends on presentation.
2. **Load a real map in that session.** Add an asset-backed scenario command that
   accepts game installation, map/campaign route, difficulty, seed, tick bound,
   orders and assertions. Load the resolved map, players, teams, objects, scripts,
   terrain logic, navigation, partition and campaign state using normal precedence.
   Keep assets read-only. Emit the scenario JSON report and identify map, player,
   script/group, object and feature for every failure.
3. **Turn the inventory into executable coverage.** Add object module and player
   order indexes, GUI callbacks, cinematic dependencies and audio resolution. Use
   each content edge to create bounded loading/scenario checks; make missing,
   empty, unsupported and unverified behavior distinct. Do not count a registered
   handler or a successful load as implemented behavior.
4. **Finish lifecycle and controls (milestone 2).** Repeatedly start, pause, resume,
   restart, surrender, load and return to menu. Verify cleanup, mouse modes,
   selection/groups, contextual commands, minimap/camera, settings and existing
   scheduler/AI/physics/audio crash regressions. Pass 20 transitions without stale
   state or increasing live resources.
5. **Build scenario coverage by gameplay dependency (milestones 3–5).** Add
   deterministic economy, production, movement, combat, vision, faction mechanics,
   scripting, objectives, campaign transitions, skirmish AI and Challenge tests.
   Start with training and the first USA mission, then expand through definition
   routes and every difficulty, faction and official map. Record Windows reference
   behavior for each accepted subsystem.
6. **Add snapshots and presentation (milestone 6).** Serialize simulation clocks,
   RNG, stable object references, AI, queues, scripts, visibility and campaign
   state into versioned `.osav` profiles; test uninterrupted versus reload hashes.
   Then complete atomic save/profile UI, video/audio synchronization and required
   GUI feedback.
7. **Run the release gates.** Perform asset-backed route/dependency scans, every
   campaign/Challenge playthrough on every difficulty, skirmish map/faction runs,
   save/reload comparisons, two-hour matches and 20 session transitions. Run the
   full asset suite on the private installation host. Missing assets or known movie
   references stay reported as failures/skips, never as a full pass.

Immediate starting point: inspect `Scene3D` construction and `Terrain`/`Drawable`
resource creation, then extract the CPU map/session services while retaining the
existing `SimulationTick` entry point. The current synthetic scene is test-only;
there is no asset-backed headless session yet.
