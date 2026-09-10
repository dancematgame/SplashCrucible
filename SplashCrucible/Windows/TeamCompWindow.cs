using System;
using System.Collections.Generic;
using System.Linq;
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

public readonly record struct PetPartyValue(int Index, string Type, string Value);

public sealed class TeamCompWindow : Window, IDisposable
{
    public CrucibleMode CurrentMode { get; set; } = CrucibleMode.Unknown;
    public string[] ActiveXbmAddons { get; set; } = Array.Empty<string>();
    public PetPartyValue[] PetPartyValues { get; set; } = Array.Empty<PetPartyValue>();

    private PetPartyValue[] baselinePetPartyValues = Array.Empty<PetPartyValue>();

    public TeamCompWindow()
        : base("Splash Crucible##Main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 220),
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
        ImGui.TextUnformatted("Horn 1: (unresolved)");
        ImGui.TextUnformatted("Horn 2: (unresolved)");
        ImGui.TextUnformatted("Horn 3: (unresolved)");

        ImGui.Spacing();
        DrawSectionHeader("Horn assignment diagnostic");

        if (PetPartyValues.Length == 0)
        {
            ImGui.TextDisabled("Open Team Composition to inspect Horn assignment state.");
        }
        else
        {
            ImGui.TextUnformatted($"XBMPetParty AtkValues: {PetPartyValues.Length}");

            if (ImGui.Button("Capture Baseline"))
                baselinePetPartyValues = PetPartyValues.ToArray();

            ImGui.SameLine();

            if (ImGui.Button("Clear Baseline"))
                baselinePetPartyValues = Array.Empty<PetPartyValue>();

            if (baselinePetPartyValues.Length == 0)
            {
                ImGui.TextDisabled("1. Capture baseline. 2. Change one Horn assignment. 3. Read changed values below.");
            }
            else if (baselinePetPartyValues.Length != PetPartyValues.Length)
            {
                ImGui.TextUnformatted($"AtkValue count changed: {baselinePetPartyValues.Length} -> {PetPartyValues.Length}. Capture a new baseline.");
            }
            else
            {
                var changes = GetChanges(baselinePetPartyValues, PetPartyValues);
                ImGui.TextUnformatted($"Changed values: {changes.Count}");

                if (changes.Count == 0)
                {
                    ImGui.TextDisabled("(none)");
                }
                else
                {
                    foreach (var change in changes)
                        ImGui.BulletText(change);
                }
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

    private static List<string> GetChanges(PetPartyValue[] baseline, PetPartyValue[] current)
    {
        var changes = new List<string>();

        for (var i = 0; i < baseline.Length; i++)
        {
            var before = baseline[i];
            var after = current[i];

            if (before.Type == after.Type && before.Value == after.Value)
                continue;

            changes.Add($"[{i}] {before.Type}: {DisplayValue(before.Value)} -> {after.Type}: {DisplayValue(after.Value)}");
        }

        return changes;
    }

    private static string DisplayValue(string value)
        => string.IsNullOrEmpty(value) ? "(empty)" : value;

    private static void DrawSectionHeader(string text)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(text);
    }
}
