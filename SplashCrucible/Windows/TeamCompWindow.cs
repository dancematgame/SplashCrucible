using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SplashCrucible.Data;

namespace SplashCrucible.Windows;

public enum CrucibleMode
{
    Unknown,
    SelectSquad,
    Map,
    Arena,
}

public sealed class TeamCompWindow : Window, IDisposable
{
    private const string DisplayVersion = "1.1.2";

    private static readonly string[] KnownXbmAddons =
    {
        "XBMStageList",
        "XBMStageMap",
        "XBMContentsMainHUD",
        "XBMPetActionDetail",
        "XBMPetParty",
        "XBMStageDetailList",
        "XBMResult",
        "XBMMonsterBookDetail",
        "XBMMonsterNotebook",
    };

    public CrucibleMode CurrentMode { get; set; } = CrucibleMode.Unknown;
    public string[] ActiveXbmAddons { get; set; } = Array.Empty<string>();
    public string[] HornNames { get; set; } = { "(unassigned)", "(unassigned)", "(unassigned)" };
    public string[] SquadNames { get; set; } = Array.Empty<string>();
    public uint[] SquadCurrentHp { get; set; } = Array.Empty<uint>();
    public uint[] SquadMaxHp { get; set; } = Array.Empty<uint>();
    public string TopEnemyWeakness { get; set; } = string.Empty;
    public bool TeamCompositionVisible { get; set; }
    public bool HasActivePet { get; set; }
    public bool BoardLayoutVisible { get; set; }
    public Action<int>? SquadRowClicked { get; set; }
    public Action? SummonHorn1Requested { get; set; }
    public Action? CommenceBattleRequested { get; set; }

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

    public void Dispose() { }

    public override void Draw()
    {
        var modeText = CurrentMode switch
        {
            CrucibleMode.SelectSquad => "Select Squad",
            CrucibleMode.Map => "Map",
            CrucibleMode.Arena => "Arena",
            _ => "Unknown / Idle",
        };

        ImGui.TextUnformatted($"Cruic-able v{DisplayVersion}");
        ImGui.Separator();
        ImGui.TextUnformatted($"Mode: {modeText}");

        ImGui.Spacing();
        DrawSectionHeader("Party");
        DrawPartyRow(0);
        DrawPartyRow(1);
        DrawPartyRow(2);

        ImGui.Spacing();
        DrawSectionHeader("Squad");
        for (var i = 0; i < 12; i++)
            DrawSquadRow(i, GetSquadName(i));

        if (BoardLayoutVisible)
        {
            ImGui.Spacing();
            DrawCenteredButton("Commence Battle", () => CommenceBattleRequested?.Invoke());
        }

        ImGui.Spacing();
        DrawSummonButton();

        ImGui.Spacing();
        DrawDebugProbe();
    }

    private unsafe void DrawDebugProbe()
    {
        DrawSectionHeader("Debug");

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
        {
            ImGui.TextDisabled("Player position: unavailable");
        }
        else
        {
            var position = player.Position;
            ImGui.TextUnformatted($"Player position: X {position.X:F2}  Y {position.Y:F2}  Z {position.Z:F2}");
        }

        ImGui.TextUnformatted($"Active squad BST detected: {(HasActivePet ? "YES" : "NO")}");
        ImGui.TextUnformatted($"Top enemy weakness cached: {(string.IsNullOrWhiteSpace(TopEnemyWeakness) ? "(none)" : TopEnemyWeakness)}");

        ImGui.Spacing();
        ImGui.TextUnformatted("XBM addon probe (allocated / visible)");

        var addonNames = KnownXbmAddons
            .Concat(ActiveXbmAddons)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var addonName in addonNames)
        {
            var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>(addonName);
            var exists = addon != null;
            var visible = exists && addon->IsVisible;
            ImGui.BulletText($"{addonName}: {(exists ? "YES" : "no")} / {(visible ? "VISIBLE" : "hidden")}");
        }
    }

    private static void DrawCenteredButton(string label, Action onPressed)
    {
        var buttonSize = ImGui.CalcTextSize(label) + new Vector2(24f, 10f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (ImGui.GetContentRegionAvail().X - buttonSize.X) * 0.5f));
        if (ImGui.Button(label, buttonSize))
            onPressed();
    }

    private void DrawSummonButton()
    {
        if (!HasActivePet)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.90f, 0.68f, 0.10f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.00f, 0.80f, 0.18f, 1.00f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.80f, 0.55f, 0.05f, 1.00f));
        }

        var buttonSize = ImGui.CalcTextSize("Summon 1") + new Vector2(24f, 10f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (ImGui.GetContentRegionAvail().X - buttonSize.X) * 0.5f));
        var pressed = ImGui.Button("Summon 1", buttonSize);

        if (!HasActivePet)
            ImGui.PopStyleColor(3);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(HasActivePet ? "A squad BST is currently active." : "No active squad BST detected. Sends Numpad 6.");

        if (pressed && !HasActivePet)
            SummonHorn1Requested?.Invoke();
    }

    private void DrawPartyRow(int hornIndex)
    {
        var name = GetHornName(hornIndex);
        var squadIndex = Array.FindIndex(SquadNames,
            squadName => string.Equals(squadName, name, StringComparison.OrdinalIgnoreCase));
        var (currentHp, maxHp) = GetSquadHp(squadIndex);

        var startX = ImGui.GetCursorPosX();
        var rowY = ImGui.GetCursorPosY();
        var rowHeight = ImGui.GetTextLineHeight();
        var rowWidth = ImGui.GetContentRegionAvail().X;

        ImGui.InvisibleButton($"##PartyRow{hornIndex}", new Vector2(rowWidth, rowHeight));
        var clicked = ImGui.IsItemClicked();
        var afterRowY = ImGui.GetCursorPosY();

        ImGui.SetCursorPosY(rowY);
        ImGui.SetCursorPosX(startX);
        DrawMetadataRow(name, name, bold: false, currentHp, maxHp);
        ImGui.SetCursorPosY(afterRowY);

        if (clicked && TeamCompositionVisible && name != "(unassigned)" && squadIndex >= 0)
            SquadRowClicked?.Invoke(squadIndex);
    }

    private void DrawSquadRow(int index, string name)
    {
        var (currentHp, maxHp) = GetSquadHp(index);
        var startX = ImGui.GetCursorPosX();
        var rowY = ImGui.GetCursorPosY();
        var rowHeight = ImGui.GetTextLineHeight();
        var rowWidth = ImGui.GetContentRegionAvail().X;

        ImGui.InvisibleButton($"##SquadRow{index}", new Vector2(rowWidth, rowHeight));
        var clicked = ImGui.IsItemClicked();
        var afterRowY = ImGui.GetCursorPosY();

        ImGui.SetCursorPosY(rowY);
        ImGui.SetCursorPosX(startX);
        DrawMetadataRow(name, name, bold: true, currentHp, maxHp);
        ImGui.SetCursorPosY(afterRowY);

        if (clicked && TeamCompositionVisible)
            SquadRowClicked?.Invoke(index);
    }

    private void DrawMetadataRow(string firstColumn, string metadataName, bool bold, uint currentHp, uint maxHp)
    {
        var startX = ImGui.GetCursorPosX();
        DrawText(firstColumn, bold);

        if (!PetMetadata.TryGet(metadataName, out var metadata))
        {
            ImGui.SameLine(); ImGui.SetCursorPosX(startX + 170f); DrawDisabledText("●", bold);
            ImGui.SameLine(); ImGui.SetCursorPosX(startX + 195f); DrawDisabledText("?", bold);
            ImGui.SameLine(); ImGui.SetCursorPosX(startX + 235f); DrawDisabledText("—", bold);
            ImGui.SameLine(); ImGui.SetCursorPosX(startX + 365f); DrawDisabledText("—", bold);
            ImGui.SameLine(); ImGui.SetCursorPosX(startX + 485f); DrawHealthPercent(currentHp, maxHp, bold);
            return;
        }

        ImGui.SameLine(); ImGui.SetCursorPosX(startX + 170f); DrawColoredText(GetColour(metadata.Colour), "●", bold);
        ImGui.SameLine(); ImGui.SetCursorPosX(startX + 195f);
        DrawAspectIcon(metadata.Aspect, IsWeaknessMatch(metadata.Aspect));
        ImGui.SameLine(); ImGui.SetCursorPosX(startX + 235f); DrawText(DisplayOrDash(metadata.BorrowType), bold);
        ImGui.SameLine(); ImGui.SetCursorPosX(startX + 365f); DrawText(DisplayOrDash(metadata.TemperedReleaseType), bold);
        ImGui.SameLine(); ImGui.SetCursorPosX(startX + 485f); DrawHealthPercent(currentHp, maxHp, bold);
    }

    private static void DrawHealthPercent(uint currentHp, uint maxHp, bool bold)
    {
        if (maxHp == 0)
        {
            DrawDisabledText("—", bold);
            return;
        }

        var percent = Math.Clamp((int)Math.Round((double)currentHp * 100.0 / maxHp), 0, 100);
        DrawText($"{percent}%", bold);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"HP: {currentHp}/{maxHp}");
    }

    private (uint Current, uint Max) GetSquadHp(int index)
    {
        if (index < 0 || index >= SquadCurrentHp.Length || index >= SquadMaxHp.Length)
            return (0, 0);

        return (SquadCurrentHp[index], SquadMaxHp[index]);
    }

    private bool IsWeaknessMatch(string aspect)
        => !string.IsNullOrWhiteSpace(TopEnemyWeakness) &&
           string.Equals(aspect, TopEnemyWeakness, StringComparison.OrdinalIgnoreCase);

    private static void DrawAspectIcon(string aspect, bool highlighted)
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

            if (highlighted)
            {
                var min = ImGui.GetItemRectMin() - new Vector2(2f, 1f);
                var max = ImGui.GetItemRectMax() + new Vector2(2f, 1f);
                var colour = ImGui.GetColorU32(new Vector4(1.00f, 0.85f, 0.20f, 1.00f));
                ImGui.GetWindowDrawList().AddRect(min, max, colour);
            }
        }

        if (ImGui.IsItemHovered())
        {
            var suffix = highlighted ? $" — matches top enemy weakness ({aspect})" : string.Empty;
            ImGui.SetTooltip($"Auto-attack: {aspect}{suffix}");
        }
    }

    private static void DrawText(string text, bool bold)
    {
        if (!bold) { ImGui.TextUnformatted(text); return; }
        var pos = ImGui.GetCursorScreenPos();
        var colour = ImGui.GetColorU32(ImGuiCol.Text);
        ImGui.TextUnformatted(text);
        ImGui.GetWindowDrawList().AddText(new Vector2(pos.X + 0.75f, pos.Y), colour, text);
    }

    private static void DrawDisabledText(string text, bool bold)
    {
        if (!bold) { ImGui.TextDisabled(text); return; }
        var pos = ImGui.GetCursorScreenPos();
        var colour = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        ImGui.TextDisabled(text);
        ImGui.GetWindowDrawList().AddText(new Vector2(pos.X + 0.75f, pos.Y), colour, text);
    }

    private static void DrawColoredText(Vector4 colour, string text, bool bold)
    {
        if (!bold) { ImGui.TextColored(colour, text); return; }
        var pos = ImGui.GetCursorScreenPos();
        ImGui.TextColored(colour, text);
        ImGui.GetWindowDrawList().AddText(new Vector2(pos.X + 0.75f, pos.Y), ImGui.ColorConvertFloat4ToU32(colour), text);
    }

    private static Vector4 GetColour(string colour) => colour switch
    {
        "Red" => new Vector4(1.00f, 0.25f, 0.25f, 1.00f),
        "Blue" => new Vector4(0.30f, 0.55f, 1.00f, 1.00f),
        "Yellow" => new Vector4(1.00f, 0.85f, 0.20f, 1.00f),
        "Green" => new Vector4(0.30f, 0.85f, 0.35f, 1.00f),
        _ => new Vector4(0.65f, 0.65f, 0.65f, 1.00f),
    };

    private static string DisplayOrDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    private string GetHornName(int index) => HornNames.Length > index && !string.IsNullOrWhiteSpace(HornNames[index]) ? HornNames[index] : "(unassigned)";
    private string GetSquadName(int index) => SquadNames.Length > index && !string.IsNullOrWhiteSpace(SquadNames[index]) ? SquadNames[index] : "(unknown)";

    private static void DrawSectionHeader(string text)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(text);
    }
}
