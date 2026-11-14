# Kalyeah on Unity (migration in progress)

The Unity port of the Godot game. The Godot project at the repository root stays the shipping game until the port reaches parity.

- `Kalyeah.Sim/`: the game's rules (the sim), plain C# with no engine dependency. The same files compile in Unity (`Kalyeah.Sim.asmdef`),
  in the dedicated server and in the tests.
- `Kalyeah.Sim.Tests/`: NUnit tests, run with `dotnet test unity/Kalyeah.sln`. They check the port against the Godot game itself:
  `Golden/` holds recordings made by the Godot game (`tools/unity_export/export.gd`): its random numbers and scripted 5v5 matches stepped
  tick by tick.
- `Data/`: every resource under `data/` exported to JSON by the same tool (rules, weapons, characters, bot levels).
- `Game/`: the Unity project (Unity 6 LTS, URP, Android).

Regenerate the data and the recordings after a rule changes in the Godot game:
`godot --headless --path . -s tools/unity_export/export.gd`

## The sim port (Kalyeah.Sim)

- Names: GDScript snake_case becomes PascalCase (`rules.move_speed` is `Rules.MoveSpeed`, `sim.add_player()` is `sim.AddPlayer()`),
  signals become C# events of the same name (`player_tagged` is `PlayerTagged`). Exceptions where C# needs a different name:
  `MatchSim.Phase` is a `MatchSim.MatchPhase`, `capture_kind` is `CurrentCaptureKind`, `clock_stage()` is `GetClockStage()`,
  `GameRules.bot_level()` is `GetBotLevel()`, `WeaponDef.Kind/Shape` enums are `WeaponKind/WeaponShape`, `sim._brains` is `Brains`.
- Maths: `Vec2`/`Rect2` are Godot's float32 Vector2/Rect2 with Godot's formulas; `.x`/`.y` read a component as a double, like GDScript.
  Scalars are doubles. `GodotRng` is Godot's RandomNumberGenerator. The golden replays match Godot bit for bit.
- Seams for the ports still to come: `IStreetMode` (what MatchSim calls on a street game; `MatchSim.StartMode`), `DummyBrain`
  (virtual `Think`, `IsFighter`; FightBrain/TeamBrain extend it), `Geometry2D.SegmentHitsRect` (FightBrain's static helper).
- No extra DLL: data loads with the built-in `MiniJson` via `SimData.Load(dir)` or `SimData.AddFile(json)` (e.g. from TextAssets).
  SimData fills fields by reflection, so keep this assembly from being stripped in IL2CPP builds (a `link.xml` preserving `Kalyeah.Sim`).
