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

    private readonly WindowSystem windowSystem = new("SplashCrucible");
    private readonly TeamCompWindow teamCompWindow;

    // Temporary until we identify the exact internal name of the new Team Composition addon.
    // Once identified, this becomes a single constant.
    private static readonly string[] CandidateAddonNames =
    {
        "XBMContentsMainHUD",
        "XBMTeamComposition",
        "XBMTeamComp",
        "XBMParty",
        "XBMPartyEdit",
        "XBMFormation",
    };

    public Plugin()
    {
        teamCompWindow = new TeamCompWindow();
        windowSystem.AddWindow(teamCompWindow);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        Log.Information("Splash Crucible loaded.");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var teamCompositionVisible = false;

        foreach (var addonName in CandidateAddonNames)
        {
            if (GameGui.GetAddonByName(addonName) != nint.Zero)
            {
                teamCompositionVisible = true;
                break;
            }
        }

        teamCompWindow.IsOpen = teamCompositionVisible;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        teamCompWindow.Dispose();
    }
}
