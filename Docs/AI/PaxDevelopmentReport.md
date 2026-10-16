# PAX supervised development

16 October 2026. Local development only; public pushing remains denied and will not be attempted.

## Workspace and protection

- Sole implementation workspace: `C:\Users\user\.codex\worktrees\pax-development\PAX_FYP1`.
- Branch: existing `CodexWorkspace`, fast-forwarded from `e82f1c9` to accepted prototype `42ad020`.
- Original checkout: `C:\Users\user\Documents\GitHub\PAX_FYP1`, still on `PrototypeBranch`. Its modified scene, Build Settings and untracked SceneTemplateSettings file must remain unchanged. Their SHA256 baselines are retained by this chat for verification.
- Absolute Samsara and shared asset originals remain untouched. No external assets, installations, publishing or paid APIs are needed.

## Milestones

1. Complete, local commit `d93703f07fe95c06cfbb9e8c5f918df8231d6897`: actionable mining/placement rejection reasons, predictable preview feedback and editable authored HUD. Range, support, collision, ghost targeting, LOS, bounds and protected tiles are preserved.
2. Validated and ready for local commit: local edited-tile save/load for repeatable playtests, versioned validation, malformed-input rejection and a recoverable previous save.
3. After those: choose small essential improvements from measured evidence, validating and locally committing each milestone. Continue until a stop instruction.

## Validation and permission state

- Preparation: original branch/changes verified; managed worktree created and attached; existing CodexWorkspace fast-forward completed normally.
- No implementation validation claimed yet for this run. The accepted prototype contains earlier 13 EditMode / 19 PlayMode passes and a successful Windows build; those are baseline evidence only.
- Public upload remains specifically denied. It is optional and does not block local implementation.

## Milestone 1 implementation

- A common typed rejection result drives both action validation and preview feedback. Messages explain bounds, 3D reach, occupied cells, player overlap, missing support, physical obstruction, protected foundation and removed targets.
- A blocked screen-space candidate retains its red preview and exact cell instead of disappearing. `SelectedCell` still excludes unreachable/physically occluded action targets. G still controls inclusion of faded solids without restoring opacity.
- Clicks revalidate through the same rules and preserve a brief result message. Successful edits synchronize physics and immediately refresh previews; rejected actions do not change the world revision.
- The existing UXML HUD now has authored mining, placement and last-action labels. Readiness colours and refresh/feedback timing remain editable in component fields. Existing serialized references/names are preserved.
- Initial isolated Unity compile succeeded (`Logs/PAX/UxCompile.log`). **29/29 PlayMode tests passed**, including all 19 accepted prototype cases and 10 UX cases (`Logs/PAX/UxPlayMode.xml`, tracked copy `DevelopmentEvidence/UxPlayMode.xml`). **13/13 EditMode checks passed**, including saved references and idempotence (`DevelopmentEvidence/UxEditMode.xml`).
- Windows x86_64 development build passed: 0 errors, 485 existing inference-package shader warnings, 187,284,266 bytes, 3m16.89s first build in this checkout (`DevelopmentEvidence/UxBuildSummary.txt`). **16/16 standalone checks passed**, including the new out-of-range feedback case (`DevelopmentEvidence/UxPlayerSmoke.txt`).
- Inspected the combined world/HUD frame `DevelopmentEvidence/Ux-Out-of-range-HUD.png`: the same far tile retains a red preview, and separate mine/place labels explain the five-tile 3D reach restriction. HUD text wraps inside its authored panel. No new scene wiring or package changes were required.
- Native Git under the restricted shell could not enter the managed directory. Scoped elevated Git commands work; no repository configuration or original checkout changes were needed.
- SHA256 verification after the PlayMode run matched all three protected original-checkout files exactly. Human permission to overwrite Inspector experiments was received, but isolation still makes that unnecessary.

## Milestone 2 implementation

- A version-1 XML playtest document stores removed/placed grid cells plus an authored-world signature. It contains no Unity object references, camera opacity or lighting state. Strict schema/version/size/count checks reject malformed and unknown data.
- World validation checks bounds, duplicates, protected foundations, authored-cell conflicts and player clearance before changing any live tile. Loading restores authored materials/collision/shadow references, invalidates geometry analysis once and avoids replacing unchanged objects.
- File writes use a same-directory temporary file and atomic replacement. A valid previous primary becomes `.bak`. Repairing a corrupt primary preserves it as `.invalid-<id>` and leaves the valid backup intact.
- Inspector-editable save names resolve under the local application data directory. Tests and the explicit smoke probe use a disposable validation directory instead of a person's real save slot.
- F5 saves, F9 loads and F8 loads the previous save. The isolated scene has an assigned save component/input/HUD reference, and an authored status label; `SaveAuthoring.log` confirms successful idempotent wiring. No Inspector assignments remain.
- **40/40 PlayMode tests passed** (`DevelopmentEvidence/SavePlayMode.xml`): 29 prior gameplay/UX cases plus 11 persistence cases. They include malformed primary recovery, semantic-invalid-primary backup preservation, protected/bounds/duplicate/world-signature rejection, player overlap, material/collider restoration, idempotent loading and roof-analysis invalidation.
- **26/26 final EditMode checks passed** (13 baseline + 13 file-format/atomic-backup cases; `DevelopmentEvidence/SaveEditMode.xml`). Windows x86_64 development build passed: 0 errors, 485 inherited inference-shader warnings, 187,308,083 bytes, 14.09s (`DevelopmentEvidence/SaveBuildSummary.txt`).
- **21/21 Windows player smoke checks passed**, including real local file creation/backup, malformed-file rejection without world mutation, previous-save recovery and corrupt-primary preservation (`DevelopmentEvidence/SavePlayerSmoke.txt`). Inspected `DevelopmentEvidence/Save-Recovery-HUD.png`: restored geometry and the authored save status render together correctly. No runtime errors/warnings were present in `SavePlayer.log`.
- Current and previous files are treated as compatible only after schema and authored-world edit validation. A syntactically valid protected-removal or wrong-world primary is preserved separately instead of replacing a recoverable `.bak`.

Manual persistence check: place/mine some tiles, press F5, make a different edit, then press F9 to restore the saved edits. Save another state and press F8 to recover the prior valid save. The normal slot is `Application.persistentDataPath/PAXPlaytests/pax-playtest.xml`; `.bak` holds the previous valid save. Loading does not move the player, and asks the player to step aside if restored solids would overlap the body. It restores edited tile state, not inventory, camera state or a full game profile.

## Real unattended prompt observed

The human reported a Windows Defender Firewall popup for Unity private/public network access and instructed development to continue without broadly allowing inbound access. The most recent launch was Editor batch authoring (`EnsurePlaytestSave`, PID 26632), which exited normally; before that was the Windows development-player UX smoke run (PID 4400), also exited. Editor bridge/debugger/player-discovery services may explain the prompt, but listener diagnosis belongs to the coordinator. No firewall rules, network permissions or inbound dependencies were changed here. Local compile/tests and save-file work continue.
