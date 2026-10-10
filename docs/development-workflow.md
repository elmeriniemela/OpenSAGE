# Local development and verification

Use .NET 8 from `global.json` and Python 3.9+; there are no Python packages to
install. The verification runner wraps existing build, test and inventory tools.
Quick/full modes include the public CPU simulation scenarios. The standalone
asset-backed gameplay runner from `PLAN.md` still needs headless map initialization.

## Commands

```sh
# Build the game test project and run its noninteractive tests.
./scripts/verify.sh quick

# Iterate on one subsystem, including parameterized tests.
./scripts/verify.sh quick --filter 'FullyQualifiedName~PolygonTriggerTests'

# Check Release behavior, build all solution projects and run both test suites.
./scripts/verify.sh full --configuration Release

# Explicitly inspect both installations; requires configured external assets.
./scripts/verify.sh assets
```

On Windows run `python scripts/verify.py` with the same arguments. Paths and test
filters are passed as separate process arguments, so spaces and shell metacharacters
in asset paths do not become shell commands. Bash entry points use LF line endings.

Each test mode first runs the Python tooling regressions. Each mode restores only
when needed, builds exactly once and tests/runs the resulting binaries without an
implicit rebuild. The restore cache checks SDK, build/project/package configuration,
NuGet user configuration and every referenced project's `project.assets.json`.
Source changes still go through MSBuild's incremental compilation; test results
and asset scans are never cached. `--restore` forces dependency restoration.

The tooling's standard-library regression suite also has a separate CI workflow
for Windows, Linux and macOS. Existing .NET build/test CI is unchanged.

Local restore omits NuGet vulnerability audit to avoid a network audit during the
edit loop. `--audit` enables it and invalidates the restore cache. Package downloads
still require normal configured NuGet access. CI configuration is unchanged.

Defaults: Debug configuration, one MSBuild worker and a 600-second timeout per
command. `--configuration Release`, `--jobs 4`, and `--timeout 1200` override them.
Timeouts fail verification; on POSIX the runner terminates the command's process
group. A lock prevents simultaneous runner invocations from corrupting shared
outputs. If a process is forcibly killed, inspect `.artifacts/verification/.lock/owner.json`
and remove the lock directory only after confirming its recorded process stopped.
Standalone builds do not participate in the lock.

## Reports and failure reproduction

`.artifacts/verification/latest.json` is the latest run's structured summary.
Each run has its own directory under `.artifacts/verification/runs/`, containing:

- `report.json`: status, configuration, reproduction command, SDK, step durations,
  exit codes, test counts and failure messages/stacks or inventory failures.
- `build.log`, `restore.log` when restore ran, `tests.log` and test `.trx` reports.
- `content-manifest.json` and `inventory.log` for asset runs.

Reports survive a failed command and show existing skipped tests separately.
Zero executed tests and missing/malformed test reports fail verification. A failed
build stops before tests, so stale binaries cannot make a failed build look verified.
No mode claims that a single-player completion gate has passed.

To reproduce a test failure, use the reported configuration and filter its class or
method with `--filter 'FullyQualifiedName~Class.Method'`. Reports also retain exact
subprocess argument lists. A timeout or sandbox socket error is infrastructure
failure, not a passing or skipped test run. VSTest's loopback socket needs the normal
execution permission provided by the calling environment; the runner does not
change sandbox permissions.

## External assets and fast queries

Copy `scripts/verification.local.example.json` to `scripts/verification.local.json`
and replace its paths with your installation directories. The local config and all
reports are gitignored. This checkout is configured for the installations under
`/home/elmeri/Projects/ZeroHour`; other checkouts supply their own paths.

Explicit flags override environment variables, which override the local file:

```sh
./scripts/verify.sh assets --generals '/path/to/Generals' --zero-hour '/path/to/Zero Hour'
# Environment alternatives: CNC_GENERALS_PATH and CNC_GENERALS_ZH_PATH.
```

Exit codes: 0 passed the requested checks; 1 failure; 2 missing assets/configuration
or invalid invocation; 124 is recorded for a timed-out step (the run fails with 1).
Missing installation directories are reported as skipped asset validation. A report
with inventory failures still produces a usable manifest. The current local asset
inventory reports 17 unresolved Zero Hour movie references; these remain failures.

Inspect the latest generated manifest without rebuilding or rereading assets:

```sh
# Most frequent missing/empty features with originating map and script examples.
python3 scripts/content_report.py --limit 10
# Find exact uses of one feature, optionally narrowed by game or map.
python3 scripts/content_report.py --feature TeamFollowWaypoints --game ZeroHour
python3 scripts/content_report.py --map USA01 --status RegisteredUnverified
# Missing files/definitions/media and map-parser failures.
python3 scripts/content_report.py --failures
# Inspect an older or independently generated manifest.
python3 scripts/content_report.py --manifest /tmp/opensage-content-manifest.json
```

Queries print bounded JSON with match counts and truncation status. Disabled script
usages remain visible with their enabled flag. A registered handler remains
unverified. The default manifest comes from the most recent asset run that produced
a valid manifest; its path is always included, even if a later run failed before
producing one.

## Reference evidence

Use [the behavior-reference template](behavior-references/README.md) for original-game
comparisons. [CPU scenarios](headless-scenarios.md) provide fixed seeds, real player
orders, bounded ticks, assertions and structured failures. Their current synthetic
host does not initialize official maps or provide all-difficulty campaign acceptance.
