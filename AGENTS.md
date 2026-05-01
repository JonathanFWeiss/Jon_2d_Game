# AGENTS.md

These notes apply to the whole `Jon_2d_Game` Unity project.

## Project Overview

- This is a Unity 2D platformer project using Unity `6000.4.1f1`.
- The Git/project root is `Jon_2d_Game`, not the parent `2d Stickman` folder.
- Main runtime code lives in `Assets/Scripts`.
- Gameplay scenes include `Assets/CastleStart.unity`, `Assets/MainMenu.unity`, and scenes under `Assets/Scenes`.
- Reusable gameplay prefabs live mostly in `Assets/Prefabs` and `Assets/Prefabs/Enemies`.
- `Assets/Not-Used, For Reference Only MetroidvaniaController` is reference material. Do not edit it unless explicitly asked.

## Working Rules

- Keep changes tightly scoped to the requested behavior.
- Do not revert unrelated dirty files. This project often has existing local changes.
- Do not edit generated Unity folders such as `Library`, `Temp`, `Logs`, or `UserSettings` unless explicitly asked.
- Do not hand-edit generated `.csproj` or `.slnx` files unless the user specifically asks.
- Preserve Unity `.meta` files when adding, moving, or deleting assets.
- Prefer existing MonoBehaviour patterns in `Assets/Scripts` over introducing new architecture.
- Use `SerializeField` private fields for inspector-configured data unless an existing script in the area uses a different style.
- Avoid `UnityEditor` APIs in runtime scripts. If editor-only code is necessary, wrap it in `#if UNITY_EDITOR`, and make sure the runtime/player build path still validates critical assumptions.

## Scene And Prefab Safety

- Be careful with Unity YAML scene and prefab edits. Small serialized reference fixes are acceptable, but prefer opening Unity for complex hierarchy, transform, animation, or prefab-variant work.
- A serialized object reference with only `{fileID: ...}` usually points to a live scene object. A prefab asset reference should include a `guid`.
- Gauntlet enemy wave entries must reference prefab assets, not live scene enemies.
- `EnemyGauntletRoom.EnemySpawn.enemyPrefab` is a spawn template. If it references a scene enemy, killing that scene enemy can null the reference and reduce the gauntlet spawn count.
- `EnemyGauntletRoom` intentionally validates this at runtime and should throw if a live scene object is assigned to an enemy prefab slot. Do not replace that with an editor-only auto-fix.

## Verification

- Best compile check is a Unity batchmode project open or test run. The installed editor path on this machine has been:
  `C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe`
- Example compile/open check:
  `& "C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\jonat\2d Stickman\Jon_2d_Game" -logFile "C:\Users\jonat\2d Stickman\Jon_2d_Game\UnityCompile.log"`
- Unity cannot open the same project twice. If batchmode fails because another Unity instance has the project open, report that clearly instead of forcing it.
- `dotnet build` may not be reliable here because Unity-generated project files depend on the local Unity setup and the machine may not have a full .NET SDK available.
- When a full Unity compile is unavailable, at minimum inspect relevant diffs, search for bad serialized references, and verify runtime/editor preprocessor boundaries.

## Common Commands

- Find files: `rg --files`
- Search code: `rg -n "pattern" Assets/Scripts`
- Check working tree: `git status --short`
- Inspect changes: `git diff -- Assets/Scripts/SomeFile.cs`

## Current Gameplay Notes

- Enemy death is handled by `EnemyBase.Die()` destroying the enemy GameObject.
- Unity's destroyed-object null behavior matters for lists and serialized references; do not use destroyed scene objects as reusable spawn templates.
- `CastleStart` contains a `Gauntlet Room` prefab instance configured through scene overrides.
- If changing enemy spawning, check both the prefab at `Assets/Prefabs/Gauntlet Room.prefab` and scene overrides in `Assets/CastleStart.unity`.
