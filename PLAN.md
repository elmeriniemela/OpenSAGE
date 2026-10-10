# Complete Generals and Zero Hour single player

## Summary

Implement all official offline content from /home/elmeri/Projects/ZeroHour: training, both games’ campaigns, skirmish, and Zero Hour’s Generals Challenge, including all supported difficulties and selectable factions/generals.

Completion means full player-facing functionality: gameplay, AI, objectives, progression, menus, controls, settings, saves, audio, and cinematics. Minor visual differences are acceptable.

The installed campaign definitions contain 21 Generals missions, training, 15 Zero Hour campaign missions, and Challenge routes. Current campaign progression, scripting, skirmish AI, and save functionality require substantial development.

## Architecture and interfaces

- Keep the existing engine and asset parsers. Separate simulation from window, rendering, and audio devices sufficiently to run automated scenarios without a GPU; use the same simulation code during normal gameplay.
- Introduce typed single-player startup options covering game, mode, campaign/mission, difficulty, skirmish configuration, and random seed. Route existing startup methods through one session lifecycle.
- Extend CampaignManager to own active campaign state, mission progression, difficulty, Challenge persona, and outcome. Mission scripts report victory/defeat through this manager.
- Add a versioned OpenSAGE save format using .osav, separate from original .sav files. Store explicit simulation snapshots; reuse existing serializers only where read/write symmetry is verified.
- Add injectable video playback and headless rendering/audio interfaces. Use a system FFmpeg decoder backend initially; the installed FFmpeg supports Bink video and audio.
- Retain .NET 8 and existing renderer choices. Arch Linux with Vulkan is the primary gameplay acceptance platform; preserve existing Windows and Linux CI builds.

## Implementation milestones

Implement these in dependency order, using small PRs with regression tests and a stated completion gate.

### 1. Establish the content inventory and test harness

- Index both installations through OpenSAGE’s actual filesystem resolution, including patch precedence, Zero Hour overrides, base-game dependencies, loose files, and case-insensitive asset names.
- Produce a machine-readable compatibility manifest covering menu-accessible campaigns, Challenge routes, official skirmish maps, factions, object modules, orders, script actions/conditions, GUI callbacks, and referenced media.
- Include cinematic maps and other dependencies reached by official content. Exclude unreachable demos, unused definitions, and developer test maps from completion requirements.
- Identify unsupported, partially implemented, and silently ignored behavior. A registered handler containing an empty implementation does not count as support.
- Add a headless scenario runner with fixed seeds, scripted player orders, bounded simulation ticks, assertions, and structured failure reports. Report the originating map, script, object, and feature for failures.

Gate: every official entry point and its dependencies are catalogued; missing behavior is visible and reproducible rather than silently skipped.

### 2. Stabilize session lifecycle and basic play

- Centralize session creation, restart, teardown, and return to menu. Correct timer resets, object disposal, event subscriptions, and asset-scope ownership.
- Complete pause/resume, Escape menu handling, restart, surrender, loading screens, and victory/defeat transitions.
- Finish both mouse modes, selection modifiers, control groups, contextual commands, minimap interactions, camera controls, and command cancellation.
- Implement persistent gameplay settings, including input, scrolling, audio volumes, resolution, fullscreen, and supported graphics options.
- Convert the crashes already encountered—scheduler corruption, AI initialization, landing physics, body damage recursion, and Linux audio—into permanent regression coverage.

Gate: repeated start → play → restart → exit cycles work without crashes, stale state, stuck input, or accumulating resources.

### 3. Complete the shared gameplay simulation

Use the inventory to implement every gameplay behavior required by official content:

- Economy, supply collection, construction, repair, production queues, prerequisites, upgrades, power, experience, veterancy, ranks, and general powers.
- Ground movement, formation movement, dynamic obstacles, bridges, aircraft, docking, transports, garrisons, tunnels, and terrain restrictions.
- Target acquisition, attack-move, guard, retaliation, weapons, projectiles, armor, damage types, death effects, capture, stealth, detection, mines, and superweapons.
- Fog of war, shroud, radar visibility, and consistent visibility rules for rendering, commands, targeting, scripts, and AI.
- All stock faction and general-specific mechanics, driven by their existing definitions rather than mission-specific shortcuts.

Implement each subsystem alongside focused scenarios and snapshot serialization support.

Gate: every stock buildable unit, building, upgrade, and ability passes its behavior scenarios; no required gameplay path reaches an unimplemented handler.

### 4. Complete mission scripting and campaigns

- Implement the actions and conditions actually used by the inventoried content: teams, spawning, waypoints, attack behavior, triggers, counters, timers, objectives, diplomacy, ownership, reinforcements, camera sequences, speech, and mission outcomes.
- Preserve script execution order, difficulty filtering, activation rules, sequential execution, and timing.
- Connect campaign startup and progression to FirstMission, NextMission, briefing, intro movie, and final victory definitions.
- Handle mission failure, retry, campaign completion, and persistent progress. Follow configured mission identifiers rather than assuming consecutive map filenames.
- Begin with training and the first Generals USA mission as complete playable slices, then expand through all campaigns in definition order.
- Implement shared features when a mission exposes a gap; avoid hardcoded exceptions for individual missions.

Gate: all campaign missions can be completed through normal gameplay on every difficulty, with correct objectives, transitions, and progression.

### 5. Complete skirmish AI and Generals Challenge

- Implement AI economy, base placement, construction, production, scouting, defense, attack teams, rebuilding, upgrades, and powers.
- Use installed AI definitions and scripts for faction behavior and difficulty differences. Apply only the advantages specified by the original data.
- Complete skirmish setup, random selections, teams, alliances, starting positions, win/loss conditions, and results.
- Implement Challenge general selection, biographies, opponent routing, taunts, special rules, boss encounters, retries, and progression from the installed definitions.
- Exercise aircraft, transports, tunnels, stealth, and faction-specific powers in AI scenarios.

Gate: every selectable faction/general operates independently; every official skirmish map completes automated matches; every selectable Challenge route is playable to completion.

### 6. Finish saves, profiles, and presentation

- Save and restore objects, stable references, AI states, queues, projectiles, timers, scripts, RNG state, visibility, campaign/Challenge state, and simulation clocks.
- Restore object references in two passes, then rebuild spatial indexes and scheduler queues before resuming simulation.
- Implement save/load menus, named saves, overwrite/delete confirmation, autosaves, quicksave/quickload, and clear handling of corrupt or incompatible saves.
- Write saves and profiles atomically. Keep campaign progress, completed difficulties, honors/statistics, and settings in a separate versioned profile.
- Complete Bink playback, audio synchronization, skipping, looping menu videos, briefings, speech, music transitions, and in-engine cinematics.
- Finish original GUI callbacks, tooltips, HUD feedback, localization, effects, and animations needed to understand gameplay.

Gate: campaign, Challenge, and skirmish sessions resume correctly after restarting the application; required media and interface flows work throughout each mode.

## Validation and release gates

- Unit tests: deterministic mechanics, script semantics, AI transitions, lifecycle cleanup, save serialization, and input behavior.
- Asset-backed integration tests: load both installations, resolve all required dependencies, and fail on unsupported behavior. Run these on a private/local runner with access to the assets.
- Simulation tests: fixed-seed matches and mission checkpoints; compare uninterrupted runs with save/reload runs using simulation-state hashes that exclude presentation state.
- Windows comparisons: record representative original-game behavior for each subsystem, campaign objectives, difficulty differences, Challenge progression, and audiovisual sequences. Use these as acceptance evidence.
- Full-content acceptance: complete every campaign and selectable Challenge route on every difficulty. Test every official skirmish map and every faction/general, with additional team and maximum-slot scenarios.
- Stability: run two-hour representative matches and 20 consecutive session transitions without crashes, simulation corruption, or growing counts of live session resources.
- CI: keep public tests independent of proprietary assets; run the full asset-dependent suite explicitly before milestone completion. Missing assets must be reported as skipped validation, never as a successful full-content check.

Track progress in docs/single-player-roadmap.md, with each feature linked to its PRs, automated tests, and gameplay acceptance evidence. Completion requires passing these gates, not merely eliminating TODO markers.

## Assumptions and boundaries

- Official content only. Multiplayer, replay functionality, arbitrary mods, custom-map compatibility, and World Builder are outside this completion target.
- Reliable OpenSAGE saves are required; importing original Windows saves remains a separate compatibility milestone.
- Original Windows gameplay provides the behavioral reference; Wine setup is excluded.
- Assets remain external and read-only. Tests and releases do not redistribute them.
- Existing local fixes are reviewed and retained where valid. Implementation follows repository contribution rules and is delivered incrementally rather than as one large PR.