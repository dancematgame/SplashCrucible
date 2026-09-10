using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SplashCrucible.Windows;

namespace SplashCrucible;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string TeamCompositionAddonName = "XBMPetParty";
    private const string BoardSelectionAddonName = "XBMStageMap";
    private const string BoardLayoutAddonName = "XBMStageDetailList";
    private const string InInstanceHudAddonName = "XBMContentsMainHUD";

    private const int PartyRowCount = 12;
    private const int PartyRowStride = 77;
    private const int FirstPartyNameIndex = 9;
    private const int FirstPartyAssignmentIndex = 80;
    private const int FirstEnemyWeaknessIndex = 62;

    private static readonly string[] KnownWeaknesses =
    {
        "Fire", "Ice", "Wind", "Earth", "Lightning", "Water",
        "Unaspected", "Blunt", "Piercing", "Slashing",
    };

    private readonly WindowSystem windowSystem = new("SplashCrucible");
    private readonly TeamCompWindow mainWindow;
    private readonly HashSet<string> activeXbmAddons = new(StringComparer.Ordinal);
    private string[] cachedHornNames = { "(unassigned)", "(unassigned)", "(unassigned)" };
    private string[] cachedSquadNames = Enumerable.Repeat("(unknown)", PartyRowCount).ToArray();
    private string cachedTopEnemyWeakness = string.Empty;
    private bool syntheticTeamCompositionClick;

    public Plugin()
    {
        mainWindow = new TeamCompWindow
        {
            SquadRowClicked = SelectTeamCompositionRow,
        };

        windowSystem.AddWindow(mainWindow);
        mainWindow.IsOpen = true;

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAddonPostSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnAddonPreFinalize);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, TeamCompositionAddonName, OnPetPartyReceiveEvent);

        Log.Information("Splash Crucible loaded.");
    }

    private void OnAddonPostSetup(AddonEvent type, AddonArgs args)
    {
        if (!args.AddonName.StartsWith("XBM", StringComparison.Ordinal))
            return;

        activeXbmAddons.Add(args.AddonName);
        Log.Information("XBM OPEN: {AddonName}", args.AddonName);
    }

    private void OnAddonPreFinalize(AddonEvent type, AddonArgs args)
    {
        if (!args.AddonName.StartsWith("XBM", StringComparison.Ordinal))
            return;

        activeXbmAddons.Remove(args.AddonName);
        Log.Information("XBM CLOSE: {AddonName}", args.AddonName);
    }

    private unsafe void OnPetPartyReceiveEvent(AddonEvent type, AddonArgs args)
    {
        if (!syntheticTeamCompositionClick ||
            args is not AddonReceiveEventArgs receiveArgs ||
            receiveArgs.AtkEventData == nint.Zero)
            return;

        var eventType = (AtkEventType)receiveArgs.AtkEventType;
        if (eventType != AtkEventType.ListItemClick)
            return;

        var data = (AtkEventData*)receiveArgs.AtkEventData;
        data->ListItemData.MouseButtonId = 0;
        data->ListItemData.MouseModifier = default;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        mainWindow.IsOpen = true;

        var teamPartyVisible = GameGui.GetAddonByName(TeamCompositionAddonName) != nint.Zero;
        var boardSelectionVisible = GameGui.GetAddonByName(BoardSelectionAddonName) != nint.Zero;
        var boardLayoutVisible = GameGui.GetAddonByName(BoardLayoutAddonName) != nint.Zero;
        var inInstanceHudVisible = GameGui.GetAddonByName(InInstanceHudAddonName) != nint.Zero;

        if (teamPartyVisible && !inInstanceHudVisible)
            mainWindow.CurrentMode = CrucibleMode.TeamSelection;
        else if (inInstanceHudVisible)
            mainWindow.CurrentMode = CrucibleMode.InInstanceUnresolved;
        else if (boardSelectionVisible)
            mainWindow.CurrentMode = CrucibleMode.BoardSelection;
        else
            mainWindow.CurrentMode = CrucibleMode.Unknown;

        if (teamPartyVisible)
            TryUpdateCurrentParty();

        if (boardLayoutVisible)
            TryUpdateTopEnemyWeakness();

        mainWindow.HornNames = cachedHornNames.ToArray();
        mainWindow.SquadNames = cachedSquadNames.ToArray();
        mainWindow.TopEnemyWeakness = cachedTopEnemyWeakness;
        mainWindow.TeamCompositionVisible = teamPartyVisible;
        mainWindow.ActiveXbmAddons = activeXbmAddons.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        mainWindow.TeamCompositionRowDiagnostic = teamPartyVisible ? ReadTeamCompositionRowDiagnostic() : Array.Empty<string>();
    }

    private unsafe void TryUpdateTopEnemyWeakness()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(BoardLayoutAddonName);
        if (addon == null || addon->AtkValues == null || addon->AtkValuesCount <= FirstEnemyWeaknessIndex)
        {
            cachedTopEnemyWeakness = string.Empty;
            return;
        }

        var value = addon->AtkValues[FirstEnemyWeaknessIndex];
        var type = value.Type & AtkValueType.TypeMask;
        if (type is not (AtkValueType.String or AtkValueType.String8))
        {
            cachedTopEnemyWeakness = string.Empty;
            return;
        }

        var raw = value.GetValueAsString();
        cachedTopEnemyWeakness = KnownWeaknesses.FirstOrDefault(
            weakness => raw.Contains(weakness, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private unsafe string[] ReadTeamCompositionRowDiagnostic()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(TeamCompositionAddonName);
        if (addon == null || addon->AtkValues == null)
            return Array.Empty<string>();

        var row = Array.FindIndex(cachedSquadNames,
            name => string.Equals(name, "Treant", StringComparison.OrdinalIgnoreCase));
        if (row < 0)
            row = Array.FindIndex(cachedSquadNames,
                name => !string.IsNullOrWhiteSpace(name) && name != "(unknown)");
        if (row < 0)
            return Array.Empty<string>();

        var start = row * PartyRowStride;
        var end = Math.Min(start + PartyRowStride - 1, addon->AtkValuesCount - 1);
        var values = new List<string> { $"BST: {cachedSquadNames[row]} | row {row} | AtkValues {start}-{end}" };

        for (var i = start; i <= end; i++)
        {
            var value = addon->AtkValues[i];
            var type = value.Type & AtkValueType.TypeMask;
            string? text = type switch
            {
                AtkValueType.String or AtkValueType.String8 => value.GetValueAsString(),
                AtkValueType.Int when value.Int != 0 => value.Int.ToString(),
                AtkValueType.UInt when value.UInt != 0 => value.UInt.ToString(),
                AtkValueType.Bool when value.Byte != 0 => "true",
                AtkValueType.Float when Math.Abs(value.Float) > 0.0001f => value.Float.ToString("0.###"),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(text))
                values.Add($"[{i}] +{i - start} {type}: {text}");
        }

        return values.ToArray();
    }

    private unsafe void SelectTeamCompositionRow(int row)
    {
        if (row < 0 || row >= PartyRowCount)
            return;

        var addon = GameGui.GetAddonByName<AtkUnitBase>(TeamCompositionAddonName);
        if (addon == null)
            return;

        var list = FindTeamCompositionList(addon);
        if (list == null)
        {
            Log.Warning("Could not locate the 12-row XBMPetParty list component.");
            return;
        }

        syntheticTeamCompositionClick = true;
        try
        {
            list->DispatchItemEvent(row, AtkEventType.ListItemClick);
        }
        finally
        {
            syntheticTeamCompositionClick = false;
        }
    }

    private static unsafe AtkComponentList* FindTeamCompositionList(AtkUnitBase* addon)
    {
        if (addon->UldManager.NodeList == null)
            return null;

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->GetNodeType() != NodeType.Component)
                continue;

            var componentNode = (AtkComponentNode*)node;
            var component = componentNode->Component;
            if (component == null || component->GetComponentType() != ComponentType.List)
                continue;

            var list = (AtkComponentList*)component;
            if (list->ListLength == PartyRowCount)
                return list;
        }

        return null;
    }

    private unsafe void TryUpdateCurrentParty()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(TeamCompositionAddonName);
        if (addon == null || addon->AtkValues == null)
            return;

        var lastRequiredIndex = FirstPartyAssignmentIndex + ((PartyRowCount - 1) * PartyRowStride);
        if (addon->AtkValuesCount <= lastRequiredIndex)
            return;

        var horns = new[] { "(unassigned)", "(unassigned)", "(unassigned)" };
        var squad = new string[PartyRowCount];

        for (var row = 0; row < PartyRowCount; row++)
        {
            var nameIndex = FirstPartyNameIndex + (row * PartyRowStride);
            var assignmentIndex = FirstPartyAssignmentIndex + (row * PartyRowStride);

            var nameValue = addon->AtkValues[nameIndex];
            var assignmentValue = addon->AtkValues[assignmentIndex];

            var name = nameValue.GetValueAsString();
            squad[row] = string.IsNullOrWhiteSpace(name) ? "(unknown)" : name;

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var assignmentType = assignmentValue.Type & AtkValueType.TypeMask;
            var assignment = assignmentType switch
            {
                AtkValueType.UInt => assignmentValue.UInt,
                AtkValueType.Int when assignmentValue.Int >= 0 => (uint)assignmentValue.Int,
                _ => uint.MaxValue,
            };

            if (assignment <= 2)
                horns[assignment] = name;
        }

        cachedHornNames = horns;
        cachedSquadNames = squad;
    }

    public void Dispose()
    {
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, OnAddonPostSetup);
        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, OnAddonPreFinalize);
        AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, TeamCompositionAddonName, OnPetPartyReceiveEvent);
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
    }
}
