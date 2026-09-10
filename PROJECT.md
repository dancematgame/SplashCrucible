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

Current mode targets:
1. **Team Selection** — outside the instance, editing the 12-BST team.
2. **Board Selection** — choosing which board/challenge to enter.
3. **Map** — playable Crucible map, including opening Team Composition to assign BSTs to Horn 1 / Horn 2 / Horn 3.
4. **Combat** — active encounter gameplay.
5. **Results** — potentially useful later.

## Confirmed / observed UI findings
- `XBMPetParty` is the Team Composition box.
- `XBMPetParty` appears both during outside-instance Team Selection and on the playable map when assigning BSTs to Horns, so it is not a unique mode marker.
- `XBMStageMap` is the Board Selection graphic, not the playable map.
- `XBMStageList` is the Board Selection list.
- `XBMContentsMainHUD` is present on the playable Crucible map and therefore must not be treated as a Combat-only marker.
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
- Native `Commence Battle` observation on `XBMStageDetailList` produced `ButtonClick | EventParam=9`; Splash reproduces that local addon ReceiveEvent path from its own button.
- See `XBM_UI_MAP.md` for the current full mapping table and confidence notes.

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
- Arena-entry auto-summon is now implemented using the cached Board Layout enemy rather than a fight-start signal. When a Board Layout opens, the one-shot summon gate is reset. After that Board Layout closes, if `XBMContentsMainHUD` confirms the player is in the Crucible and a targetable local object appears whose name exactly matches the cached top Board Layout enemy, Splash treats that as arrival in the boss arena. If no active squad BST is present, it sends Numpad 6 once to summon Horn 1. It will not repeatedly summon again during that same board encounter even if the BST later despawns or dies.
- A Splash `Commence Battle` button is shown while `XBMStageDetailList` is open and reproduces the observed `ButtonClick` / `EventParam=9` local UI path.

## Immediate validation targets

### Arena-entry auto-summon
Verify that:
1. Opening a Board Layout caches the correct top enemy name.
2. Before entering the arena, no automatic summon occurs.
3. After entering the arena, when that exact targetable enemy appears in the local object table and no BST is active, Splash sends Numpad 6 once.
4. If a BST is already active when the enemy appears, Splash does not send Numpad 6.
5. During the same encounter, losing the BST does not cause repeated automatic summons.
6. Opening the next Board Layout resets the one-shot gate for the next encounter.

### Party / Squad validation
Verify that:
1. Assigning or replacing BSTs updates Party and metadata.
2. Squad shows all 12 BSTs in native Team Composition order with correct metadata.
3. HP percentages match the native Team Composition values; for the observed Treant `333/891`, Splash should show approximately `37%`.
4. Hovering a percentage shows the exact current/max HP values.
5. Native auto-attack Aspect symbols line up beside the affinity colour dots and show the expected tooltip.
6. Opening a Board Layout with a known top-enemy weakness highlights every matching Aspect icon in Party and Squad.
7. On a multi-enemy Board Layout, only the first/top enemy weakness affects highlighting for now.
8. Closing Team Composition leaves the last known Party assignments, Squad, and HP percentages visible.
9. With Team Composition open, clicking different Squad rows continues to reproduce only the normal native left-click behavior.
10. With Team Composition open, clicking an assigned BST in Party removes/toggles that assignment exactly as clicking the same BST in Squad.
11. With Team Composition closed, Party/Squad clicks do nothing.

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
