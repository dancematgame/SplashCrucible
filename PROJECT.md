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
- Interactive features must otherwise remain restricted to reading or driving the local `XBMPetParty` Team Composition UI.
- Do not use packet/network helpers, combat actions, commands, server-bound agent actions, or unrelated state-changing APIs for Team Composition convenience features.
- Before reproducing a native UI interaction, first observe the exact native event/callback where practical instead of guessing.
- **Window sizing rule:** keep the current minimum window size at `560 x 360`. Do not increase the minimum size without explicit user approval.

## Current architecture
Splash Crucible has one persistent main window which is intended to remain open continuously. The information shown in that window changes according to the current Crucible context.

Current modes:
1. **Team Selection** — outside the instance, editing the 12-BST team.
2. **Board Selection** — choosing which board/challenge to enter.
3. **Map** — playable Crucible map before arrival in the selected enemy arena.
4. **Arena** — the selected Board Layout top enemy has appeared as a targetable local object.
5. **Results** — potentially useful later.

## Confirmed / observed UI findings
- `XBMPetParty` is the Team Composition box.
- `XBMPetParty` appears both during outside-instance Team Selection and on the playable map when assigning BSTs to Horns, so it is not a unique mode marker.
- `XBMStageMap` is the Board Selection graphic, not the playable map.
- `XBMStageList` is the Board Selection list.
- `XBMContentsMainHUD` is present while inside the Crucible and is used as the broad in-instance signal.
- `XBMStageDetailList` is the Board Layout window.
- `XBMPetParty` exposes the displayed 12-BST list through repeated `AtkValue` row blocks.
- Each displayed BST row uses a stride of 77 `AtkValue` entries.
- Row 0 name is at index `9`; subsequent names are `9 + (row * 77)`.
- Within each 77-value row block, current HP is at relative offset `+11` and max HP at `+12`. This was confirmed from Treant displaying `333/891`, with the corresponding row diagnostic values `+11 = 333` and `+12 = 891`.
- Row 0 Horn assignment state is at index `80`; subsequent assignment states are `80 + (row * 77)`.
- Confirmed Horn assignment encoding: `0 = Horn 1`, `1 = Horn 2`, `2 = Horn 3`, `3 = unassigned`.
- Example observed row assignment indices: first row `80`, second row `157`, third row `234`.
- Native Team Composition `ListItemClick` diagnostics confirmed direct zero-based list mapping: Vulture/row 1 reports `SelectedIndex=0` and `RendererIndex=0`; Bat/row 2 reports `1`; Dullahan/row 3 reports `2`.
- Native mouse diagnostics confirmed `MouseButtonId=0` with no modifier for left-click and `MouseButtonId=1` with no modifier for right-click.
- `SelectItem(row, true)` changed native list selection but did not visibly activate the BST row.
- A bare `DispatchItemEvent(row, AtkEventType.ListItemClick)` could be misinterpreted as a right-click because it did not reliably carry mouse-button context.
- Squad activation now dispatches the local native list event while synchronously normalizing only Splash-generated events to the confirmed left-click context (`MouseButtonId=0`, no modifier). This has been runtime-validated to reproduce the intended native left-click behavior.
- Party rows use the same native list-click path: clicking an assigned BST in Party looks up its zero-based Squad row and dispatches that same local left-click, so it removes/toggles the assignment exactly as clicking that BST in Squad does.
- Board Layout enemy data uses a confirmed 40-AtkValue stride. First enemy name is `[57]`, first enemy weakness label is `[61]`, and first enemy weakness value is `[62]`. Second enemy equivalents were observed at `[97]`, `[101]`, and `[102]`.
- For current gameplay, only the **top enemy** drives weakness highlighting. Multi-enemy stride information is retained for future extension.
- The Board Layout top enemy name is cached from `[57]` and used as the arena-arrival signal.
- Native `Commence Battle` observation on `XBMStageDetailList` produced `ButtonClick | EventParam=9`; Splash reproduces that local addon ReceiveEvent path from its own button.
- See `XBM_UI_MAP.md` for the current full mapping table and confidence notes.

## Mode detection
- `XBMPetParty` visible while `XBMContentsMainHUD` is absent -> **Team Selection**.
- `XBMStageMap` visible while not inside the Crucible -> **Board Selection**.
- `XBMContentsMainHUD` visible and the current Board Layout top enemy has not yet appeared -> **Map**.
- Once the cached top enemy appears as a targetable object in the local object table, the current board is latched into **Arena**.
- Arena remains latched even if the enemy later dies or despawns, preventing a false return to Map during/after the fight.
- Opening a new `XBMStageDetailList` Board Layout clears the Arena latch for the next board.
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

Auto-attack Aspect is stored for all 50 BSTs using the observed attack-type values: elemental aspects plus Slashing, Piercing, Blunt, and Unaspected. The UI uses the game's own bitmap-font symbols: `ElementFire`, `ElementIce`, `ElementWind`, `ElementEarth`, `ElementLightning`, `ElementWater`, `RedStar` for Unaspected, and the native Blunt/Piercing/Slashing damage symbols. Hovering the icon shows the full auto-attack type. `Magic Barrier` is normalized to `Barrier` in code-side metadata.

Colour is rendered as a coloured circle; Borrow Type and Tempered Release Type are rendered as text. HP is displayed as a percentage at the right side of each row, with a hover tooltip showing the exact `current/max` values. Blank metadata values display as an em dash.

## Current implementation state
- The main Splash Crucible window is permanently visible.
- The minimum window size remains `560 x 360` and must not be increased without explicit user approval.
- **Party** displays the three assigned BSTs with colour, native auto-attack Aspect icon, Borrow Type, Tempered Release Type, and HP percentage, without Horn-number prefixes.
- **Squad** lists all 12 BSTs in Team Composition row order.
- Squad rows show BST name, coloured circle, native auto-attack Aspect icon, Borrow Type, Tempered Release Type, and HP percentage, with no redundant column-header row.
- Squad row text is visually bold for faster scanning.
- Horn names, squad names, and HP values update live while Team Composition is open.
- The most recently read Horn assignments, squad list, and HP values are cached in plugin memory so they remain visible after the native Team Composition window closes.
- Squad rows are clickable while Team Composition is open and reproduce the native left-click row activation path.
- Assigned Party rows are also clickable while Team Composition is open and route through the matching Squad row, so they can remove/toggle that assignment through the same validated native UI path.
- While `XBMStageDetailList` is visible, Splash reads and caches the top enemy name from AtkValue `[57]` and its weakness from `[62]`.
- Any Party or Squad BST whose auto-attack Aspect matches the cached top-enemy weakness gets a visible highlight around its Aspect icon.
- `Summon 1` appears centered under Party. Splash checks the local object table for an owned active BST whose name matches the cached Squad. When no active BST is detected, the button is highlighted; pressing it synthesizes the user's existing Numpad 6 bind. When a BST is already active, the button is not highlighted and does nothing.
- Arena-entry auto-summon uses the same cached top-enemy appearance that drives Arena detection. If the player is inside the Crucible, Board Layout is closed, the cached top enemy becomes targetable, and no active BST exists, Splash sends Numpad 6 once for that board. Opening the next Board Layout resets the one-shot gate.
- A Splash `Commence Battle` button is shown while `XBMStageDetailList` is open and reproduces the observed `ButtonClick` / `EventParam=9` local UI path.

## Immediate validation targets
1. Confirm the mode reads **Map** on the playable map before entering the selected enemy arena.
2. Confirm the mode switches to **Arena** when the cached top enemy appears.
3. Confirm Arena remains latched after that enemy dies/despawns.
4. Confirm opening the next Board Layout resets the next board to Map/pre-arena state.
5. Confirm entering an arena with no active BST auto-summons Horn 1 exactly once.
6. Confirm entering an arena with an already active BST does not synthesize Numpad 6.
7. Confirm the Splash `Commence Battle` button reproduces the native button behavior.
8. Confirm HP percentages match the native Team Composition values and remain cached when it closes.

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
