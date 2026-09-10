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

    private readonly WindowSystem windowSystem = new("SplashCrucible");
    private readonly TeamCompWindow teamCompWindow;

    public Plugin()
    {
        teamCompWindow = new TeamCompWindow();
        windowSystem.AddWindow(teamCompWindow);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        Log.Information("Splash Crucible loaded. Team Composition addon: {AddonName}", TeamCompositionAddonName);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        teamCompWindow.IsOpen = GameGui.GetAddonByName(TeamCompositionAddonName) != nint.Zero;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        teamCompWindow.Dispose();
    }
}
