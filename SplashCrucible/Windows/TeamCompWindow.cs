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
    public string[] HornNames { get; set; } = { "(unassigned)", "(unassigned)", "(unassigned)" };

    public TeamCompWindow()
        : base("Splash Crucible##Main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 180),
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
        DrawSectionHeader("Current Party");
        ImGui.TextUnformatted($"Horn 1: {GetHornName(0)}");
        ImGui.TextUnformatted($"Horn 2: {GetHornName(1)}");
        ImGui.TextUnformatted($"Horn 3: {GetHornName(2)}");

        ImGui.Spacing();
        DrawSectionHeader("Active XBM addons");

        if (ActiveXbmAddons.Length == 0)
        {
            ImGui.TextDisabled("(none observed)");
            return;
        }

        foreach (var addonName in ActiveXbmAddons)
            ImGui.BulletText(addonName);
    }

    private string GetHornName(int index)
        => HornNames.Length > index && !string.IsNullOrWhiteSpace(HornNames[index])
            ? HornNames[index]
            : "(unassigned)";

    private static void DrawSectionHeader(string text)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(text);
    }
}
