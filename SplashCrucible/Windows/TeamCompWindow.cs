using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SplashCrucible.Windows;

public sealed class TeamCompWindow : Window, IDisposable
{
    public TeamCompWindow()
        : base("Splash Crucible##TeamComp")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        IsOpen = false;
        RespectCloseHotkey = false;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Splash Debug");
    }
}
