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
    private const string InInstanceHudAddonName = "XBMContentsMainHUD";

    private const int PartyRowCount = 12;
    private const int PartyRowStride = 77;
    private const int FirstPartyNameIndex = 9;
    private const int FirstPartyAssignmentIndex = 80;

    private readonly WindowSystem windowSystem = new("SplashCrucible");
    private readonly TeamCompWindow mainWindow;
    private readonly HashSet<string> activeXbmAddons = new(StringComparer.Ordinal);
    private string[] cachedHornNames = { "(unassigned)", "(unassigned)", "(unassigned)" };
    private string[] cachedSquadNames = Enumerable.Repeat("(unknown)", PartyRowCount).ToArray();

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

        Log.Information("Splash Crucible loaded. XBM state diagnostic enabled.");
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

    private void OnFrameworkUpdate(IFramework framework)
    {
        mainWindow.IsOpen = true;

        var teamPartyVisible = GameGui.GetAddonByName(TeamCompositionAddonName) != nint.Zero;
        var boardSelectionVisible = GameGui.GetAddonByName(BoardSelectionAddonName) != nint.Zero;
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

        mainWindow.HornNames = cachedHornNames.ToArray();
        mainWindow.SquadNames = cachedSquadNames.ToArray();
        mainWindow.TeamCompositionVisible = teamPartyVisible;
        mainWindow.ActiveXbmAddons = activeXbmAddons.OrderBy(x => x, StringComparer.Ordinal).ToArray();
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

        // SelectItem(..., true) only changed the list's selection state and did not
        // reproduce the native row-click behavior in XBMPetParty. We observed that
        // real row activation arrives as AtkEventType.ListItemClick with the row index
        // in the list-item event data, so dispatch that exact local list event instead.
        // This remains entirely inside the native Team Composition UI path.
        list->DispatchItemEvent(row, AtkEventType.ListItemClick);
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
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
    }
}
