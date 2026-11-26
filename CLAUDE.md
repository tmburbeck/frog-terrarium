# Notes for AI assistants

* Unity version is locked to the one in `ProjectSettings/ProjectVersion.txt` (6000.3.29f1). Do not
  change it without the owner's say-so; CI picks its editor image from that file.
* Code in `Assets/Scripts/Simulation` must not reference `UnityEngine` (its asmdef sets
  `noEngineReferences`). It is also compiled by `tools/SimCoreTests` with plain .NET (C# 9), so keep
  to C# 9 and APIs available in both Unity's .NET Standard 2.1 profile and .NET 8.
* All simulation randomness goes through `World.Random.Get(name)`.
* Any change to saved state needs a new save version; follow `docs/SAVE_FORMAT.md`.
* The owner is new to game development: explain changes simply in PR descriptions.
* New art or audio must be added to `docs/ASSET_LOG.md` with its licence.
