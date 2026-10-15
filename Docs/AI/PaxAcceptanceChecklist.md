# PAX prototype acceptance review

Reviewed 16 October 2026 for the focused PAX sleep-session trial. Evidence is local Unity execution plus coordinator source/render review. This does not claim a complete game or a human keyboard/mouse playthrough.

## Required prototype gates

| Requirement | Result and evidence |
| --- | --- |
| Saved, editable test scene and Inspector references | Passed: `Assets/PAX/Scenes/PaxPrototype.unity`, serialized source review and 13/13 `EditMode-Release.xml` checks. 1,695 authored tiles, prefab, materials and UXML HUD. |
| Idempotent authoring, build scene and preserved AI config | Passed: byte-preserving authoring test; inspected Build Settings retains `com.unity.dt.app-ui`. |
| Asset metadata and appropriate assemblies | Passed: no missing meta files under Assets/PAX; Editor-only authoring and test assemblies use TestAssemblies. Windows player excludes PAX Editor/test assemblies. |
| Camera-relative movement and normalized diagonal | Passed: movement scenario before/after rotation in 19/19 `PlayMode-Release.xml`; standalone movement smoke. |
| Two-tile visual body, variable jump and solid collision | Passed: held/tap jump, floor/wall/ceiling and one-high clearance cases. Capsule is 1.96 high with a 0.04-unit contact tolerance; visual remains two tiles high. |
| Authored vertical/cave traversal | Passed: real jumps and landings on both steps, stable reference updates and walking through the authored two-high cave doorway. Coordinator inspected landing and cave-entry renders. |
| Five-tile mining/placement safety | Passed: 3D reach, support, occupied-cell/player overlap and LOS rejection; world/collider updates in Play Mode and standalone smoke. |
| Smart/precise modes, target and placement previews | Passed: serialized controls/source review, precise and rotated selection, exact preview/click mutation and G faded-solid targeting tests. Signed layer text is visible in player HUD. |
| Contextual local cutaway/ghost presentation | Passed for the authored cases below: local radius and vertical relevance, weighted roofs, bounded confinement, smooth fade and camera obstruction precedence. |
| Collision and world light independent of camera hiding | Passed: collider/shadow invariance, hidden-tile sunlight and actor-filter tests; inspected shaded world beneath faded roofs. |
| Windows player compilation | Passed: x86_64 development build, 0 errors, 485 installed inference-package shader warnings. Latest capture-probe build: 187,258,883 bytes, 11.52s. |
| Standalone runtime and full HUD rendering | Passed: 15/15 `PlayerSmoke.txt` checks; coordinator inspected combined world/HUD captures from the built player. Hidden-window rendering uses an opt-in offscreen capture target. |
| Git checkpoint and normal push | Pending implementation-owner confirmation and coordinator SHA verification. |

Release XML source paths are `Logs/PAX/EditMode-Release.xml` and `Logs/PAX/PlayMode-Release.xml`; tracked copies and selected captures are under `Docs/AI/PaxEvidence/`. Logs are local ignored artifacts. Tests that initially failed are documented in the milestone report; later passing release results supersede them.

## Rendered scenario review

| Case | Evidence and scope |
| --- | --- |
| Open field | Player open-field HUD frame: readable player, floor, previews, controls and live status. |
| House and camera rotation | House-interior, House-rotated and player house HUD frames; obstruction/restoration and fade tests. Continuous human walk outside/inside/outside was not run. |
| High hall, tiny room and narrow connector | High-hall, Low-room-interior and Narrow-passage frames; differentiated roof-gap/context tests and bounded flood. |
| Cave and underground shaft | Cave-interior, Cave-entry-walk and player shaft HUD frames; local cutaway retains distant geometry and physical illumination. |
| Short jump/higher landing | Variable-jump/reference assertions and Step-two-landing frame, with actual movement and grounding. |
| Roof mining/wall changes | Geometry-edit invalidation tests restore roof coverage; exact placement/mining tests cover world mutations. |
| Strong enclosure/obstruction priority | House/shaft contexts plus explicit obstruction-priority assertions. A separate artificial fully-surrounded stress fixture was not run. |

## Explicit limits

- Tests drive motor, pointer and camera commands. A person has not tested raw keyboard/mouse input; no claim of human playthrough is made.
- Fade convergence uses `1 - exp(-sharpness * deltaTime)`. A coordinator calculation at 30/60/120 Hz with the authored sharpness reaches the same final opacity; visual inspection and Play Mode execution used the captured timing. This is not a display-frame-rate/performance benchmark.
- Long falls hold the last grounded reference until landing or the configured respawn threshold; extreme-depth readability has not been stress-tested.
- Above/same/below uses bounded coloured cube edges plus signed target-layer HUD text. Merged silhouettes and full room segmentation are future work.
- Lighting is coarse tile occupancy toward the assigned sun, not full propagation/day-night/torch gameplay. Hidden solids continue to block that calculation.
- HUD is informational and has no clickable controls; UI button click consumption is not applicable.
- No external assets, purchases or paid APIs were used. Unrelated games remain idle.

Detailed controls, commands, failure diagnosis and limitations are in `PaxMilestoneReport.md`. Remaining acceptance action is Git checkpoint/push verification; no further gameplay expansion is required for this trial.
