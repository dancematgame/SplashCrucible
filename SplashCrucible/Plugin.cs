using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SplashCrucible.Windows;

namespace SplashCrucible;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string TeamCompositionAddonName = "XBMPetParty";
    private const string MapAddonName = "XBMStageMap";
    private const string CombatHudAddonName = "XBMContentsMainHUD";

    private readonly WindowSystem windowSystem = new("SplashCrucible");
    private readonly TeamCompWindow mainWindow;

    public Plugin()
    {
        mainWindow = new TeamCompWindow();
        windowSystem.AddWindow(mainWindow);

        mainWindow.IsOpen = true;

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        Log.Information("Splash Crucible loaded.");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // This is the persistent main window. Closing it is not part of normal operation.
        mainWindow.IsOpen = true;

        // More specific in-instance states take priority over the shared Team Composition window.
        if (GameGui.GetAddonByName(CombatHudAddonName) != nint.Zero)
        {
            mainWindow.CurrentMode = CrucibleMode.Combat;
        }
        else if (GameGui.GetAddonByName(MapAddonName) != nint.Zero)
        {
            mainWindow.CurrentMode = CrucibleMode.Map;
        }
        else if (GameGui.GetAddonByName(TeamCompositionAddonName) != nint.Zero)
        {
            mainWindow.CurrentMode = CrucibleMode.TeamSelection;
        }
        else
        {
            mainWindow.CurrentMode = CrucibleMode.Unknown;
        }
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
    }
}
