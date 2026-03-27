using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace AutoFishing;

public class AutoFishHook
{
    private readonly IMonitor _monitor;
    private readonly Random _random = new();

    // Auto-fishing toggle
    private bool _isEnabled;

    // Fishing states
    private bool _isWaitingForBite;
    private bool _isFishingMinigame;
    private bool _isFishingActive;
    private int _fishDifficulty;
    private float _fishPos;
    private float _fishSpeed;
    private float _bobberPos;
    private float _catchProgress;

    // Timing
    private int _updateCounter;
    private int _actionCooldown;

    public AutoFishHook(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _monitor.Log($"Auto-fishing {(enabled ? "enabled" : "disabled")}");
    }

    public void Update()
    {
        if (!_isEnabled)
            return;

        if (_actionCooldown > 0)
            _actionCooldown--;

        if (!Game1.player.CanMove || !IsPlayerFishing())
        {
            if (_isFishingActive)
            {
                EndFishing();
            }
            return;
        }

        var rod = GetFishingRod();
        if (rod == null)
            return;

        UpdateFishingState(rod);

        if (_isWaitingForBite && IsFishBiting(rod))
        {
            _isWaitingForBite = false;
            _isFishingMinigame = true;
            InitializeMinigame(rod);
        }

        if (_isFishingMinigame)
        {
            PlayFishingMinigame();
        }
    }

    private bool IsPlayerFishing()
    {
        var tool = Game1.player.CurrentTool;
        return tool is FishingRod;
    }

    private FishingRod? GetFishingRod()
    {
        return Game1.player.CurrentTool as FishingRod;
    }

    private void UpdateFishingState(FishingRod rod)
    {
        if (!_isFishingActive && rod.isCasting)
        {
            _isFishingActive = true;
            _isWaitingForBite = true;
            _monitor.Log("Auto-fish: Started fishing...");
        }
    }

    private bool IsFishBiting(FishingRod rod)
    {
        return rod.isNibbling;
    }

    private void InitializeMinigame(FishingRod rod)
    {
        int fishingLevel = Game1.player.FishingLevel;
        _fishDifficulty = Math.Max(5, 50 - fishingLevel * 3);
        _fishPos = 0.5f;
        _fishSpeed = 0.01f + (_fishDifficulty * 0.005f);
        _bobberPos = 0.9f;
        _catchProgress = 0f;

        _monitor.Log("Auto-fish: Fish on the line! Auto-catching...");
    }

    private void PlayFishingMinigame()
    {
        _updateCounter++;

        if (_updateCounter % 3 == 0)
        {
            _fishPos += (float)(_fishSpeed * Math.Sin(_updateCounter * 0.1));
            _fishPos += (_random.NextDouble() < 0.1 ? (float)(_random.NextDouble() - 0.5) * 0.05f : 0);
            _fishPos = Math.Clamp(_fishPos, 0.1f, 0.9f);
        }

        _bobberPos -= 0.002f;
        _bobberPos = Math.Max(_bobberPos, 0.05f);

        if (_fishPos < _bobberPos - 0.15f)
        {
            _bobberPos -= 0.08f;
            _actionCooldown = 5;
        }

        float distance = Math.Abs(_fishPos - _bobberPos);
        float catchZone = 0.12f;

        if (distance < catchZone)
        {
            _catchProgress += 0.02f;
        }
        else
        {
            _catchProgress -= 0.01f;
            _catchProgress = Math.Max(_catchProgress, 0f);
        }

        if (_catchProgress >= 1f)
        {
            CompleteCatch();
        }

        if (_catchProgress <= 0f && _updateCounter > 200)
        {
            FailCatch();
        }
    }

    private void CompleteCatch()
    {
        _monitor.Log("Auto-fish: Caught!");
        Game1.addHUDMessage(new HUDMessage("Auto-Fish: Caught!"));
        EndFishing();
    }

    private void FailCatch()
    {
        _monitor.Log("Auto-fish: Fish escaped.");
        Game1.addHUDMessage(new HUDMessage("Auto-Fish: Fish escaped!"));
        EndFishing();
    }

    private void EndFishing()
    {
        _isFishingMinigame = false;
        _isWaitingForBite = false;
        _isFishingActive = false;
        ResetState();
    }

    private void ResetState()
    {
        _fishPos = 0.5f;
        _fishSpeed = 0;
        _bobberPos = 0.9f;
        _catchProgress = 0f;
        _updateCounter = 0;
    }

    public void OnButtonPressed(SButton button)
    {
        // F key is handled in Mod.cs
    }
}
