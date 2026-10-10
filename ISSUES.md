# Issues found in manual user testing.

Start with the Release build and Vulkan launch for both games, using your installed game paths:

dotnet build src/OpenSage.Launcher -c Release
dotnet run --project src/OpenSage.Launcher -c Release --no-build -- --renderer Vulkan --gamepath "/home/elmeri/Projects/ZeroHour/Command and Conquer Generals"

Then repeat with --gamepath "/home/elmeri/Projects/ZeroHour/Command and Conquer Generals Zero Hour".

For each launch, check:

1. The game reaches its main menu without crashing or showing errors.
2. You can open a map. Try a few maps, especially if any previously failed to load.
3. In a skirmish, test selecting units, moving them, issuing an attack, and building or using a basic unit if available.
4. Check whether the game responds correctly to pause, restart, and returning to the menu.
5. Watch /tmp/sage.log for errors or warnings around any failure.

If you have time, try a campaign mission and see whether objectives advance or the mission completes. Campaign and full gameplay behavior still need substantial work, so a failure there is useful evidence too.

For anything that breaks, note the game, map or mission, steps to reproduce, what you expected, and what happened. The current verification and remaining work are tracked in PLAN.md (PLAN.md) and docs/arch.md (docs/arch.md).

## Using a tank to attack an empty building crashes the game:

[20:43:27.9507][Info] Weapon CrusaderTankGun on game object  transitioning to state PreAttack
[20:43:27.9507][Info] Weapon CrusaderTankGun on game object  transitioning to state Firing
[20:43:27.9653][Info] Set active condition state for GenericTankShell
Unhandled exception. OpenSage.Diagnostics.SimulationFeatureException: Simulation feature 'Object.LogicTick' failed: Object reference not set to an instance of an object.
 ---> System.NullReferenceException: Object reference not set to an instance of an object.
   at OpenSage.Logic.Object.ProjectileNugget.Execute(WeaponEffectExecutionContext context) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/Weapon/WeaponEffects/ProjectileNugget.cs:line 71
   at OpenSage.Logic.Object.FiringWeaponState.OnEnterStateImpl() in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/Weapon/WeaponStates/FiringWeaponState.cs:line 37
   at OpenSage.Logic.Object.BaseWeaponState.OnEnterState() in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/Weapon/WeaponStates/BaseWeaponState.cs:line 33
   at OpenSage.Logic.Object.WeaponStateMachine.TransitionToState(WeaponState state) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/Weapon/WeaponStates/WeaponStateMachine.cs:line 104
   at OpenSage.Logic.Object.WeaponStateMachine.Update() in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/Weapon/WeaponStates/WeaponStateMachine.cs:line 116
   at OpenSage.Logic.Object.Weapon.LogicTick() in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/Weapon/Weapon.cs:line 121
   at OpenSage.Logic.Object.GameObject.LogicTick(TimeInterval& time) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/Object/GameObject.cs:line 728
   at OpenSage.Logic.SimulationTick.UpdateScene(IGame game, TimeInterval& time) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/SimulationTick.cs:line 35
   --- End of inner exception stack trace ---
   at OpenSage.Logic.SimulationTick.UpdateScene(IGame game, TimeInterval& time) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/SimulationTick.cs:line 39
   at OpenSage.Logic.SimulationTick.Execute(IGame game, TimeInterval& time) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Logic/SimulationTick.cs:line 11
   at OpenSage.Game.Update(IEnumerable`1 messages) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Game/Game.cs:line 809
   at OpenSage.Launcher.Program.Run(Options opts) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Launcher/Program.cs:line 268
   at OpenSage.Launcher.Program.<>c.<Main>b__1_1(Options opts) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Launcher/Program.cs:line 74
   at CommandLine.ParserResultExtensions.WithParsed[T](ParserResult`1 result, Action`1 action)
   at OpenSage.Launcher.Program.Main(String[] args) in /home/elmeri/Projects/OpenSAGE/src/OpenSage.Launcher/Program.cs:line 73

