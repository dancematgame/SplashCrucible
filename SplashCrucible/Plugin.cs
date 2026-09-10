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
    private string[]? pendingClickSnapshot;
    private bool comparePendingClickOnNextFrame;

    public Plugin()
    {
        mainWindow = new TeamCompWindow();
        windowSystem.AddWindow(mainWindow);
        mainWindow.IsOpen = true;

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAddonPostSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnAddonPreFinalize);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, TeamCompositionAddonName, OnPetPartyReceiveEvent);

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

    private unsafe void OnPetPartyReceiveEvent(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs receiveArgs)
            return;

        uint atkEventParam = 0;
        uint nodeId = 0;
        nint target = nint.Zero;
        nint listener = nint.Zero;

        if (receiveArgs.AtkEvent != nint.Zero)
        {
            var atkEvent = (AtkEvent*)receiveArgs.AtkEvent;
            atkEventParam = atkEvent->Param;
            target = (nint)atkEvent->Target;
            listener = (nint)atkEvent->Listener;

            if (atkEvent->Node != null)
                nodeId = atkEvent->Node->NodeId;
        }

        var diagnostic = new PetPartyUiEvent(
            receiveArgs.AtkEventType.ToString(),
            receiveArgs.EventParam,
            atkEventParam,
            nodeId,
            target,
            listener,
            receiveArgs.AtkEventData);

        mainWindow.AddPetPartyUiEvent(diagnostic);

        if (string.Equals(diagnostic.EventType, "ListItemClick", StringComparison.Ordinal))
        {
            pendingClickSnapshot = CapturePetPartyAtkValues();
            comparePendingClickOnNextFrame = pendingClickSnapshot != null;
        }

        Log.Information(
            "XBMPetParty UI EVENT: Type={EventType}, EventParam={EventParam}, AtkEventParam={AtkEventParam}, NodeId={NodeId}, Target=0x{Target:X}, Listener=0x{Listener:X}, EventData=0x{EventData:X}",
            diagnostic.EventType,
            diagnostic.EventParam,
            diagnostic.AtkEventParam,
            diagnostic.NodeId,
            diagnostic.Target,
            diagnostic.Listener,
            diagnostic.EventData);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        mainWindow.IsOpen = true;

        if (comparePendingClickOnNextFrame)
        {
            comparePendingClickOnNextFrame = false;
            ComparePendingClickSnapshot();
        }

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

    private unsafe string[]? CapturePetPartyAtkValues()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(TeamCompositionAddonName);
        if (addon == null || addon->AtkValues == null || addon->AtkValuesCount == 0)
            return null;

        var values = new string[addon->AtkValuesCount];
        for (var i = 0; i < values.Length; i++)
            values[i] = FormatAtkValue(addon->AtkValues[i]);

        return values;
    }

    private void ComparePendingClickSnapshot()
    {
        var before = pendingClickSnapshot;
        pendingClickSnapshot = null;
        if (before == null)
            return;

        var after = CapturePetPartyAtkValues();
        if (after == null)
        {
            mainWindow.SetPetPartyAtkValueChanges(new[] { "Team Composition closed before comparison." });
            return;
        }

        var changes = new List<string>();
        var count = Math.Min(before.Length, after.Length);
        for (var i = 0; i < count; i++)
        {
            if (!string.Equals(before[i], after[i], StringComparison.Ordinal))
                changes.Add($"[{i}] {before[i]} -> {after[i]}");
        }

        if (before.Length != after.Length)
            changes.Add($"AtkValuesCount {before.Length} -> {after.Length}");

        mainWindow.SetPetPartyAtkValueChanges(changes.Count == 0
            ? new[] { "No AtkValue changes detected after ListItemClick." }
            : changes.ToArray());
    }

    private static string FormatAtkValue(AtkValue value)
    {
        var type = value.Type & AtkValueType.TypeMask;
        return type switch
        {
            AtkValueType.Bool => $"Bool:{value.Byte != 0}",
            AtkValueType.Int => $"Int:{value.Int}",
            AtkValueType.UInt => $"UInt:{value.UInt}",
            AtkValueType.Int64 => $"Int64:{value.Int64}",
            AtkValueType.UInt64 => $"UInt64:{value.UInt64}",
            AtkValueType.Float => $"Float:{value.Float:R}",
            AtkValueType.String or AtkValueType.WideString or AtkValueType.ConstString => $"String:{value.GetValueAsString()}",
            _ => $"Type:{value.Type}",
        };
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
