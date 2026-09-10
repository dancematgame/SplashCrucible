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
- `XBMPetParty` string `AtkValue` entries expose the displayed 12-BST list and associated stats/status text.
- Changing a Horn assignment does not appear to change the displayed BST-name strings, so Horn membership must be derived from another value/state rather than assuming dedicated Horn-name string indices.
- See `XBM_UI_MAP.md` for the current full mapping table and confidence notes.

## Current implementation state
- The main Splash Crucible window is permanently visible.
- The current build includes diagnostic support for observing active XBM addons.
- Earlier semantic mode guesses were intentionally rolled back where evidence showed they were incorrect.
- The next mode-detection task is to derive reliable context rules from actual observed UI/game state rather than single-addon guesses.
- A **Current Party** section exists for Horn 1 / Horn 2 / Horn 3, but the three assignments are not yet mapped to client data.
- The earlier string-only `XBMPetParty` diagnostic has been replaced by a before/after `AtkValue` comparator. The comparator snapshots every indexed `AtkValue`, including scalar values and strings, then shows only indices whose type/value changed after a Horn assignment action.

## Immediate diagnostic targets

### Mode detection
Capture/compare the active XBM addon set in:
1. Playable map, no Team Composition window open.
2. Playable map with `XBMPetParty` open for Horn assignment.
3. Active combat encounter.

If Combat does not expose a unique XBM addon, detect it via another reliable game-state signal.

### Current Party Horn mapping
Use the **Horn assignment diagnostic** while `XBMPetParty` is open:
1. Set Team Composition to a known state.
2. Press **Capture Baseline** in Splash Crucible.
3. Change exactly one Horn assignment in the native Team Composition UI.
4. Read the **Changed values** list in Splash Crucible.
5. Repeat with a different BST and/or different Horn to identify which `AtkValue` indices encode selected BST and Horn assignment state.
6. Once stable indices/semantics are confirmed, wire those values into the Current Party display and remove the temporary comparator.

If changing a Horn produces no meaningful `AtkValue` changes, the next diagnostic step is to inspect the `XBMPetParty` node/component state rather than infer from text.

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
