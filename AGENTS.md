# Working in OpenSAGE

## Scope and source of truth

- Read the task and relevant code first. `PLAN.md` defines the single-player target;
  `docs/single-player-roadmap.md` records actual completion and outstanding gates.
- Follow `CONTRIBUTING.md` and `docs/coding-style.md`. Preserve existing user changes,
  including staged files. Do not commit, reset or rewrite them as part of verification.
- .NET 8 is pinned by `global.json`. Development tooling uses Python 3.9+ and its
  standard library; no pip dependencies, Ghidra or extra build tools are required.

## Fast verification

Run from any directory using the repository script's path. On Windows use
`python scripts/verify.py` in place of `./scripts/verify.sh`.

- Local edit cycle: `./scripts/verify.sh quick`.
- Focused regression: `./scripts/verify.sh quick --filter 'FullyQualifiedName~ClassName'`.
- Release-specific bug: add `--configuration Release` to the same command.
- Final repository check: `./scripts/verify.sh full --configuration Release`.
- Explicit asset-backed inventory: `./scripts/verify.sh assets`.
- Tooling-only check: `python3 -m unittest discover -s scripts/tests -v`.

The runner builds before testing, then uses `--no-build --no-restore`. Restore is
cached against SDK, project/package configuration and restored dependency graphs.
Keep `bin`/`obj`; use `--restore` when intentionally checking a fresh restore. The
runner defaults to one MSBuild worker because parallel restore/build has failed in
this restricted environment. Use `--jobs` only when the environment supports it.
Do not run overlapping builds against shared output directories. The runner locks
its own invocations; standalone `dotnet build` commands are outside that lock.

Inspect `.artifacts/verification/latest.json` for status, commands, durations and
report paths. Each run keeps separate build/test logs, TRX and `report.json`.
Failures include test messages/stacks; a zero-test filter fails verification.
Local restores skip NuGet vulnerability auditing for iteration speed; use `--audit`
for an audited restore. Existing CI remains responsible for its normal checks.

VSTest needs a loopback socket. If a tool run fails because the sandbox denies that
socket, use the environment's normal approval mechanism for the same verification
command, with that specific justification. The scripts never elevate themselves.

## Assets and known gaps

- Installation paths live in ignored `scripts/verification.local.json`; copy its
  example when configuring a new checkout. Overrides: `CNC_GENERALS_PATH`,
  `CNC_GENERALS_ZH_PATH`, or `--generals` / `--zero-hour`.
- Assets are external and read-only. Never copy proprietary assets into the repo.
  Asset reports contain local paths and stay under ignored `.artifacts`.
- Missing assets produce a skipped report and exit code 2, never successful
  full-content validation. The current installation has 17 unresolved Zero Hour
  movie references. Inventory exit 1 retains these failures; do not whitelist them.
- Query a generated report without rescanning: `python3 scripts/content_report.py`.
  Use `--feature`, `--map`, `--game`, `--status` or `--failures` to narrow it down.
- Registered script handlers are unverified; empty handlers are not support.
  Passing unit tests or inventory generation does not certify playable campaigns.
- The bounded scenario core shares production simulation ticks and has CPU-only
  synthetic scenarios. Run `quick --filter 'FullyQualifiedName~SimulationScenarioRunnerTests'`
  for them. Official-map headless initialization and an asset scenario CLI remain
  outstanding; synthetic passes never certify campaign playability.

## Reproducing regressions

For a bug, first retain the command, configuration and relevant log. Add a focused
fixture exercising the same production code, then run its filtered check. Use
synthetic data or existing repository fixtures for public tests. Only run asset
checks when asset parsing/resolution or official content is relevant to the change.
Run the broader suite once after a coherent change; repeat only for changed code,
new failures or unresolved concerns.

Use `docs/behavior-references/README.md` when recording original-game evidence.
Record map, game version, difficulty, seed where available, exact orders and expected
state. Do not substitute Ghidra findings or a removed TODO for gameplay evidence.
See `docs/development-workflow.md` for command details and troubleshooting.
