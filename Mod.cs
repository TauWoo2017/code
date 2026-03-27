using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AutoFishing;

public class Mod : StardewModdingAPI.Mod
{
    private AutoFishHook? _autoFishHook;
    private bool _autoFishingEnabled;

    public override void Entry(IModHelper helper)
    {
        _autoFishHook = new AutoFishHook(this.Monitor);

        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        _autoFishHook?.Update();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.F)
        {
            _autoFishingEnabled = !_autoFishingEnabled;
            _autoFishHook?.SetEnabled(_autoFishingEnabled);

            Game1.addHUDMessage(new HUDMessage(_autoFishingEnabled ? "Auto-Fishing: ON" : "Auto-Fishing: OFF"));
        }
    }
}
