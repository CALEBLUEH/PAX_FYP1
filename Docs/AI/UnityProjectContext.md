# PAX project context

Prepared 16 October 2026 for the overnight PAX-only prototype trial.

- Root: `C:\Users\user\Documents\GitHub\PAX_FYP1`, branch `PrototypeBranch`.
- Baseline commit: `e82f1c9` (Codex setup). Assets were empty. Existing working change: `EditorBuildSettings.asset` contains the Unity AI App UI config reference; preserve it.
- Unity 6000.3.18f1, Built-in rendering, legacy Input Manager, Windows keyboard/mouse.
- Installed setup includes Coplay Unity MCP and Unity AI Assistant. No Unity tool is exposed to this chat at implementation start; do not route mutations to another game.
- Test Framework is present transitively in the package lock. No package installation is required for this milestone.
- Runtime: `Assets/PAX/Runtime`, Editor authoring/build: `Assets/PAX/Editor`, tests under `Assets/PAX/Tests`.
- Authored UI: `Assets/PAX/UI`; its panel references Unity's generated `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`. Keep that theme and its metadata in the checkpoint.
- Scene is saved to `Assets/PAX/Scenes/PaxPrototype.unity`. Grid tiles and references are authored and Inspector editable. The generator returns without modification when the scene exists.
- The grid uses integer X/Z centres and integer Y bottom faces. One unit is one tile. Player pivot is at its feet, nominal visual height 2, collision height 1.96 with diameter 0.9 for contact clearance inside a one-by-two-tile space. It cannot crouch into one-high openings.
- Source: human's `Desktop/Dummy Word File.docx`, PAX section. Prototype scope is movement, variable jump, mine/place, cursor previews, camera and contextual cutaway only. Other game sections, combat, progression and giant procedural worlds are excluded.
- Components own separate responsibilities. References are explicit, no runtime hierarchy-name lookups. Visibility changes shader properties only; colliders and solid shadow casting remain active.
- Each tile prefab includes an opaque `ShadowsOnly` child. Transparent tile surfaces receive coarse occupancy-based directional sunlight from `PaxWorldLighting`; lighting refreshes on grid/sun-direction changes and ignores presentation. Full propagation and day/night remain out of scope.
- No third-party art or downloaded assets are used. Materials/shaders and primitive scene content are created in this project.

Validation evidence and remaining limitations belong in `Docs/AI/PaxMilestoneReport.md`.
