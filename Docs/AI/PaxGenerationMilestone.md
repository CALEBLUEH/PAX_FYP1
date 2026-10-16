# PAX editable island generation

16 October 2026 trip milestone. Scope: one bounded, playable generated island with existing movement, mine/place, cutaway and tile-edit persistence. No external assets, package changes, combat or cross-island travel.

## Authoring and playing

- Open `Assets/PAX/Scenes/PaxGeneratedIsland.unity`. Seed 16427 produces 2,060 saved tile prefab instances with terraced ground, a house, a roofed cave and a broken ruin. Spawn and structure entrance markers remain visible and editable in the Hierarchy.
- Select `Assets/PAX/Generation/TripIsland.asset` to edit seed, radius (12-16), terrace height (0-2), patch size (2-5) and structure toggles. Click **Create new editable island scene...**, choosing a NEW filename under `Assets/PAX/Scenes`. Alternatively create a definition through **Assets > Create > PAX > Island generation definition**.
- Generation refuses existing filenames. **PAX > Create generated island if missing** is idempotent and preserves all later manual scene changes. Changing settings does not rewrite any scene on load, Play, Awake or OnValidate. The scene's `PaxGeneratedIsland` stores the original seed/settings/count and definition/spawn references as provenance.
- All gameplay components, materials, tile prefab references, shadow children, authored UI and controls are inherited from the validated prototype rig. No additional Inspector assignments are required. Each tile remains a scene instance; generation does not run during ordinary gameplay.
- WASD moves, Space jumps, LMB mines, RMB places, Ctrl toggles precise/smart targeting, G includes faded solids, Q/E or middle mouse orbits, wheel zooms, Home resets view and R returns to spawn. F5/F9/F8 save/load/load previous tile edits.
- The default island uses `PAXPlaytests/pax-island-87cafc0ae5d64364c90e9936857e724d.xml` under Unity's persistent data directory. Each newly generated scene receives its own GUID-based filename, leaving `pax-playtest.xml` intact. No automatic loading, saving or save-file deletion is added. Existing schema version 1 and authored-world SHA256 compatibility checks remain unchanged.

## Generation contract

The layout uses a fixed unsigned integer hash, not Unity's global random state or engine noise. Identical settings produce an identical sorted list of cells, materials and mineability. Protected foundation at Y=-3 supports mineable soil/grass terraces. Flat three-wide approaches connect the clear central spawn to every structure's two-high doorway and interior. Full house/cave roofs exercise contextual cutaway; the ruin provides partial shelter. Terrain tops differ by at most two tiles and remain within the existing jump range.

The Editor tool clones the saved prototype into a new scene before modifying it, replaces only its tile content and old text labels, updates the explicit world bounds, repositions the existing spawn, and retains the assigned gameplay references. Build Settings adds the new scene without removing the original or Unity AI config objects. It never writes the original `PaxPrototype.unity`.

Batch authoring/build entry points require `-paxAuthoringProject <absolute intended project root>` and compare that independent argument with actual `Application.dataPath` before asset/scene changes. A mismatch throws; the confirmed path is logged. Normal Inspector/menu use checks the Editor's actual project against its working directory. No selected-editor Unity MCP mutation is used.

## Validation

**Complete, locally validated; waiting for the coordinator's AS handoff.** Isolated `CodexWorkspace`, baseline `4afd11e`. Unity 6000.3.18f1, Windows x86_64 Development build. Evidence is in `Docs/AI/DevelopmentEvidence/Generation/`.

- **34/34 EditMode tests:** all prior tests plus exact same-seed layout, changed-seed output, global RNG independence, 18 radius/height/seed combinations (including extreme signed seeds), unique/in-bounds cells, protected foundation, two-high clear spawn and connected entrances/interiors, settings/toggles, invalid settings, serialized scene/material/shadow references, unique nonempty save-slot GUID, Build Settings, overwrite/idempotence preservation and wrong-project refusal.
- **45/45 full PlayMode tests:** all prior movement/building/save/cutaway regressions and three generated-world scenarios. The three generated scenarios then passed **3/3 again** after adding explicit downward-mining, protected-foundation and grass/collider restoration assertions. Results: `EditMode.xml`, `PlayMode.xml`, `GeneratedPlayModeFinal.xml`.
- **Windows build succeeded:** 0 errors, 485 pre-existing installed Unity inference shader warnings, 187,754,878 bytes, 12.43s final incremental build. No PAX shader warning. `BuildSummary.txt`; full local log `Logs/PAX/GenerationBuildFinal.log` logs the actual guarded isolated Assets path. Earlier build also succeeded before a diagnostic-only nearest-floor probe correction.
- **20/20 built-player checks:** real saved scene/startup/HUD/grounding and movement, validated placement and downward mining, separate disposable save directory, tile-edit reload, incompatible-world rejection, every two-high doorway and opposite-wall collision, and house/cave/ruin roof/cutaway/solid invariants. No warning/error/exception in `Logs/PAX/GenerationPlayer.log`; `PlayerSmoke.txt` contains exact checks.
- **Visual inspection:** actual camera and authored UI rendered together at 1600x900. Overview and all three structure captures inspected: player visible through local cutaway, distinct terrain/structures and live legible HUD. `Generation-Overview-HUD.png`, `Generation-House-HUD.png`, `Generation-Cave-HUD.png`, `Generation-Ruin-HUD.png` (spawn also captured). Hidden-window evidence uses the existing opt-in offscreen renderer, not a claim of human input testing.
- **Preservation:** isolated prototype scene has no Git diff. Original `PrototypeBranch` remains at `42ad020`; its observed scene SHA256 `FC644B89751FD684D3889038624171A0E6B501C37FF76605038CE44C20F7EF07` and Build Settings SHA256 `8FF8C4D688FAE85511E326765D71797330A776D389AD0627E55926DC4E636F7C` match the start-of-milestone audit. Its existing modified Build Settings and coordinator-owned untracked trip ledger were left alone. No Absolute Samsara/shared-asset mutation or public upload. Unity-generated scripting-define order and the isolated temporary SceneTemplateSettings file were cleaned up after validation.
- Asset audit: no missing PAX `.meta` files, no duplicate PAX GUID groups; source/document whitespace check passed. No scoped PAX Editor/player remained running at handoff; unrelated open AS Editor was preserved. Installed Unity AI Editor services emitted entitlement/network diagnostics during startup, without blocking compilation, tests or builds.

Player: `C:\Users\user\.codex\worktrees\pax-development\PAX_FYP1\Builds\PAX-Generated-Windows\PAX.exe` (keep adjacent data/runtime files). `PaxIslandAuthoring.BuildWindows` builds only the generated scene into this separate directory. Normal launch uses the authored GUID save slot; `-paxSmoke` is the explicit diagnostic path.

Batch reproduction: launch the project Unity version with `-batchmode -projectPath <isolated-root> -paxAuthoringProject <same-isolated-root> -executeMethod Pax.Editor.PaxIslandAuthoring.BuildWindows -quit -logFile <log-path>`. For EditMode/PlayMode use `-runTests -testPlatform EditMode|PlayMode -assemblyNames Pax.EditModeTests|Pax.PlayModeTests -testResults <xml-path>` without `-quit`. Run only one validation process at a time.

Manual follow-up: open the generated scene (or its Windows player), walk the flat routes through all entrances, jump onto terrace tops, orbit/zoom inside the house/cave, mine nearby floor/roof tiles and place replacements. F5 saves state A; make another edit and F5 saves state B with A as backup. Make an unsaved edit, then F9 should restore B and F8 should restore A. Try a different seed via a NEW output filename and confirm the old edited scene remains untouched. Human raw keyboard/mouse coverage and longer performance/stress play remain unverified.

## Limits and next useful milestone

This is a bounded single-island generator, not streaming terrain or a biome/progression system. Settings deliberately cap island size; no large-world FPS or memory budget is claimed. House/cave/ruin are primitive structural archetypes with seed-dependent placement and terrain, not a general grammar or content library. Human keyboard/mouse exploration, aesthetic judgement and longer performance playtests remain useful.

The next useful PAX milestone is a focused exploration playtest and visibility stress pass in the generated structures before adding gameplay systems. Stop after this milestone and wait for the coordinator's AS handoff.
