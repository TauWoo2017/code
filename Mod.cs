using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AutoFishing;

public class Mod : StardewModdingAPI.Mod
{
    private AutoFishHook? _autoFishHook;
    private AutoChopHook? _autoChopHook;
    private bool _autoFishingEnabled;
    private bool _autoChopEnabled;

    public override void Entry(IModHelper helper)
    {
        _autoFishHook = new AutoFishHook(this.Monitor);
        _autoChopHook = new AutoChopHook(this.Monitor);

        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        _autoFishHook?.Update();
        _autoChopHook?.Update();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.F)
        {
            _autoFishingEnabled = !_autoFishingEnabled;
            _autoFishHook?.SetEnabled(_autoFishingEnabled);

            Game1.addHUDMessage(new HUDMessage(_autoFishingEnabled ? "Auto-Fishing: ON" : "Auto-Fishing: OFF"));
        }
        else if (e.Button == SButton.Z)
        {
            _autoChopEnabled = !_autoChopEnabled;
            _autoChopHook?.SetEnabled(_autoChopEnabled);

            Game1.addHUDMessage(new HUDMessage(_autoChopEnabled ? "Auto-Chop: ON" : "Auto-Chop: OFF"));
        }
    }
}
