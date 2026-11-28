# Asset log

Every model, texture, sound and font that is not made from scratch goes in this table, so the
game could still be sold later without licence surprises. AI-generated assets count too.

| Asset | Path in project | Source (URL or tool) | Author | Licence | Date added | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Frog, egg and tadpole shapes (body, belly, eyes, leg bones, webbed feet, tadpole tail) | `Assets/Scripts/Simulation/Looks/FrogShapes.cs` | Made from code in this project | Frog Terrarium | Same as the project (no outside licence) | 2026-11-27 | Not downloaded: built from maths when the game starts |
| Every sound (frog calls for each species, splash, plop, crickets, tongue, slide, stairs, tunnel, lift, teleporter) | `Assets/Scripts/Simulation/Looks/SoundSynth.cs` | Made from code in this project | Frog Terrarium | Same as the project (no outside licence) | 2026-11-27 | Synthesised at start-up; no recordings |
| Shaders for the ground, water, glass, mist and room backdrop | `Assets/Shaders/` | Written by hand for this project (Unity URP shader code) | Frog Terrarium | Same as the project (no outside licence) | 2026-11-28 | No textures: grain, ripples and gradients are maths |
| Room reflection (the soft room and lamp that shiny things reflect) | `Assets/Scripts/Game/TankVisuals.cs` | Made from code in this project | Frog Terrarium | Same as the project (no outside licence) | 2026-11-28 | Built when the game starts |
| Universal Render Pipeline (URP) and its post-processing | Unity package `com.unity.render-pipelines.universal` | Unity Package Manager | Unity Technologies | Unity Companion License (free with Unity) | 2026-11-28 | Part of Unity, not an outside asset; listed so the licence is on record |
