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
- See `XBM_UI_MAP.md` for the current full mapping table and confidence notes.

## BST metadata
A user-maintained 50-BST property table is being incorporated into the UI. The current code-side lookup is `SplashCrucible/Data/PetMetadata.cs` and is derived from the supplied `Pet Properties.csv`.

The first visual metadata pass uses these CSV fields:
- `Pet`
- `Colour`
- `Borrow Type`
- `Tempered Release Type`

Current Squad resolves metadata by BST name. Colour is rendered as a coloured circle; Borrow Type and Tempered Release Type are rendered as text. Blank metadata values display as an em dash.

## Current implementation state
- The main Splash Crucible window is permanently visible.
- The current build includes diagnostic support for observing active XBM addons.
- Earlier semantic mode guesses were intentionally rolled back where evidence showed they were incorrect.
- The next mode-detection task is to derive reliable context rules from actual observed UI/game state rather than single-addon guesses.
- **Current Party** reads the 12 `XBMPetParty` rows and displays the BST assigned to Horn 1 / Horn 2 / Horn 3.
- **Current Squad** is displayed directly below Current Party and lists all 12 BSTs in Team Composition row order.
- Each Current Squad row now shows: BST name, coloured circle, Borrow Type, and Tempered Release Type.
- Horn names and squad names update live while Team Composition is open.
- The most recently read Horn assignments and squad list are cached in plugin memory so they remain visible after the native Team Composition window closes.
- The temporary `AtkValue` before/after comparator has been removed now that the row and assignment mapping is confirmed.

## Immediate diagnostic targets

### Mode detection
Capture/compare the active XBM addon set in:
1. Playable map, no Team Composition window open.
2. Playable map with `XBMPetParty` open for Horn assignment.
3. Active combat encounter.

If Combat does not expose a unique XBM addon, detect it via another reliable game-state signal.

### Current Party / Current Squad validation
Verify that:
1. Assigning a BST to Horn 1 immediately updates Current Party Horn 1.
2. Assigning BSTs to Horn 2 and Horn 3 updates the corresponding entries.
3. Replacing or clearing an assignment updates the correct Horn.
4. Current Squad shows all 12 BST names in the same order as Team Composition.
5. Each known BST resolves its CSV-derived colour, Borrow Type, and Tempered Release Type correctly.
6. Closing Team Composition leaves the last known Horn assignments and Current Squad visible in Splash Crucible.

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
