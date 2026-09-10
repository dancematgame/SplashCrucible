using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
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
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("SplashCrucible");
    private readonly TeamCompWindow teamCompWindow;

    // Temporary until the exact internal Team Composition addon name is identified.
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

        // Diagnostic: log every addon as it is created so the Team Composition
        // addon can be identified without guessing its internal name.
        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAddonPostSetup);

        Log.Information("Splash Crucible loaded. Addon-name diagnostic enabled.");
    }

    private void OnAddonPostSetup(AddonEvent type, AddonArgs args)
    {
        Log.Information("ADDON OPEN: {AddonName}", args.AddonName);
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
        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, OnAddonPostSetup);
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        teamCompWindow.Dispose();
    }
}
