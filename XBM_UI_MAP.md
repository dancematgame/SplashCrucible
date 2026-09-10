# XBM UI Map

This file records observed XBM addon names and their likely visible UI purpose. Treat entries marked with `?` as provisional until confirmed in-game.

| XBM addon | Visible UI / interpretation | Confidence |
|---|---|---|
| `XBMStageList` | Board Selection List | High |
| `XBMStageMap` | Board Selection Graphic | High |
| `XBMContentsMainHUD` | Item Box? | Provisional |
| `XBMPetActionDetail` | Bottom Left Popup | High |
| `XBMPetParty` | Team Composition Box | High |
| `XBMStageDetailList` | Board Layout Window | High |
| `XBMResult` | Results Panel | High |
| `XBMMonsterBookDetail` | Master's Bestiary Right Page | High |
| `XBMMonsterNotebook` | Master's Bestiary Left Page | High |

## Important mode-detection findings

- `XBMPetParty` appears both during outside-instance Team Selection and when assigning BSTs to Horn 1 / Horn 2 / Horn 3 from the Crucible map. It therefore cannot distinguish those contexts by itself.
- `XBMStageMap` is the board/challenge selection graphic, not the playable Crucible map.
- `XBMContentsMainHUD` is present on the playable Crucible map and therefore is not sufficient by itself to identify Combat.
- Mode detection should be derived from combinations of active UI/state markers, not from a single guessed addon name.

## Contexts the main window must eventually distinguish

1. **Team Selection** — outside the instance, editing the 12-BST team.
2. **Board Selection** — choosing which board/challenge to enter.
3. **Map** — playable Crucible map, including opening Team Composition to assign BSTs to Horn 1 / Horn 2 / Horn 3.
4. **Combat** — active encounter gameplay.
5. **Results** — post-encounter/result UI, if useful later.

## Current diagnostic goal

Compare active XBM addons in these states:

- Playable map with no Team Composition window open.
- Playable map with `XBMPetParty` open for Horn assignment.
- Active combat encounter.

If no combat-specific XBM marker exists, use another reliable game-state signal instead of inferring Combat from `XBMContentsMainHUD`.