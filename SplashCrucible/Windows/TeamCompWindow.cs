using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SplashCrucible.Windows;

public enum CrucibleMode
{
    Unknown,
    TeamSelection,
    BoardSelection,
    InInstanceUnresolved,
}

public sealed class TeamCompWindow : Window, IDisposable
{
    public CrucibleMode CurrentMode { get; set; } = CrucibleMode.Unknown;
    public string[] ActiveXbmAddons { get; set; } = Array.Empty<string>();
    public string[] PetPartyStringValues { get; set; } = Array.Empty<string>();

    public TeamCompWindow()
        : base("Splash Crucible##Main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 160),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var modeText = CurrentMode switch
        {
            CrucibleMode.TeamSelection => "Team Selection",
            CrucibleMode.BoardSelection => "Board Selection",
            CrucibleMode.InInstanceUnresolved => "Map / Combat (unresolved)",
            _ => "Unknown / Idle",
        };

        ImGui.TextUnformatted("Splash Debug");
        ImGui.Separator();
        ImGui.TextUnformatted($"Mode: {modeText}");

        ImGui.Spacing();
        ImGui.SeparatorText("Current Party");
        ImGui.TextUnformatted("Horn 1: (unresolved)");
        ImGui.TextUnformatted("Horn 2: (unresolved)");
        ImGui.TextUnformatted("Horn 3: (unresolved)");

        ImGui.Spacing();
        ImGui.SeparatorText("XBMPetParty string values");

        if (PetPartyStringValues.Length == 0)
        {
            ImGui.TextDisabled("Open Team Composition to inspect Horn values.");
        }
        else
        {
            foreach (var value in PetPartyStringValues)
                ImGui.BulletText(value);
        }

        ImGui.Spacing();
        ImGui.SeparatorText("Active XBM addons");

        if (ActiveXbmAddons.Length == 0)
        {
            ImGui.TextDisabled("(none observed)");
            return;
        }

        foreach (var addonName in ActiveXbmAddons)
            ImGui.BulletText(addonName);
    }
}
