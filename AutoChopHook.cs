using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using Microsoft.Xna.Framework;

namespace AutoFishing;

public class AutoChopHook
{
    private readonly IMonitor _monitor;
    private readonly Random _random = new();

    // Auto-chop toggle
    private bool _isEnabled;

    // Tree detection timer
    private int _treeDetectedTimer;
    private Vector2? _detectedTreePos;

    // Chopping state
    private bool _isChopping;
    private int _chopProgress;
    private Vector2? _currentTreePos;

    public AutoChopHook(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        if (!enabled)
        {
            ResetState();
        }
        _monitor.Log($"Auto-chop {(enabled ? "enabled" : "disabled")}");
    }

    public void Update()
    {
        if (!_isEnabled)
            return;

        // Must have axe equipped
        if (Game1.player.CurrentTool is not Axe)
        {
            ResetState();
            return;
        }

        // If currently chopping, continue (don't check for new trees)
        if (_isChopping)
        {
            ContinueChopping();
            return;
        }

        // Check for tree in range (adjacent tiles)
        Vector2? nearestTree = FindTreeInChopRange();

        if (nearestTree.HasValue)
        {
            // Tree detected at adjacent tile
            if (_detectedTreePos.HasValue && _detectedTreePos.Value == nearestTree.Value)
            {
                // Same tree, increase timer
                _treeDetectedTimer++;

                // After 1 second (60 frames), start chopping
                if (_treeDetectedTimer >= 60)
                {
                    StartChopping(nearestTree.Value);
                    return;
                }
            }
            else
            {
                // New tree detected, reset timer
                _detectedTreePos = nearestTree;
                _treeDetectedTimer = 0;
            }
        }
        else
        {
            // No tree in range, reset detection
            _detectedTreePos = null;
            _treeDetectedTimer = 0;
        }
    }

    private Vector2 GetPlayerTile()
    {
        return new Vector2(
            (int)(Game1.player.Position.X / 64f),
            (int)(Game1.player.Position.Y / 64f)
        );
    }

    /// <summary>
    /// Find a tree that is within chop range (adjacent tiles)
    /// </summary>
    private Vector2? FindTreeInChopRange()
    {
        Vector2 playerPos = GetPlayerTile();

        // Check all adjacent tiles (including diagonal)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                Vector2 tile = new Vector2(playerPos.X + dx, playerPos.Y + dy);

                if (IsTreeAt(tile))
                {
                    return tile;
                }
            }
        }

        return null;
    }

    private bool IsTreeAt(Vector2 tile)
    {
        var currentLocation = Game1.currentLocation;
        if (currentLocation == null)
            return false;

        if (currentLocation.terrainFeatures != null &&
            currentLocation.terrainFeatures.TryGetValue(tile, out var terrain))
        {
            if (terrain is StardewValley.TerrainFeatures.Tree tree)
            {
                return tree.growthStage.Value >= 3;
            }
        }

        return false;
    }

    private void StartChopping(Vector2 treePos)
    {
        _isChopping = true;
        _chopProgress = 0;
        _currentTreePos = treePos;
        _detectedTreePos = null;
        _treeDetectedTimer = 0;

        // Face the tree
        FaceTree(treePos);

        _monitor.Log($"Auto-chop: Started chopping tree at {treePos}");
    }

    private void FaceTree(Vector2 target)
    {
        float dx = target.X - GetPlayerTile().X;
        float dy = target.Y - GetPlayerTile().Y;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            Game1.player.faceDirection(dx > 0 ? 1 : 3);
        }
        else
        {
            Game1.player.faceDirection(dy > 0 ? 2 : 0);
        }
    }

    private void ContinueChopping()
    {
        if (!_isChopping)
            return;

        // Check if we have a tree to chop
        if (!_currentTreePos.HasValue)
        {
            _monitor.Log("Auto-chop: No tree position, stopping");
            StopChopping();
            return;
        }

        Vector2 treePos = _currentTreePos.Value;

        // Get the tree object
        var currentLocation = Game1.currentLocation;
        if (currentLocation == null || currentLocation.terrainFeatures == null)
        {
            StopChopping();
            return;
        }

        if (!currentLocation.terrainFeatures.TryGetValue(treePos, out var terrain) ||
            terrain is not StardewValley.TerrainFeatures.Tree tree)
        {
            _monitor.Log("Auto-chop: Tree no longer exists");
            Game1.addHUDMessage(new HUDMessage("Auto-Chop: Tree chopped!"));
            StopChopping();
            return;
        }

        // Face the tree
        FaceTree(treePos);

        // Swing axe every 20 frames (one swing per ~0.33 seconds at 60fps)
        if (_chopProgress % 20 == 0 && _chopProgress > 0)
        {
            // Directly reduce tree health (copper axe does 3 damage)
            tree.health.Value -= 3;
            _monitor.Log($"Auto-chop: Axe swing at tree, tree health: {tree.health.Value}");

            // Trigger tree shake animation
            tree.shake(treePos * 64f, false);

            // Trigger tool use animation
            Game1.player.UsingTool = true;
            Game1.player.animateOnce(6 + Game1.player.FacingDirection);

            // Check if tree is now dead (health <= 0)
            if (tree.health.Value <= 0)
            {
                _monitor.Log("Auto-chop: Tree chopped down!");
                Game1.addHUDMessage(new HUDMessage("Auto-Chop: Tree chopped!"));
                ChopTree(treePos);
                StopChopping();
                return;
            }
        }

        _chopProgress++;

        // Safety: after 180 frames (3 seconds of swinging), force remove tree
        if (_chopProgress >= 180)
        {
            _monitor.Log("Auto-chop: Safety timeout, forcing tree removal");
            ChopTree(treePos);
            Game1.addHUDMessage(new HUDMessage("Auto-Chop: Tree chopped!"));
            StopChopping();
        }
    }

    private void StopChopping()
    {
        _isChopping = false;
        _chopProgress = 0;
        _currentTreePos = null;
        _detectedTreePos = null;
        _treeDetectedTimer = 0;

        // Reset player state so they can move again
        Game1.player.UsingTool = false;
        Game1.player.CanMove = true;
        Game1.player.xOffset = 0;
        Game1.player.yOffset = 0;
    }

    private void ChopTree(Vector2 tile)
    {
        var currentLocation = Game1.currentLocation;
        if (currentLocation == null)
            return;

        // Check if tree exists
        if (!currentLocation.terrainFeatures.TryGetValue(tile, out var terrain))
            return;

        if (terrain is not StardewValley.TerrainFeatures.Tree)
            return;

        // Remove the tree
        currentLocation.terrainFeatures.Remove(tile);

        // Spawn wood debris
        int woodCount = _random.Next(1, 4);
        for (int i = 0; i < woodCount; i++)
        {
            var wood = ItemRegistry.Create("(O)388");
            var debris = new Debris(wood, tile * 64f + new Vector2(32, 32));
            currentLocation.debris.Add(debris);
        }

        // Sometimes spawn a seed
        if (_random.NextDouble() < 0.3)
        {
            var seed = ItemRegistry.Create("(O)292");
            var seedDebris = new Debris(seed, tile * 64f + new Vector2(32, 32));
            currentLocation.debris.Add(seedDebris);
        }

        _monitor.Log($"Auto-chop: Spawned {woodCount} wood debris");
    }

    private void ResetState()
    {
        _isChopping = false;
        _chopProgress = 0;
        _currentTreePos = null;
        _detectedTreePos = null;
        _treeDetectedTimer = 0;
    }
}
