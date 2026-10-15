# PAX prototype milestone

Status: **Ready with limitations for this prototype checkpoint.** Unity compilation, 13 EditMode tests, 19 PlayMode tests, Windows development build and 15 built-player smoke checks passed. Combined world/HUD captures were inspected. A human raw keyboard/mouse playthrough and performance/stress coverage remain unverified.

## Git checkpoint and delivery blocker

Implementation checkpoint: `a207eeb3de2247de148cfb75798f5fd532188420` on `PrototypeBranch`. It includes the coordinator-approved acceptance/status documents and the required generated UI theme. The working tree was clean after this commit; builds, local logs and diagnostic files remain ignored.

Normal push was rejected twice by automatic approval review. Read-only verification confirmed that the unchanged origin is `https://github.com/CALEBLUEH/PAX_FYP1.git`, the connected account owns it with push permission, and the destination is **public**. Its existing `PrototypeBranch` remains at `e82f1c91410ea0c20c83df9561c06a176ce3744b`. No push executed.

The second rejection specifically requires trusted explicit user approval for publicly uploading this payload (PAX source, authored scene/assets, screenshots and validation/report documents) to that destination, despite the earlier general normal-push authorization. No alternate upload mechanism will be used. Remote delivery is blocked pending that approval; the implementation, local Git checkpoint, Windows build and validation evidence are complete. A follow-up documentation commit records this blocker.

## Scope and baseline

16 October 2026 overnight trial, PAX only, existing `PrototypeBranch`, baseline `e82f1c9`. The pre-existing Unity AI App UI Build Settings config must remain. No shared asset originals or other game files are edited.

## Intended playable slice

A saved, editable test island with a 1×2 player, camera-relative movement, variable jump up to two tiles, five-tile mine/place, smart/precise targeting, previews, orbit/zoom, and height/roof/confinement/obstruction cutaway. Open field, house, cave, low enclosed room and jump steps provide bounded test cases.

The saved scene has 1,695 tile instances, plus a high hall connected to the low room by a narrow passage and a real underground shaft. All scene references are assigned; the one-by-two-tile visual uses a 1.96-high collision capsule for doorway contact clearance.

## Conservative assumptions

- One Unity unit per tile; nominal player visual height 2, capsule height 1.96, diameter 0.9. The 0.04-unit vertical collision clearance allows two-high grid doorways without crouching. No automatic one-tile climbing.
- LMB mines; RMB places. Left Ctrl toggles smart/precise targeting. Infinite prototype building supply, no inventory/progression yet.
- Placement requires any face-adjacent support, empty space, player clearance, line of sight and five-tile 3D reach.
- Island foundation at Y=-2 is protected for repeatability; upper terrain and structures can be mined.
- Grounded feet height controls layer evaluation and stays fixed while airborne. Nearby geometry and weighted low roofs preserve context, but actual screen-space camera obstruction always wins.
- Confinement uses a capped two-high horizontal flood plus four directional probes. It estimates local enclosure; it is not a complete connected-room segmentation system.
- Smooth transparent cube edges are individual ghost outlines, bounded by horizontal and vertical relevance. Merging adjacent tile outlines is deferred.
- Collision and solid sun shadows remain independent of cutaway. Each tile receives three physical occupancy probes toward the assigned sun, refreshed on geometry/sun-direction changes; camera alpha never enters that calculation. This provides coarse tile sunlight for transparent receivers. Full light propagation, torches, day/night and gameplay darkness are future work.
- No external assets, purchases or paid APIs.

## Current evidence

- Unity scene authoring retry passed: `Logs/PAX/Authoring-Retry.log`, `PAX_AUTHORING_OK`, 1,691 saved tile instances, clean batch shutdown.
- Final EditMode: **13/13 passed**, including all scene references, independent shadow references, unique cells, enabled build scene and byte-for-byte authoring idempotence. Tracked result: `Docs/AI/PaxEvidence/EditMode.xml`; full local log: `Logs/PAX/EditMode-Release.log`.
- Final PlayMode: **19/19 passed**. Tracked result: `Docs/AI/PaxEvidence/PlayMode.xml`; full local log: `Logs/PAX/PlayMode-Release.log`.
- Captures inspected: house, rotated house, cave, low room, high hall, connecting passage and underground shaft. Saved in `Docs/AI/PaxEvidence/`. The player and local solids remain readable with smooth fade; distant geometry stays solid. Coarse physical tile shadows remain visible beneath faded geometry.
- Noisy dithering was replaced with smooth alpha blending. Transparent receivers could not retain the earlier opaque screen-space shadows, so independent opaque shadow children preserve shadow casting onto the player and occupancy-based tile sunlight preserves the world surface illumination. The sunlight-invariance PlayMode test passes with all tile surfaces made invisible and camera rotated.
- The first authoring attempt failed with a prefab-reference exception (`Logs/PAX/Authoring.log`). Creation order/instantiation were corrected and the successful retry supersedes that failure. No scene was saved by the failed run.
- Earlier added fixtures failed: a rotated west-side ray selected an out-of-range house roof, and an outdoor lighting sample was outside the trimmed island. Ray diagnostics prove the roof hits `(-6,3,3)` and `(-5,3,3)` precede the reachable tile `(-2,0,-1)`. The final test retains this rejection and asserts a positive clear east-side target. The lighting sample now asserts an existing outdoor cell before checking illumination. Both corrected cases passed in isolation, followed by the full 16/16 run.
- Final Windows x86_64 development build: **Succeeded**, 0 errors, 485 installed inference-shader warnings, 187,258,883 bytes, 11.52s incremental build. Executable and required data folder: `Builds/PAX-Prototype-Windows/PAX.exe`; full log: `Logs/PAX/BuildWindows-PlayerRender.log`; tracked summary: `PaxEvidence/BuildSummary.txt`. PAX Editor/test assemblies are excluded from the player.
- Built player: **15/15 smoke checks passed**, including startup/grounding, movement, held jump/landing, place/mine, overlap/range rejection, all contextual locations and the live authored HUD. No error/exception/warning was reported in `Logs/PAX/Player-Verified.log`. Tracked checks: `PaxEvidence/PlayerSmoke.txt`.
- Standalone captures inspected: `Player-Open-field-HUD.png`, `Player-Cave-HUD.png` and `Player-Shaft-HUD.png` (house also saved). Hidden Windows swap-chain captures initially produced black images; the opt-in diagnostic now explicitly renders the real camera and authored UI panel to a shared offscreen target. The final evidence contains both world geometry and live HUD text. Normal launch keeps the ordinary camera/UI configuration; the diagnostic is activated only by `-paxSmoke`.
- First Windows x86_64 development build passed: 0 errors, 485 warnings, 187,251,987 bytes, 3m24.83s. All 485 shader warnings are in the installed Unity AI inference package: 460 `ConvGeneric` compute variants and 25 `Hidden/Sentis` pixel shaders. No PAX shader warning was reported. Initial summary is tracked in `PaxEvidence/InitialBuildSummary.txt`; full log: `Logs/PAX/BuildWindows.log`.
- Additional 19-case run passed 18/19: real jumps/landings on the one- and two-tile steps passed, including stable reference changes, and the moving-actor sunlight regression passed. The cave doorway test stopped at Z=2.455. A bounded probe confirmed that disabling step offset did not resolve it, while capsule height 1.96/centre 0.98 passed to Z=5.50. The two-tile visual remains unchanged. Probe log: `Logs/PAX/DoorProbe.log`. The scene now has four additional lintel tiles (1,695 total) and the corrected authored capsule.
- The full 19/19 rerun retains the original doorway assertion and confirms the correction plus movement/ceiling/one-high-clearance regressions. Captures `Step-two-landing.png` and `Cave-entry-walk.png` were inspected. Temporary configuration probes were removed; the saved scene supplies the tested dimensions.
- World sunlight explicitly filters hits to registered `PaxBlock` solids. A passing regression puts the player on a raycast-visible layer directly in a sunlight ray, then moves it without changing world geometry; no actor shadow is cached into tile sunlight.

## Validated behavior and limits

| Criterion | Evidence / status |
| --- | --- |
| Saved scene, required references, build entry, idempotence | Passed, 13 EditMode checks |
| Camera-relative movement, normalized diagonal after rotation | Passed, PlayMode movement scenario |
| Held/tapped jump, stable airborne reference, solid floor/wall/ceiling, no one-high crouch | Passed, PlayMode movement/clearance scenarios |
| Real jumps onto authored steps, updated floor reference on landing, walk through two-high cave door | Passed, PlayMode traversal scenarios and inspected captures |
| Place/mine support, occupancy, overlap, reach including vertical, physical LOS | Passed, PlayMode interaction scenarios |
| Precise highlight and preview predict actual click mutation | Passed, PlayMode exact preview/click scenario |
| Faded-solid targeting plus signed layer cue | Passed, PlayMode G-targeting scenario |
| Roof/enclosure context, local underground fade, bounded ghosts/flood | Passed, PlayMode scenarios and inspected captures |
| Obstruction beats preservation; camera rotation restores previous blocker | Passed, PlayMode visibility scenarios |
| Roof mining/placement invalidates context | Passed, PlayMode geometry-edit scenario |
| Fading leaves physical solidity and sunlight intact | Passed, PlayMode invariance scenarios |
| Windows player compilation, runtime smoke and actual world/HUD render | Passed, development build, 15 checks and inspected standalone captures |
| Raw keyboard/mouse playthrough by a person | Not run; automated motor, pointer and camera command paths were exercised |
| Stress/performance and different display frame rates | Not run; no performance budget specified |
| Full connected-room partitioning, merged outlines, full light propagation | Deferred beyond this bounded prototype |

Non-feature Editor log noise includes Unity AI bridge process/signature validation warnings and licensing entitlement messages; no C# or shader errors appeared in the final passing run. These package/environment messages are not reported as gameplay test failures.

## Controls and authoring

Open `Assets/PAX/Scenes/PaxPrototype.unity`. WASD moves relative to camera; hold/tap Space controls jump; R returns to the spawn marker. LMB mines; RMB places; Left Ctrl toggles smart/precise; G includes faded physical solids in targeting. Q/E rotate; MMB drag orbits/tilts; wheel zooms; Home restores the camera. The HUD displays exact cells and signed target height.

All references are assigned in the saved scene. Movement, reach, roof/confinement sampling, local fade radius, ghost bounds and smoothing are Inspector fields. Modify the tile prefab's independent shadow renderer with care: it deliberately stays opaque and `ShadowsOnly` while its parent surface fades. No Inspector setup is expected for first play.

`PAX/Create prototype if missing` preserves the scene if it already exists. `PaxPrototypeAuthoring.EnsureSolidShadows` is an idempotent additive prefab upgrade used during this implementation.

Only `EditorBuildSettings.asset` has an intended project-settings change: enabling the saved prototype scene. Its existing Unity AI App UI configuration is preserved. Unity-generated scene-template settings were removed, and define ordering was restored to the initial state. The HUD panel's generated Unity default runtime theme under `Assets/UI Toolkit/UnityThemes` is a required included dependency. Coordinator-owned checklist/status documents are included after coordinator approval.

## Reproduce validation

Use Unity `C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe` with `-batchmode -projectPath "C:\Users\user\Documents\GitHub\PAX_FYP1"`. Never run a second Editor against an open/locked project.

- EditMode: `-nographics -runTests -testPlatform EditMode -assemblyNames Pax.EditModeTests -testResults Logs/PAX/EditMode-Release.xml -logFile Logs/PAX/EditMode-Release.log`.
- PlayMode: `-runTests -testPlatform PlayMode -assemblyNames Pax.PlayModeTests -testResults Logs/PAX/PlayMode-Release.xml -logFile Logs/PAX/PlayMode-Release.log` (graphics enabled for captures).
- Windows build: `-executeMethod Pax.Editor.PaxPrototypeAuthoring.BuildWindows -quit -logFile Logs/PAX/BuildWindows-Final.log`.
- Built-player diagnostic: launch `Builds/PAX-Prototype-Windows/PAX.exe -paxSmoke -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile Logs/PAX/Player.log` with an absolute log path. The opt-in probe exits automatically and writes checks/screenshots to its sibling `SmokeEvidence` folder; ordinary launch never creates the probe.

## Suggested human playthrough

1. Launch the player normally or open the saved prototype scene and press Play. Move and rotate the camera, tap/hold jump, and climb the labelled steps.
2. Mine/place nearby tiles; check that the yellow selection and green/red placement hologram predict the result. Check blocked placement inside the player and unsupported air cells.
3. Switch smart/precise and toggle G for faded roof/floor solids; inspect the signed layer in the HUD. Physical solids and the five-tile 3D range still constrain actions.
4. Walk through the cave entrance, house, high hall, passage and low room. Rotate beside walls, leave/re-enter, and inspect roof edits. The shaft is a real drop with a jumpable exit step.

Next recommended milestone: a human keyboard/mouse playthrough of this bounded slice, with any visibility/targeting feedback fixed before expanding gameplay or moving to `CodexWorkspace`.
