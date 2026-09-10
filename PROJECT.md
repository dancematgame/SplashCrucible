# Splash Crucible — Project State

## Purpose
Splash Crucible is a personal-use Dalamud plugin for BST Crucible content. Its primary purpose is accessibility: reduce the cognitive/input-management burden of Crucible systems and present useful calculated information clearly.

## Development rules
- GitHub is the source of truth for project state and code.
- Prefer small, testable changes over speculative large implementations.
- Verify current Dalamud/ClientStructs APIs before relying on them.
- The project does not need to meet requirements for publication in the official Dalamud plugin repository.
- Preserve accessibility as the main design objective.
- Refer to Beastmaster only as BST in project-facing text.
- Do not mention the game title in project descriptions or documentation.
- Do not guess XBM UI meanings when they can be established from diagnostics or current client structures.
- **Hard interaction boundary:** Splash Crucible must not intentionally send data or actions to the game server unless the user explicitly requests a scoped exception for a specific feature.
- Interactive features must otherwise remain restricted to reading or driving local UI state or synthesizing explicitly requested keyboard input.
- Do not use packet/network helpers, combat actions, commands, server-bound agent actions, or speculative state-changing APIs.
- Before reproducing a native UI interaction, first observe the exact native event/callback where practical instead of guessing.
- **Window sizing rule:** keep the current minimum window size at `560 x 360`. Do not increase the minimum size without explicit user approval.

## Current architecture
Splash Crucible has one persistent main window which is intended to remain open continuously.

The top-level displayed states are intentionally limited to:
1. **Select Squad** — outside the Crucible duty, with the Team Composition UI available for squad selection.
2. **Map** — inside the Crucible duty, before arrival in the selected enemy arena, and again after returning from an arena.
3. **Arena** — the cached top Board Layout enemy has appeared as a targetable local object.
4. **Unknown / Idle** — none of the above can be established.

## Confirmed / observed UI findings
- `XBMPetParty` is the Team Composition box.
- `XBMPetParty` appears both during outside-duty squad selection and on the playable map when assigning BSTs to Horns, so it is not a unique state marker by itself.
- `XBMStageMap` is a board/challenge selection graphic, not the playable map and not a top-level Splash state.
- `XBMStageList` is the Board Selection list.
- `XBMContentsMainHUD` is present while inside the Crucible and is used as the broad in-duty signal.
- `XBMStageDetailList` is the Board Layout window.
- `XBMResult` is the post-encounter Results panel.
- Several XBM addons can remain allocated after being hidden. Pointer existence from `GetAddonByName` therefore must not be treated as equivalent to visible UI state. Splash now checks `AtkUnitBase.IsVisible` for Team Composition, Board Layout, and Results.
- `XBMPetParty` exposes the displayed 12-BST list through repeated `AtkValue` row blocks.
- Each displayed BST row uses a stride of 77 `AtkValue` entries.
- Row 0 name is at index `9`; subsequent names are `9 + (row * 77)`.
- Within each 77-value row block, current HP is at relative offset `+11` and max HP at `+12`.
- Row 0 Horn assignment state is at index `80`; subsequent assignment states are `80 + (row * 77)`.
- Confirmed Horn assignment encoding: `0 = Horn 1`, `1 = Horn 2`, `2 = Horn 3`, `3 = unassigned`.
- Native Team Composition `ListItemClick` diagnostics confirmed direct zero-based list mapping.
- Native mouse diagnostics confirmed `MouseButtonId=0` with no modifier for left-click and `MouseButtonId=1` with no modifier for right-click.
- Squad activation dispatches the local native list event while normalizing only Splash-generated events to the confirmed left-click context. This has been runtime-validated.
- Party rows use that same validated Squad row-click path.
- Board Layout enemy data uses a confirmed 40-AtkValue stride. First enemy name is `[57]`, first enemy weakness label is `[61]`, and first enemy weakness value is `[62]`. Second enemy equivalents were observed at `[97]`, `[101]`, and `[102]`.
- For current gameplay, only the top enemy drives weakness highlighting.
- The Board Layout top enemy name is cached from `[57]` and used as the Arena-arrival signal.
- Native `Commence Battle` observation on `XBMStageDetailList` produced `ButtonClick | EventParam=9`.
- The earlier direct synthetic call to `AtkEventListener.ReceiveEvent(ButtonClick, 9, null, null)` crashed the client and must not be retried.
- The current Commence Battle implementation reuses the native button's stored `AtkEvent*` and has now been runtime-validated successfully.

## State detection
- `XBMPetParty` visibly open while `XBMContentsMainHUD` is absent -> **Select Squad**.
- `XBMContentsMainHUD` present and the current cached top enemy has not yet appeared -> **Map**.
- Once the cached top enemy appears as a targetable object in the local object table -> **Arena**.
- Arena remains latched if the enemy later dies/despawns so the state does not drop early during the encounter.
- Preferred Arena -> Map signal: `XBMResult` becomes visibly open during Arena and then visibly closes while the in-duty HUD remains present. Earlier code checked only addon pointer existence, which could never observe the hide transition if the addon remained allocated.
- Runtime-proven fallback: if Team Composition becomes visibly open while still inside the duty, Splash clears Arena -> Map. This prevents Arena remaining latched forever if the Results transition is not usable for a particular encounter.
- Opening a new visible `XBMStageDetailList` Board Layout also clears the Arena latch for the next board.
- Losing `XBMContentsMainHUD` clears the Arena latch because the player is no longer considered inside the Crucible.

## BST metadata
A user-maintained 50-BST property table is incorporated into the UI through `SplashCrucible/Data/PetMetadata.cs`, derived from the supplied `Pet Properties.csv`.

The current visual metadata uses:
- `Pet`
- `Colour`
- auto-attack `Aspect`
- `Borrow Type`
- `Tempered Release Type`
- live HP percentage read from `XBMPetParty`

Auto-attack Aspect is stored for all 50 BSTs using elemental aspects plus Slashing, Piercing, Blunt, and Unaspected. The UI uses the game's own bitmap-font symbols. `Magic Barrier` is normalized to `Barrier` in code-side metadata.

## Current implementation state
- The main Splash Crucible window is permanently visible.
- The minimum window size remains `560 x 360` and must not be increased without explicit user approval.
- **Party** displays the three assigned BSTs with colour, native auto-attack Aspect icon, Borrow Type, Tempered Release Type, and HP percentage.
- **Squad** lists all 12 BSTs in Team Composition row order with the same metadata.
- Squad row text is visually bold for faster scanning.
- Horn names, squad names, and HP values update live while Team Composition is visibly open and are cached after it closes.
- Squad and assigned Party rows are clickable while Team Composition is visibly open using the validated native left-click path.
- While Board Layout is visibly open, Splash reads and caches the top enemy name from AtkValue `[57]` and weakness from `[62]`.
- Any Party or Squad BST whose auto-attack Aspect matches the cached top-enemy weakness gets a visible highlight around its Aspect icon.
- `Summon 1` is the final control at the bottom of the Splash window. It remains centered and synthesizes the user's existing Numpad 6 bind when no active squad BST is detected.
- Arena auto-summon is tied to Map -> Arena. The check is queued on Arena entry, waits 750 ms, then retries locally every 250 ms for up to 5 seconds if an owned BST object is still reported. Once no active BST is reported, Splash sends Numpad 6 exactly once for that board.
- Auto-summon lifecycle is now reset on the **visible rising edge** of Board Layout, not only `AddonEvent.PostSetup`. This fixes the case where `XBMStageDetailList` is reused between boards and the one-shot summon gate otherwise remains completed forever after the first encounter.
- `Commence Battle` is shown while Board Layout is visibly open. Splash scans the native button components for their real stored `ButtonClick` event with `EventParam=9` and passes that actual `AtkEvent*` back through the addon receive-event path. This implementation is runtime-validated and works.

## Server-interaction boundary
- Splash contains no packet-writing, network helper, action-manager, chat-command, or agent-action implementation for these features.
- Team Composition row selection is performed through the local addon list event path.
- `Summon 1` and Arena auto-summon synthesize the user's existing Numpad 6 keyboard bind; the game may then perform its normal server-side action as if the user pressed that key.
- `Commence Battle` replays the native UI button's own stored event locally. The resulting normal game action may cause server communication, exactly as pressing the native button does. Splash itself does not construct or send a network packet.

## Immediate validation targets
1. Confirm **Select Squad** while outside the duty with Team Composition open.
2. Confirm **Map** while inside the duty before the cached enemy appears.
3. Confirm **Arena** when the cached top enemy appears.
4. After an encounter, confirm the visible Results transition changes Arena -> Map before Team Composition is reopened; if not, confirm Team Composition still provides the fallback reset.
5. Confirm a newly opened Board Layout resets the per-board auto-summon gate even when the addon object itself was reused.
6. Confirm entering Arena with no active BST auto-summons Horn 1 after the short delay/retry window.
7. Confirm entering Arena with an already active BST does not synthesize Numpad 6.
8. Confirm `Summon 1` remains at the bottom of the Splash UI.
9. `Commence Battle` is runtime-validated as working with the native stored event path.

## Local workflow
Repository: `https://github.com/dancematgame/SplashCrucible`

Local checkout:
`F:\ff port\SplashCrucible`

After repository changes:
```powershell
cd "F:\ff port\SplashCrucible"
git pull
dotnet build
```

The resulting DLL is loaded as a Dalamud development plugin for testing.

## Handoff rule for future chats
A fresh chat should first inspect this repository, especially `PROJECT.md`, `XBM_UI_MAP.md`, `SplashCrucible/Plugin.cs`, and the main window class, before suggesting or making changes. GitHub is the continuity layer; do not rely on previous-chat memory.
