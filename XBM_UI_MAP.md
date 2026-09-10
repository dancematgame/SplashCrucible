# XBM UI Map

This file records observed XBM addon names and their visible UI purpose. Treat entries marked with `?` as provisional until confirmed in-game.

| XBM addon | Visible UI / interpretation | Confidence |
|---|---|---|
| `XBMStageList` | Board Selection List | High |
| `XBMStageMap` | Board Selection Graphic | High |
| `XBMContentsMainHUD` | Broad in-Crucible HUD marker | High for in-duty presence; exact visible sub-panel still provisional |
| `XBMPetActionDetail` | Bottom Left Popup | High |
| `XBMPetParty` | Team Composition Box | High |
| `XBMStageDetailList` | Board Layout Window | High |
| `XBMResult` | Results Panel | High |
| `XBMMonsterBookDetail` | Master's Bestiary Right Page | High |
| `XBMMonsterNotebook` | Master's Bestiary Left Page | High |

## Current top-level state findings

- **Select Squad**: `XBMPetParty` is visible while `XBMContentsMainHUD` is absent. This is the outside-duty squad-selection context.
- **Map**: `XBMContentsMainHUD` is present and the cached Board Layout top enemy has not been observed for the current board.
- **Arena**: the cached Board Layout top enemy appears as a targetable local object. Arena is latched so enemy death/despawn does not falsely return the state to Map mid-resolution.
- `XBMResult` is the confirmed Results panel. After Arena has been entered and Results has been seen, its disappearance while `XBMContentsMainHUD` remains present is now used as the provisional earliest Arena -> Map return signal.
- Opening a new `XBMStageDetailList` also resets the Arena latch for the next board.
- `XBMPetParty` appears both outside the duty and on the playable map, so it cannot distinguish those contexts by itself.
- `XBMStageMap` is the board/challenge selection graphic, not the playable map.

## Current validation goal

Confirm that the `XBMResult` visible -> closed transition occurs at the physical return to the playable Map, before Team Composition is reopened. If it is still late or early, capture the active XBM addon changes around the arena-exit transition and use a more precise observed marker.
