# PAX supervised development

16 October 2026. Local development only; public pushing remains denied and will not be attempted.

## Workspace and protection

- Sole implementation workspace: `C:\Users\user\.codex\worktrees\pax-development\PAX_FYP1`.
- Branch: existing `CodexWorkspace`, fast-forwarded from `e82f1c9` to accepted prototype `42ad020`.
- Original checkout: `C:\Users\user\Documents\GitHub\PAX_FYP1`, still on `PrototypeBranch`. Its modified scene, Build Settings and untracked SceneTemplateSettings file must remain unchanged. Their SHA256 baselines are retained by this chat for verification.
- Absolute Samsara and shared asset originals remain untouched. No external assets, installations, publishing or paid APIs are needed.

## Milestones

1. Validated and ready for local commit: actionable mining/placement rejection reasons, predictable preview feedback and editable authored HUD. Range, support, collision, ghost targeting, LOS, bounds and protected tiles are preserved.
2. Next immediately after commit: local edited-tile save/load for repeatable playtests, versioned validation, malformed-input rejection and a recoverable previous save.
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
