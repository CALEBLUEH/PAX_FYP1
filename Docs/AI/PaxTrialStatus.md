# PAX overnight prototype trial

## Authorization and boundary

The user authorized development on 16 October 2026 before departure, for a sleep-session automation trial. Only PAX is active. Absolute Samsara and the later two-game trip run remain inactive. Stop and pause continuation when the user requests it.

Work on `PrototypeBranch`, baseline `e82f1c9`. Complete focused movement/building/cutaway essentials before broader development. No survival, combat, economy, large procedural world or decorative polish is required.

## Coordination

- Coordinator chat: `01a13e11-3bc1-7013-8ecf-fb899988cf19`.
- Sole implementation owner: PAX chat `01a13e34-e713-7f20-bd8f-de7eb355e9fb`.
- Active goal: focused PAX prototype implementation and validation.
- Heartbeat: `pax-overnight-prototype-trial`, every 15 minutes.
- PAX chat owns runtime, authoring, tests, required build settings and `PaxMilestoneReport.md`. Coordinator owns this status and the acceptance checklist. No concurrent runtime edits.

## Current evidence

| Gate | Current result |
| --- | --- |
| Saved editable scene | Authored island, house, cave, high hall, narrow passage, shaft and steps; 1,695 tile instances after cave lintel addition |
| Saved references/build entry/idempotence | 13/13 passed in `Logs/PAX/EditMode-Release.xml` |
| Gameplay and presentation checks | 19/19 passed in `Logs/PAX/PlayMode-Release.xml` |
| Real traversal | Both step landings update stable floor reference; two-high cave entrance passed after clearance correction |
| Rendering | House/cave/hall/passage/shaft renders inspected; local smooth fade, bounded coloured ghost edges and physical tile sunlight visible |
| First Windows build | Succeeded, 0 errors, 485 package shader warnings; predates final clearance/lighting revision |
| Final Windows build | Succeeded, 0 errors, 485 package warnings, 187,258,883 bytes, 11.52s; latest `Logs/PAX/BuildWindows-PlayerRender.log` |
| Standalone gameplay/full HUD | 15/15 smoke checks passed; coordinator inspected nonblack combined world/player/HUD captures for open field, house and shaft |
| Git checkpoint/push | Pending final evidence review |

Passing Play Mode coverage includes camera-relative movement, normalized diagonal input, variable two-tile jump, solid wall/ceiling/one-high clearance, step/cave traversal, five-tile 3D reach, support/occupancy/player overlap, physical LOS, exact preview/click mutation, rotated precision rejection and positive selection, faded-solid targeting, signed height cue, local roof/confinement analysis, obstruction precedence, restoration, roof edits, stable reference and actor-independent tile sunlight.

Tracked evidence copies and detailed commands/limits belong in `Docs/AI/PaxMilestoneReport.md` and `Docs/AI/PaxEvidence/`. Earlier green results do not supersede later source changes; use the release results above for the current revision.

## Corrections verified during the trial

- First authoring failed with a prefab-reference exception. Corrected creation order/instantiation produced the saved scene successfully; the failed run saved no scene.
- Initial global cutaway/ghost presentation was constrained to local horizontal/vertical relevance. G toggles faded-solid targeting, and the HUD reports signed target height.
- Noisy dithering was replaced with smooth transparency. Independent opaque shadow-only children retain shadow casting; tile sunlight uses physical occupancy independently of camera opacity. Sunlight filters registered world tiles so moving actors cannot leave cached shading.
- Added fixtures initially sampled a nonexistent island corner and aimed behind an out-of-reach roof. Existence checks and ray diagnostics corrected the fixtures while retaining blocked-target rejection and a clear positive target.
- Real cave traversal exposed a capsule clearance defect. A probe showed height 2 stopped with either step offset, while height 1.96 passed. The saved controller now has 0.04 units of contact clearance, with the two-tile visual body preserved. The original doorway assertion and full 19-case regression suite pass.

## Build warnings and preservation

The first Windows x86_64 development build took 3m24.83s and reported 187,251,987 bytes, 0 errors and 485 shader warnings. These were classified as installed Unity AI inference shader diagnostics: 460 compute variants and 25 pixel-shader warnings; none name PAX shaders. Final-build counts must be recorded separately.

The pre-existing Unity AI App UI object in `EditorBuildSettings.asset` remains present. No other game or shared asset originals were edited by this trial. No external art, purchases or paid APIs were used.

## Next action

Gameplay, scene, rendering and build evidence have passed the focused prototype audit in `PaxAcceptanceChecklist.md`, with human input/performance limitations stated. Verify the implementation owner's final Git checkpoint/push. Then complete the goal and pause the heartbeat; do not add unrelated features.

Initial hidden-window screenshots were black despite 14 passing logic checks. An opt-in diagnostic probe now explicitly draws the actual camera and authored UI panel into a shared offscreen target; ordinary gameplay stays unchanged. The subsequent build passes 15 smoke checks and produces combined world/HUD images independently inspected by the coordinator. Tracked captures supersede those initial black files.

## Recovery and usage limits

Use bounded Unity operations, verify the project path before any Editor mutation and do not launch competing sessions. Avoid the Windows window-activation operation that timed out during setup. Never force-kill Codex, ChatGPT or Unity. The heartbeat cannot guarantee recovery from a hung desktop app; no external watchdog is installed.

Trial-start usage snapshot was 0 percent used in the five-hour window and 4 percent weekly, with three banked resets unused. This is not a current allowance reading. Do not consume banked resets without per-use confirmation required by the reset tool. No reset has been consumed by this coordinator.
