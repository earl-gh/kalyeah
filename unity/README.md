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
