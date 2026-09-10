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
- **Hard interaction boundary:** Splash Crucible must not intentionally send data or actions to the game server.
- Interactive features must remain restricted to reading or driving the local `XBMPetParty` Team Composition UI unless the user explicitly changes this rule.
- Do not use packet/network helpers, combat actions, commands, server-bound agent actions, or unrelated state-changing APIs for Team Composition convenience features.
- Before reproducing a native Team Composition interaction, first observe the exact native UI event/callback and verify that the implementation is limited to the local addon UI path.

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
- `XBMContentsMainHUD` is present on the playable map and therefore must not be treated as a Combat-only marker.
- `XBMPetParty` exposes the displayed 12-BST list through repeated `AtkValue` row blocks.
- Each displayed BST row uses a stride of 77 `AtkValue` entries.
- Row 0 name is at index `9`; subsequent names are `9 + (row * 77)`.
- Row 0 Horn assignment state is at index `80`; subsequent assignment states are `80 + (row * 77)`.
- Confirmed Horn assignment encoding: `0 = Horn 1`, `1 = Horn 2`, `2 = Horn 3`, `3 = unassigned`.
- Example observed row assignment indices: first row `80`, second row `157`, third row `234`.
- Native Team Composition `ListItemClick` diagnostics confirmed direct zero-based list mapping: Vulture/row 1 reports `SelectedIndex=0` and `RendererIndex=0`; Bat/row 2 reports `1`; Dullahan/row 3 reports `2`.
- Native mouse diagnostics confirmed `MouseButtonId=0` with no modifier for left-click and `MouseButtonId=1` with no modifier for right-click.
- `SelectItem(row, true)` changed native list selection but did not visibly activate the BST row.
- A bare `DispatchItemEvent(row, AtkEventType.ListItemClick)` could be misinterpreted as a right-click because it did not reliably carry mouse-button context.
- Current Squad activation now dispatches the local native list event while synchronously normalizing only Splash-generated events to the confirmed left-click context (`MouseButtonId=0`, no modifier). This has been runtime-validated to reproduce the intended native left-click behavior.
- See `XBM_UI_MAP.md` for the current full mapping table and confidence notes.

## BST metadata
A user-maintained 50-BST property table is incorporated into the UI through `SplashCrucible/Data/PetMetadata.cs`, derived from the supplied `Pet Properties.csv`.

The current visual metadata uses:
- `Pet`
- `Colour`
- auto-attack `Aspect`
- `Borrow Type`
- `Tempered Release Type`

Auto-attack Aspect is stored for all 50 BSTs using the observed attack-type values: elemental aspects plus Slashing, Piercing, Blunt, and Unaspected. In the UI it is rendered as a compact icon immediately beside the BST's colour dot; hovering the icon shows the full auto-attack type. `Magic Barrier` is normalized to `Barrier` in code-side metadata.

Colour is rendered as a coloured circle; Borrow Type and Tempered Release Type are rendered as text. Blank metadata values display as an em dash.

## Current implementation state
- The main Splash Crucible window is permanently visible.
- **Current Party** reads the 12 `XBMPetParty` rows and displays Horn 1 / Horn 2 / Horn 3 together with the assigned BST's colour, auto-attack Aspect icon, Borrow Type, and Tempered Release Type.
- **Current Squad** is displayed directly below Current Party and lists all 12 BSTs in Team Composition row order.
- Current Squad rows show BST name, coloured circle, auto-attack Aspect icon, Borrow Type, and Tempered Release Type, with no redundant column-header row.
- Current Squad row text is visually bold for faster scanning.
- Horn names and squad names update live while Team Composition is open.
- The most recently read Horn assignments and squad list are cached in plugin memory so they remain visible after the native Team Composition window closes.
- Current Squad rows are clickable while Team Composition is open and reproduce the native left-click row activation path.
- The temporary Team Composition mouse diagnostic has been removed after successful validation.
- The next mode-detection task is to distinguish playable Map from active Combat using reliable observed state rather than a guessed single-addon marker.

## Immediate diagnostic targets

### Mode detection
Capture/compare the active XBM addon set in:
1. Playable map, no Team Composition window open.
2. Playable map with `XBMPetParty` open for Horn assignment.
3. Active combat encounter.

If Combat does not expose a unique XBM addon, detect it via another reliable game-state signal.

### Current Party / Current Squad validation
Verify that:
1. Assigning or replacing BSTs in Horn 1 / Horn 2 / Horn 3 updates the corresponding Current Party row and metadata.
2. Current Squad shows all 12 BSTs in native Team Composition order with correct metadata.
3. Auto-attack Aspect icons line up beside the affinity colour dots and show the expected tooltip.
4. Closing Team Composition leaves the last known Horn assignments and Current Squad visible.
5. With Team Composition open, clicking different Current Squad rows continues to reproduce only the normal native left-click behavior.
6. With Team Composition closed, Current Squad clicks do nothing.

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
