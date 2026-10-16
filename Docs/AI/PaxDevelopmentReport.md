# PAX supervised development

16 October 2026. Local development only; public pushing remains denied and will not be attempted.

**Stopped at the human/coordinator's explicit instruction.** Three validated implementation milestones are locally committed. The next visibility stress case had only been announced/read for planning; no runtime or test implementation for it was started. No further development or publication will proceed in this run.

## Workspace and protection

- Sole implementation workspace: `C:\Users\user\.codex\worktrees\pax-development\PAX_FYP1`.
- Branch: existing `CodexWorkspace`, fast-forwarded from `e82f1c9` to accepted prototype `42ad020`.
- Original checkout: `C:\Users\user\Documents\GitHub\PAX_FYP1`, still on `PrototypeBranch`. Its modified scene, Build Settings and untracked SceneTemplateSettings file must remain unchanged. Their SHA256 baselines are retained by this chat for verification.
- Absolute Samsara and shared asset originals remain untouched. No external assets, installations, publishing or paid APIs are needed.

## Milestones

1. Complete, local commit `d93703f07fe95c06cfbb9e8c5f918df8231d6897`: actionable mining/placement rejection reasons, predictable preview feedback and editable authored HUD. Range, support, collision, ghost targeting, LOS, bounds and protected tiles are preserved.
2. Complete, local commit `466f3190ef2f3de93ae1f53f454691ffd9e0c9c1`: local edited-tile save/load for repeatable playtests, versioned validation, malformed-input rejection and a recoverable previous save.
3. Complete, local commit `af8752ee5afbe202ffad2310f27008c58a0ed5b2`: reduce measured unchanged-target feedback allocations while preserving live selection/validation and Inspector tuning.
4. Not started: the fully-enclosed visibility stress case remains a future validation item. The stop arrived during planning reads; no new stress-fixture/runtime/test code was added.

## Validation and permission state

- Preparation: original branch/changes verified; managed worktree created and attached; existing CodexWorkspace fast-forward completed normally.
- Completed checkpoint evidence is listed below. Latest relevant suites: 26/26 EditMode (milestone 2), 42/42 PlayMode (milestone 3), and 21/21 Windows player checks. The accepted prototype's earlier evidence remains separate.
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

## Milestone 3 measurement

A bounded PlayMode probe warms targeting, freezes command input/presentation changes, then measures 500 unchanged-pointer selections in each cursor mode. The managed per-thread counter returned zero even for a known allocation; raw `GC.Alloc` marker values also failed the control. Those results are not allocation evidence. A measurement-only brace error was corrected before any runtime optimization.

The replacement Unity `GC Allocated In Frame` recorder verifies its unit is Bytes and detected a **262,176-byte delta** for a known 256-KiB allocation. With matched idle frames, its baseline was **493,282 excess frame bytes / 500 smart selections** and **468,000 / 500 precise selections** (`TargetingAllocation-Before.txt` and XML). These are frame-wide estimates, not per-thread totals or a frame-rate benchmark.

The improvement caches only unchanged feedback text, avoids list-enumerator allocation and redundant material assignments. Live target/physics/visibility evaluation is retained; the cache includes candidate/placement cells, both action reasons, signed layer and editable reach. A regression checks that the same pointer immediately reflects a new/removed physical blocker.

The matched post-change probe passed its known-allocation control and measured **5,210 excess frame bytes / 500 smart selections** and **0 excess / 500 precise selections** (the latter at the matched idle-frame level). This is roughly 99% lower measured excess allocation in this bounded repeated-target case; it is not a claim that whole-game GC or frame time is zero. Timings were 107.65→98.46 ms smart and 4.79→3.57 ms precise per 500 calls, descriptive single-run figures only. Full regressions and target build validation are next.

First full regression run: 41/42 passed. A HUD text assertion read the initial label while the builder state had correctly changed; the new unchanged-pointer blocker regression passed. A bounded passing probe measured 15 captured frames as only 0.0492s unscaled, below the HUD's 0.1s polling interval. The test now waits on the actual unscaled clock and retains its exact label/action assertions; no runtime UI binding change was justified. Temporary probe logging was removed. Full rerun and build remain before milestone completion.

Final full regression: **42/42 PlayMode cases passed** (`DevelopmentEvidence/TargetingPlayMode.xml`), including the validated byte-valued allocation control/budget and unchanged-pointer physical-blocker updates. Runtime serialization/wiring and persistence code are unchanged from milestone 2's 26/26 EditMode pass. Windows build passed with 0 errors, the same 485 inference-package warnings, 187,308,863 bytes and 13.45s (`TargetingBuildSummary.txt`); **21/21 player checks passed** (`TargetingPlayerSmoke.txt`) with no runtime errors/warnings.

## Real unattended prompt observed

The human reported a Windows Defender Firewall popup for Unity private/public network access and instructed development to continue without broadly allowing inbound access. The most recent launch was Editor batch authoring (`EnsurePlaytestSave`, PID 26632), which exited normally; before that was the Windows development-player UX smoke run (PID 4400), also exited. Editor bridge/debugger/player-discovery services may explain the prompt, but listener diagnosis belongs to the coordinator. No firewall rules, network permissions or inbound dependencies were changed here. Local compile/tests and save-file work continue.

## Stop audit and handoff

- Runtime/feature commits: UX `d93703f07fe95c06cfbb9e8c5f918df8231d6897`; persistence `466f3190ef2f3de93ae1f53f454691ffd9e0c9c1`; measured targeting feedback `af8752ee5afbe202ffad2310f27008c58a0ed5b2`. A final documentation-only commit records this stop audit.
- The isolated branch remains `CodexWorkspace`. Original `PrototypeBranch` still points at accepted checkpoint `42ad02073e331bc10031d3fdeb4bab0b8f128417` (including implementation `a207eeb`). Its three initial modified/untracked files matched their startup SHA256 hashes exactly at the final audit.
- No `Unity.exe` or `PAX.exe` process was running at the final process audit. No app was force-killed. Build outputs, local logs and validation save slots are ignored artifacts.
- Current Windows build: `C:\Users\user\.codex\worktrees\pax-development\PAX_FYP1\Builds\PAX-Prototype-Windows\PAX.exe`. Preserve the adjacent data/runtime files. Normal launch uses F5/F9/F8 for the user's local playtest slot; `-paxSmoke` uses a separate disposable slot.
- Build status: 0 errors, 485 unchanged installed inference-package shader warnings. Public GitHub pushing remains specifically denied and was not retried in this supervised run. Native Git/file-copy worktree access used scoped approvals; no local permission blocker remains. The firewall popup is recorded without changing its rules.
- Remaining limits: human raw keyboard/mouse playthrough, fully-enclosed stress and long-fall/display-frame-rate/performance coverage remain distinct from the automated checks. The allocation result is only the matched repeated-target estimate; no whole-game FPS claim is made. Absolute Samsara remained idle.
