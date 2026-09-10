using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SplashCrucible.Data;

namespace SplashCrucible.Windows;

public enum CrucibleMode
{
    Unknown,
    TeamSelection,
    BoardSelection,
    InInstanceUnresolved,
}

public readonly record struct PetPartyClickEvent(
    int SelectedIndex,
    int RendererIndex,
    byte MouseButtonId,
    string MouseModifier);

public sealed class TeamCompWindow : Window, IDisposable
{
    public CrucibleMode CurrentMode { get; set; } = CrucibleMode.Unknown;
    public string[] ActiveXbmAddons { get; set; } = Array.Empty<string>();
    public string[] HornNames { get; set; } = { "(unassigned)", "(unassigned)", "(unassigned)" };
    public string[] SquadNames { get; set; } = Array.Empty<string>();
    public bool TeamCompositionVisible { get; set; }

    private readonly List<PetPartyClickEvent> petPartyClickEvents = new();

    public TeamCompWindow()
        : base("Splash Crucible##Main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public void Dispose()
    {
    }

    public void AddPetPartyClickEvent(PetPartyClickEvent clickEvent)
    {
        petPartyClickEvents.Insert(0, clickEvent);
        if (petPartyClickEvents.Count > 12)
            petPartyClickEvents.RemoveRange(12, petPartyClickEvents.Count - 12);
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
        DrawSectionHeader("Current Squad");
        DrawSquadHeader();

        for (var i = 0; i < 12; i++)
            DrawSquadRow(GetSquadName(i));

        ImGui.TextDisabled("Squad-row activation is temporarily disabled while native left/right click data is measured.");

        ImGui.Spacing();
        DrawSectionHeader("Team Composition mouse diagnostic");
        ImGui.TextDisabled("Observation only. Manually click a BST in the native Team Composition window.");

        if (!TeamCompositionVisible)
            ImGui.TextDisabled("Open Team Composition before testing.");

        if (ImGui.Button("Clear Clicks"))
            petPartyClickEvents.Clear();

        ImGui.SameLine();
        ImGui.TextUnformatted($"Recorded: {petPartyClickEvents.Count}");

        if (petPartyClickEvents.Count == 0)
        {
            ImGui.TextDisabled("No native ListItemClick recorded yet.");
        }
        else
        {
            foreach (var clickEvent in petPartyClickEvents)
            {
                ImGui.BulletText(
                    $"SelectedIndex={clickEvent.SelectedIndex} | RendererIndex={clickEvent.RendererIndex} | MouseButtonId={clickEvent.MouseButtonId} | Modifier={clickEvent.MouseModifier}");
            }
        }

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

    private static void DrawSquadHeader()
    {
        var startX = ImGui.GetCursorPosX();
        ImGui.TextDisabled("Name");
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 170f);
        ImGui.TextDisabled("Colour");
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 235f);
        ImGui.TextDisabled("Borrow Type");
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 365f);
        ImGui.TextDisabled("Tempered Release Type");
    }

    private static void DrawSquadRow(string name)
    {
        var startX = ImGui.GetCursorPosX();
        ImGui.TextUnformatted(name);

        if (!PetMetadata.TryGet(name, out var metadata))
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 170f);
            ImGui.TextDisabled("●");
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 235f);
            ImGui.TextDisabled("—");
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 365f);
            ImGui.TextDisabled("—");
            return;
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 170f);
        ImGui.TextColored(GetColour(metadata.Colour), "●");

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 235f);
        ImGui.TextUnformatted(DisplayOrDash(metadata.BorrowType));

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 365f);
        ImGui.TextUnformatted(DisplayOrDash(metadata.TemperedReleaseType));
    }

    private static Vector4 GetColour(string colour)
        => colour switch
        {
            "Red" => new Vector4(1.00f, 0.25f, 0.25f, 1.00f),
            "Blue" => new Vector4(0.30f, 0.55f, 1.00f, 1.00f),
            "Yellow" => new Vector4(1.00f, 0.85f, 0.20f, 1.00f),
            "Green" => new Vector4(0.30f, 0.85f, 0.35f, 1.00f),
            _ => new Vector4(0.65f, 0.65f, 0.65f, 1.00f),
        };

    private static string DisplayOrDash(string value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private string GetHornName(int index)
        => HornNames.Length > index && !string.IsNullOrWhiteSpace(HornNames[index])
            ? HornNames[index]
            : "(unassigned)";

    private string GetSquadName(int index)
        => SquadNames.Length > index && !string.IsNullOrWhiteSpace(SquadNames[index])
            ? SquadNames[index]
            : "(unknown)";

    private static void DrawSectionHeader(string text)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(text);
    }
}
