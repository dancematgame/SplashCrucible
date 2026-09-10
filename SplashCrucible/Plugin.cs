using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
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
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/scs";
    private const string TeamCompositionAddonName = "XBMPetParty";
    private const string BoardLayoutAddonName = "XBMStageDetailList";
    private const string InInstanceHudAddonName = "XBMContentsMainHUD";
    private const string ResultAddonName = "XBMResult";

    private const int PartyRowCount = 12;
    private const int PartyRowStride = 77;
    private const int FirstPartyNameIndex = 9;
    private const int FirstPartyAssignmentIndex = 80;
    private const int PartyCurrentHpOffset = 11;
    private const int PartyMaxHpOffset = 12;
    private const int FirstEnemyNameIndex = 57;
    private const int FirstEnemyWeaknessIndex = 62;
    private const uint CommenceBattleEventParam = 9;

    private const byte VkNumpad6 = 0x66;
    private const uint KeyeventfKeyup = 0x0002;
    private static readonly TimeSpan ArenaAutoSummonDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan ArenaAutoSummonRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ArenaAutoSummonWindow = TimeSpan.FromSeconds(5);

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
    private bool arenaEnteredForCurrentBoard;
    private bool autoSummonCompletedForCurrentBoard;
    private DateTime? pendingArenaAutoSummonAt;
    private DateTime? arenaAutoSummonDeadline;
    private bool resultWasVisible;
    private bool resultSeenForCurrentArena;
    private bool boardLayoutWasVisible;

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

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle Splash's Crucible Solver window.",
        });

        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAddonPostSetup);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnAddonPreFinalize);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, TeamCompositionAddonName, OnPetPartyReceiveEvent);

        Log.Information("Splash Crucible loaded.");
    }

    private void OnCommand(string command, string arguments)
    {
        mainWindow.IsOpen = !mainWindow.IsOpen;
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

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        // Several XBM addons persist after being hidden, so pointer existence alone is not a
        // reliable "window is open" test. Use the native visibility state for actual UI windows.
        var teamPartyVisible = IsAddonVisible(TeamCompositionAddonName);
        var boardLayoutVisible = IsAddonVisible(BoardLayoutAddonName);
        var resultVisible = IsAddonVisible(ResultAddonName);

        // XBMContentsMainHUD has already proven reliable as the broad in-duty marker in testing.
        var inInstanceHudVisible = GameGui.GetAddonByName(InInstanceHudAddonName) != nint.Zero;

        // XBMStageDetailList can be reused rather than set up from scratch for every board.
        // Reset encounter state on the visible edge, not merely Addon PostSetup.
        if (boardLayoutVisible && !boardLayoutWasVisible)
        {
            ResetForNewBoardLayout();
            Log.Information("Board Layout became visible; reset Arena and auto-summon state for the new board.");
        }
        boardLayoutWasVisible = boardLayoutVisible;

        if (teamPartyVisible)
            TryUpdateCurrentParty();

        if (boardLayoutVisible)
            TryUpdateTopEnemyData();

        if (!inInstanceHudVisible)
        {
            arenaEnteredForCurrentBoard = false;
            pendingArenaAutoSummonAt = null;
            arenaAutoSummonDeadline = null;
            resultSeenForCurrentArena = false;
        }
        else
        {
            if (!boardLayoutVisible && !arenaEnteredForCurrentBoard && IsCachedTopEnemyPresent())
            {
                arenaEnteredForCurrentBoard = true;
                QueueArenaAutoSummon();
                Log.Information("Arena detected from cached enemy {EnemyName}.", cachedTopEnemyName);
            }

            if (arenaEnteredForCurrentBoard && resultVisible)
                resultSeenForCurrentArena = true;

            // Preferred Arena -> Map signal: the real Results window was visible and then closed.
            if (arenaEnteredForCurrentBoard && resultSeenForCurrentArena && resultWasVisible && !resultVisible)
            {
                ReturnToMap("Results panel closed");
            }
            // Runtime-proven fallback: opening Team Composition on the duty map means we are no
            // longer in the arena. This is later than Results, but prevents a permanently latched
            // Arena state if a particular encounter does not expose the expected Results transition.
            else if (arenaEnteredForCurrentBoard && teamPartyVisible)
            {
                ReturnToMap("Team Composition opened on duty map");
            }
        }

        resultWasVisible = resultVisible;
        TryRunPendingArenaAutoSummon(inInstanceHudVisible);

        if (teamPartyVisible && !inInstanceHudVisible)
            mainWindow.CurrentMode = CrucibleMode.SelectSquad;
        else if (inInstanceHudVisible)
            mainWindow.CurrentMode = arenaEnteredForCurrentBoard ? CrucibleMode.Arena : CrucibleMode.Map;
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

    private unsafe bool IsAddonVisible(string addonName)
    {
        var addon = GameGui.GetAddonByName<AtkUnitBase>(addonName);
        return addon != null && addon->IsVisible;
    }

    private void ResetForNewBoardLayout()
    {
        arenaEnteredForCurrentBoard = false;
        autoSummonCompletedForCurrentBoard = false;
        pendingArenaAutoSummonAt = null;
        arenaAutoSummonDeadline = null;
        resultSeenForCurrentArena = false;
        resultWasVisible = false;
    }

    private void ReturnToMap(string reason)
    {
        arenaEnteredForCurrentBoard = false;
        pendingArenaAutoSummonAt = null;
        arenaAutoSummonDeadline = null;
        autoSummonCompletedForCurrentBoard = true;
        resultSeenForCurrentArena = false;
        cachedTopEnemyName = string.Empty;
        cachedTopEnemyWeakness = string.Empty;
        Log.Information("{Reason}; returning state to Map.", reason);
    }

    private void QueueArenaAutoSummon()
    {
        if (autoSummonCompletedForCurrentBoard)
            return;

        var now = DateTime.UtcNow;
        pendingArenaAutoSummonAt = now + ArenaAutoSummonDelay;
        arenaAutoSummonDeadline = now + ArenaAutoSummonWindow;
        Log.Information("Horn 1 auto-summon check queued for Arena entry.");
    }

    private void TryRunPendingArenaAutoSummon(bool inInstanceHudVisible)
    {
        if (pendingArenaAutoSummonAt is null || DateTime.UtcNow < pendingArenaAutoSummonAt.Value)
            return;

        if (!inInstanceHudVisible || !arenaEnteredForCurrentBoard)
        {
            pendingArenaAutoSummonAt = null;
            arenaAutoSummonDeadline = null;
            return;
        }

        if (HasOwnedSquadPet())
        {
            if (arenaAutoSummonDeadline is not null && DateTime.UtcNow < arenaAutoSummonDeadline.Value)
            {
                pendingArenaAutoSummonAt = DateTime.UtcNow + ArenaAutoSummonRetryDelay;
                return;
            }

            pendingArenaAutoSummonAt = null;
            arenaAutoSummonDeadline = null;
            autoSummonCompletedForCurrentBoard = true;
            Log.Information("Arena Horn 1 auto-summon skipped because an active squad BST remained present.");
            return;
        }

        pendingArenaAutoSummonAt = null;
        arenaAutoSummonDeadline = null;
        autoSummonCompletedForCurrentBoard = true;
        Log.Information("Arena entered with no active squad BST; auto-summoning Horn 1.");
        SendNumpad6();
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

        var nativeEvent = FindCommenceBattleEvent(addon);
        if (nativeEvent == null)
        {
            Log.Warning("Could not locate the native Commence Battle ButtonClick event on XBMStageDetailList.");
            return;
        }

        Log.Information("Dispatching native Commence Battle button event from XBMStageDetailList.");
        addon->ReceiveEvent(nativeEvent->State.EventType, (int)nativeEvent->Param, nativeEvent);
    }

    private static unsafe AtkEvent* FindCommenceBattleEvent(AtkUnitBase* addon)
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
            if (component == null || component->GetComponentType() != ComponentType.Button)
                continue;

            var ownerNode = component->OwnerNode;
            if (ownerNode == null)
                continue;

            var nativeEvent = (AtkEvent*)ownerNode->AtkResNode.AtkEventManager.Event;
            while (nativeEvent != null)
            {
                if (nativeEvent->State.EventType == AtkEventType.ButtonClick &&
                    nativeEvent->Param == CommenceBattleEventParam)
                    return nativeEvent;

                nativeEvent = nativeEvent->NextEvent;
            }
        }

        return null;
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
        CommandManager.RemoveHandler(CommandName);
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
