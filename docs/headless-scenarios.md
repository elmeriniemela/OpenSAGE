# CPU simulation scenarios

`SimulationScenarioRunner` runs bounded scenarios through production simulation
code. The public regressions use synthetic data and create no platform window,
graphics device or audio device:

```sh
./scripts/verify.sh quick --configuration Release --filter 'FullyQualifiedName~SimulationScenarioRunnerTests'
```

The complete working example is
[RunTrace in SimulationScenarioRunnerTests](../src/OpenSage.Game.Tests/Diagnostics/SimulationScenarioRunnerTests.cs).
It creates an object, issues selection/control-group orders on specified ticks,
executes a counter script and checks state on every tick. Two independent hosts
with the same seed produce the same trace; another seed produces a different trace.

## Host contract

The caller supplies an `IGame` with initialized simulation services and a callback
that updates its `MapTime`. It owns and disposes that host. Each attempt needs a
new host: the runner rejects already advanced hosts, pre-created objects and reuse
after a failed setup. The host must have a scene, partition manager, local order
queue and scripting system, with no graphics device or panel.

Create objects and initialize scenario-specific state in `SimulationScenario.Setup`.
The runner seeds both simulation and client RNGs before calling it. Starting players
and other host construction must not depend on an uninitialized scenario RNG.
The synthetic test scene delegates scene logic to the same implementation as
`Scene3D`; it does not duplicate the scheduler, player, object or order implementations.
Presentation operations throw if a scenario accidentally reaches them.

`MaxTicks` must be positive. Ticks start at zero, with simulation time zero on the
first tick. For Generals and Zero Hour each logic tick advances 1/30 second. Integer
time calculations avoid accumulated clock drift. Each tick performs:

1. Apply the deterministic clock and queue that tick's orders in insertion order.
2. Run the production scheduler, order queue, scene/object and partition updates.
3. Run scripting ticks due at that simulation time, after the logic tick.
4. Evaluate assertions scheduled for that tick.

An assertion at tick 59 observes the state after 60 completed ticks. The runner
rejects schedules outside `[0, MaxTicks)` and scenarios without assertions. It
snapshots schedules before setup so callbacks cannot remove required checks.

## Reports and failure behavior

The returned `SimulationScenarioReport` includes game/map/scenario, seed, tick
bound, completed ticks, queued-order count, assertion count, status and failures.
`CompletedTicks` counts ticks whose simulation and scripting finished; a failed
assertion can therefore refer to the last completed tick. `OrdersQueued` does not
claim that every queued order completed successfully. Failures include stage,
tick, map/script/object/feature origin, message and exception stack.

Call `report.WriteTo(path)` to write version 1 JSON atomically. Assertions can supply
a `ScenarioOrigin` for precise provenance. Script and scheduler exceptions supply
their runtime feature and script/object context; normal nested script execution
includes its group path. A setup failure uses tick -1. Cancellation produces a
`Cancelled` report and cannot pass. Normal runtime exceptions produce `Failed`;
invalid scenario/host arguments throw before the simulation starts.

Strict validation is scoped to the run. Missing script handlers, empty action
implementations and unimplemented default order dispatch fail rather than log and
continue. Disabled script content is ignored. A registered partial implementation
can still need coverage, and explicit empty order cases are not yet classified.
Reports always keep `CompletionGatePassed: false`.

Tick bounds and cooperative cancellation cannot interrupt a hung native call or
an infinite loop inside one simulation update. Use the verification script's
process timeout for a hard outer bound. Its logs/TRX retain assertion failures,
including the serialized report from scenario regression assertions.

## Remaining integration

This is the scenario core and a synthetic public test host. There is no standalone
asset scenario CLI or production headless map loader yet. Official-map terrain,
navigation, stock drawable separation, null rendering/audio adapters, difficulty,
mission startup, full dependency checking and runtime origin coverage still need
integration. Asset inventory remains static analysis. Scenario passes are evidence
for their particular checks, not campaign completion, full subsystem support or
original-game behavioral equivalence.
