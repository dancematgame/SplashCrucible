using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility;
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

public sealed class TeamCompWindow : Window, IDisposable
{
    public CrucibleMode CurrentMode { get; set; } = CrucibleMode.Unknown;
    public string[] ActiveXbmAddons { get; set; } = Array.Empty<string>();
    public string[] HornNames { get; set; } = { "(unassigned)", "(unassigned)", "(unassigned)" };
    public string[] SquadNames { get; set; } = Array.Empty<string>();
    public bool TeamCompositionVisible { get; set; }
    public Action<int>? SquadRowClicked { get; set; }

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
        DrawPartyRow(0);
        DrawPartyRow(1);
        DrawPartyRow(2);

        ImGui.Spacing();
        DrawSectionHeader("Current Squad");

        for (var i = 0; i < 12; i++)
            DrawSquadRow(i, GetSquadName(i));

        if (!TeamCompositionVisible)
            ImGui.TextDisabled("Open Team Composition to select a BST from Current Squad.");

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

    private void DrawPartyRow(int hornIndex)
    {
        var name = GetHornName(hornIndex);
        DrawMetadataRow(name, name, bold: false);
    }

    private void DrawSquadRow(int index, string name)
    {
        var startX = ImGui.GetCursorPosX();
        var rowY = ImGui.GetCursorPosY();
        var rowHeight = ImGui.GetTextLineHeight();
        var rowWidth = ImGui.GetContentRegionAvail().X;

        ImGui.InvisibleButton($"##SquadRow{index}", new Vector2(rowWidth, rowHeight));
        var clicked = ImGui.IsItemClicked();
        var afterRowY = ImGui.GetCursorPosY();

        ImGui.SetCursorPosY(rowY);
        ImGui.SetCursorPosX(startX);
        DrawMetadataRow(name, name, bold: true);
        ImGui.SetCursorPosY(afterRowY);

        if (clicked && TeamCompositionVisible)
            SquadRowClicked?.Invoke(index);
    }

    private static void DrawMetadataRow(string firstColumn, string metadataName, bool bold)
    {
        var startX = ImGui.GetCursorPosX();

        DrawText(firstColumn, bold);

        if (!PetMetadata.TryGet(metadataName, out var metadata))
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 170f);
            DrawDisabledText("●", bold);
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 195f);
            DrawDisabledText("?", bold);
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 235f);
            DrawDisabledText("—", bold);
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + 365f);
            DrawDisabledText("—", bold);
            return;
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 170f);
        DrawColoredText(GetColour(metadata.Colour), "●", bold);

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 195f);
        DrawAspectIcon(metadata.Aspect);

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 235f);
        DrawText(DisplayOrDash(metadata.BorrowType), bold);

        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + 365f);
        DrawText(DisplayOrDash(metadata.TemperedReleaseType), bold);
    }

    private static void DrawAspectIcon(string aspect)
    {
        var icon = aspect switch
        {
            "Fire" => BitmapFontIcon.ElementFire,
            "Ice" => BitmapFontIcon.ElementIce,
            "Wind" => BitmapFontIcon.ElementWind,
            "Earth" => BitmapFontIcon.ElementEarth,
            "Lightning" => BitmapFontIcon.ElementLightning,
            "Water" => BitmapFontIcon.ElementWater,
            "Unaspected" => BitmapFontIcon.RedStar,
            "Blunt" => BitmapFontIcon.BluntDamage,
            "Piercing" => BitmapFontIcon.PiercingDamage,
            "Slashing" => BitmapFontIcon.SlashingDamage,
            _ => BitmapFontIcon.None,
        };

        if (icon == BitmapFontIcon.None)
        {
            ImGui.TextDisabled("?");
        }
        else
        {
            ImGuiHelpers.CompileSeStringWrapped($"<icon({(int)icon})>");
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Auto-attack: {aspect}");
    }

    private static void DrawText(string text, bool bold)
    {
        if (!bold)
        {
            ImGui.TextUnformatted(text);
            return;
        }

        var pos = ImGui.GetCursorScreenPos();
        var colour = ImGui.GetColorU32(ImGuiCol.Text);
        ImGui.TextUnformatted(text);
        ImGui.GetWindowDrawList().AddText(new Vector2(pos.X + 0.75f, pos.Y), colour, text);
    }

    private static void DrawDisabledText(string text, bool bold)
    {
        if (!bold)
        {
            ImGui.TextDisabled(text);
            return;
        }

        var pos = ImGui.GetCursorScreenPos();
        var colour = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        ImGui.TextDisabled(text);
        ImGui.GetWindowDrawList().AddText(new Vector2(pos.X + 0.75f, pos.Y), colour, text);
    }

    private static void DrawColoredText(Vector4 colour, string text, bool bold)
    {
        if (!bold)
        {
            ImGui.TextColored(colour, text);
            return;
        }

        var pos = ImGui.GetCursorScreenPos();
        ImGui.TextColored(colour, text);
        ImGui.GetWindowDrawList().AddText(
            new Vector2(pos.X + 0.75f, pos.Y),
            ImGui.ColorConvertFloat4ToU32(colour),
            text);
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
