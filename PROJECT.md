# Splash Crucible — Project State

## Purpose
Splash Crucible is a personal-use Dalamud plugin for Final Fantasy XIV's Beastmaster Crucible content. Its primary purpose is accessibility: reduce the cognitive/input-management burden of Crucible systems and present useful calculated information clearly.

## Development rules
- GitHub is the source of truth for project state and code.
- Prefer small, testable changes over speculative large implementations.
- Verify current Dalamud/FFXIVClientStructs APIs before relying on them.
- The project does not need to meet requirements for publication in the official Dalamud plugin repository.
- Preserve accessibility as the main design objective.

## Current milestone: Team Comp UI
Create an addon-powered companion window for FFXIV's Beastmaster Team Composition UI.

### First test
When the FFXIV Team Composition window is visible, Splash Crucible should display its own window containing:

    Splash Debug

When Team Composition closes, the Splash Crucible window should close/hide too.

## Current uncertainty
Beastmaster/Crucible is new content and the exact internal Atk addon name for the Team Composition UI still needs to be verified in-game. Candidate XBM addon names are temporarily checked in Plugin.cs. Once identified, replace this candidate list with the confirmed identifier.

## Next milestones
1. Confirm the Team Composition Atk addon name.
2. Make companion-window visibility reliably mirror Team Composition.
3. Inspect Team Composition data/state.
4. Display calculated information about the selected team.
5. Expand Crucible accessibility features incrementally.
