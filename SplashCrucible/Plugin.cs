using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string TeamCompositionAddonName = "XBMPetParty";
    private const string BoardSelectionAddonName = "XBMStageMap";
    private const string BoardLayoutAddonName = "XBMStageDetailList";
    private const string InInstanceHudAddonName = "XBMContentsMainHUD";

    private const int PartyRowCount = 12;
    private const int PartyRowStride = 77;
    private const int FirstPartyNameIndex = 9;
    private const int FirstPartyAssignmentIndex = 80;
    private const int PartyCurrentHpOffset = 11;
    private const int PartyMaxHpOffset = 12;
    private const int FirstEnemyNameIndex = 57;
    private const int FirstEnemyWeaknessIndex = 62;

    private const byte VkNumpad6 = 0x66;
    private const uint KeyeventfKeyup = 0x0002;

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
    private uint[] cachedSquadCurrentHp = new uint[PartyRowCount];
    private uint[] cachedSquadMaxHp = new uint[PartyRowCount];
    private string cachedTopEnemyName = string.Empty;
    private string cachedTopEnemyWeakness = string.Empty;
    private bool syntheticTeamCompositionClick;
    private bool autoSummonAttemptedForCurrentBoard;
    private bool arenaEnteredForCurrentBoard;

    public Plugin()
    {
        mainWindow = new TeamCompWindow
        {
            SquadRowClicked = SelectTeamCompositionRow,
            SummonHorn1Requested = SummonHorn1,
            CommenceBattleRequested = CommenceBattle,
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

        if (args.AddonName == BoardLayoutAddonName)
        {
            autoSummonAttemptedForCurrentBoard = false;
            arenaEnteredForCurrentBoard = false;
        }

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

        if (teamPartyVisible)
            TryUpdateCurrentParty();

        if (boardLayoutVisible)
            TryUpdateTopEnemyData();

        if (!inInstanceHudVisible)
        {
            arenaEnteredForCurrentBoard = false;
        }
        else if (!boardLayoutVisible && IsCachedTopEnemyPresent())
        {
            arenaEnteredForCurrentBoard = true;
        }

        TryAutoSummonHorn1(inInstanceHudVisible, boardLayoutVisible);

        if (teamPartyVisible && !inInstanceHudVisible)
            mainWindow.CurrentMode = CrucibleMode.TeamSelection;
        else if (inInstanceHudVisible)
            mainWindow.CurrentMode = arenaEnteredForCurrentBoard ? CrucibleMode.Arena : CrucibleMode.Map;
        else if (boardSelectionVisible)
            mainWindow.CurrentMode = CrucibleMode.BoardSelection;
        else
            mainWindow.CurrentMode = CrucibleMode.Unknown;

        mainWindow.HornNames = cachedHornNames.ToArray();
        mainWindow.SquadNames = cachedSquadNames.ToArray();
        mainWindow.SquadCurrentHp = cachedSquadCurrentHp.ToArray();
        mainWindow.SquadMaxHp = cachedSquadMaxHp.ToArray();
        mainWindow.TopEnemyWeakness = cachedTopEnemyWeakness;
        mainWindow.TeamCompositionVisible = teamPartyVisible;
        mainWindow.HasActivePet = HasOwnedSquadPet();
        mainWindow.BoardLayoutVisible = boardLayoutVisible;
        mainWindow.ActiveXbmAddons = activeXbmAddons.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private bool HasOwnedSquadPet()
    {
        var player = ObjectTable.LocalPlayer;
        if (player == null)
            return false;

        var playerEntityId = player.EntityId;
        if (playerEntityId == 0)
            return false;

        var knownSquadNames = cachedSquadNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && name != "(unknown)" && name != "(unassigned)")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (knownSquadNames.Count == 0)
            return false;

        foreach (var gameObject in ObjectTable)
        {
            if (gameObject == null || gameObject.OwnerId != playerEntityId)
                continue;

            if (knownSquadNames.Contains(gameObject.Name.TextValue))
                return true;
        }

        return false;
    }

    private bool IsCachedTopEnemyPresent()
    {
        if (string.IsNullOrWhiteSpace(cachedTopEnemyName))
            return false;

        foreach (var gameObject in ObjectTable)
        {
            if (gameObject == null || !gameObject.IsTargetable)
                continue;

            if (string.Equals(gameObject.Name.TextValue, cachedTopEnemyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void TryAutoSummonHorn1(bool inInstanceHudVisible, bool boardLayoutVisible)
    {
        if (autoSummonAttemptedForCurrentBoard || !inInstanceHudVisible || boardLayoutVisible)
            return;

        if (!IsCachedTopEnemyPresent())
            return;

        autoSummonAttemptedForCurrentBoard = true;

        if (HasOwnedSquadPet())
            return;

        Log.Information("Top Board Layout enemy {EnemyName} is present with no active BST; auto-summoning Horn 1.", cachedTopEnemyName);
        SendNumpad6();
    }

    private void SummonHorn1()
    {
        if (HasOwnedSquadPet())
            return;

        SendNumpad6();
    }

    private static void SendNumpad6()
    {
        keybd_event(VkNumpad6, 0, 0, UIntPtr.Zero);
        keybd_event(VkNumpad6, 0, KeyeventfKeyup, UIntPtr.Zero);
    }

    private unsafe void CommenceBattle()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(BoardLayoutAddonName);
        if (addon == null)
            return;

        addon->AtkEventListener.ReceiveEvent(AtkEventType.ButtonClick, 9, null, null);
    }

    private unsafe void TryUpdateTopEnemyData()
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(BoardLayoutAddonName);
        if (addon == null || addon->AtkValues == null ||
            addon->AtkValuesCount <= FirstEnemyWeaknessIndex)
        {
            cachedTopEnemyName = string.Empty;
            cachedTopEnemyWeakness = string.Empty;
            return;
        }

        var nameValue = addon->AtkValues[FirstEnemyNameIndex];
        var nameType = nameValue.Type & AtkValueType.TypeMask;
        if (nameType is AtkValueType.String or AtkValueType.ConstString)
        {
            var name = nameValue.GetValueAsString();
            if (!string.IsNullOrWhiteSpace(name))
                cachedTopEnemyName = name.Trim();
        }

        var weaknessValue = addon->AtkValues[FirstEnemyWeaknessIndex];
        var weaknessType = weaknessValue.Type & AtkValueType.TypeMask;
        if (weaknessType is not (AtkValueType.String or AtkValueType.ConstString))
        {
            cachedTopEnemyWeakness = string.Empty;
            return;
        }

        var raw = weaknessValue.GetValueAsString();
        cachedTopEnemyWeakness = KnownWeaknesses.FirstOrDefault(
            weakness => raw.Contains(weakness, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
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
        var currentHp = new uint[PartyRowCount];
        var maxHp = new uint[PartyRowCount];

        for (var row = 0; row < PartyRowCount; row++)
        {
            var rowStart = row * PartyRowStride;
            var nameIndex = FirstPartyNameIndex + rowStart;
            var assignmentIndex = FirstPartyAssignmentIndex + rowStart;

            var nameValue = addon->AtkValues[nameIndex];
            var assignmentValue = addon->AtkValues[assignmentIndex];

            var name = nameValue.GetValueAsString();
            squad[row] = string.IsNullOrWhiteSpace(name) ? "(unknown)" : name;
            currentHp[row] = ReadUnsignedValue(addon->AtkValues[rowStart + PartyCurrentHpOffset]);
            maxHp[row] = ReadUnsignedValue(addon->AtkValues[rowStart + PartyMaxHpOffset]);

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
        cachedSquadCurrentHp = currentHp;
        cachedSquadMaxHp = maxHp;
    }

    private static uint ReadUnsignedValue(AtkValue value)
    {
        var type = value.Type & AtkValueType.TypeMask;
        return type switch
        {
            AtkValueType.UInt => value.UInt,
            AtkValueType.Int when value.Int >= 0 => (uint)value.Int,
            _ => 0,
        };
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

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
