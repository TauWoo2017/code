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

    // Movement
    private bool _isMovingToTree;
    private Vector2? _targetTree;
    private int _moveTimeout;

    // Chopping state
    private bool _isChopping;
    private int _chopProgress;

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
            return;

        // Handle movement to tree
        if (_isMovingToTree && _targetTree.HasValue)
        {
            MoveToTree();
            return;
        }

        // Handle chopping
        if (_isChopping)
        {
            ContinueChopping();
            return;
        }

        // Look for trees to chop
        FindAndChopTree();
    }

    private Vector2 GetPlayerTile()
    {
        return new Vector2(
            (int)(Game1.player.Position.X / 64f),
            (int)(Game1.player.Position.Y / 64f)
        );
    }

    private void FindAndChopTree()
    {
        Vector2? nearestTree = FindNearestTree();

        if (nearestTree.HasValue)
        {
            StartChopping(nearestTree.Value);
        }
        else
        {
            // No tree nearby, walk around to find one
            WalkToFindTree();
        }
    }

    private Vector2? FindNearestTree()
    {
        Vector2 playerPos = GetPlayerTile();
        double nearestDist = double.MaxValue;
        Vector2? nearest = null;

        int searchRadius = 12;

        for (int x = (int)playerPos.X - searchRadius; x <= (int)playerPos.X + searchRadius; x++)
        {
            for (int y = (int)playerPos.Y - searchRadius; y <= (int)playerPos.Y + searchRadius; y++)
            {
                Vector2 tile = new Vector2(x, y);

                if (IsTreeAt(tile))
                {
                    double dist = Math.Sqrt(Math.Pow(tile.X - playerPos.X, 2) + Math.Pow(tile.Y - playerPos.Y, 2));
                    if (dist < nearestDist && dist > 1.5)
                    {
                        nearestDist = dist;
                        nearest = tile;
                    }
                }
            }
        }

        return nearest;
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
        _targetTree = treePos;
        _isMovingToTree = true;
        _isChopping = false;
        _moveTimeout = 600;

        _monitor.Log($"Auto-chop: Found tree at {treePos}, moving to it...");
    }

    private void MoveToTree()
    {
        if (!_targetTree.HasValue)
        {
            _isMovingToTree = false;
            return;
        }

        Vector2 playerPos = GetPlayerTile();
        Vector2 target = _targetTree.Value;
        double dist = Math.Sqrt(Math.Pow(target.X - playerPos.X, 2) + Math.Pow(target.Y - playerPos.Y, 2));

        _moveTimeout--;

        // When close enough, start chopping
        if (dist <= 1.5)
        {
            _isMovingToTree = false;
            _isChopping = true;
            _chopProgress = 0;
            FaceTree(target);
            _monitor.Log("Auto-chop: In range, starting to chop...");
            return;
        }

        // Move towards tree step by step
        float dx = target.X - playerPos.X;
        float dy = target.Y - playerPos.Y;
        float length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (length > 0)
        {
            dx /= length;
            dy /= length;
        }

        FaceDirection(dx, dy);

        // Walk step by step instead of teleporting
        // Use addFarmerTrajectory to walk naturally
        Game1.player.addedSpeed = 2;
        Game1.player.setMoving(1); // Start walking

        if (_moveTimeout <= 0)
        {
            _monitor.Log("Auto-chop: Move timeout, looking for another tree...");
            _isMovingToTree = false;
            _targetTree = null;
            Game1.player.setMoving(0); // Stop walking
        }
    }

    private void FaceDirection(float dx, float dy)
    {
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            Game1.player.faceDirection(dx > 0 ? 1 : 3);
        }
        else
        {
            Game1.player.faceDirection(dy > 0 ? 2 : 0);
        }
    }

    private void FaceTree(Vector2 target)
    {
        float dx = target.X - GetPlayerTile().X;
        float dy = target.Y - GetPlayerTile().Y;
        FaceDirection(dx, dy);
    }

    private void ContinueChopping()
    {
        if (!_targetTree.HasValue)
        {
            _isChopping = false;
            return;
        }

        Vector2 playerPos = GetPlayerTile();
        Vector2 target = _targetTree.Value;
        double dist = Math.Sqrt(Math.Pow(target.X - playerPos.X, 2) + Math.Pow(target.Y - playerPos.Y, 2));

        // Check if tree still exists
        if (dist > 3 || !IsTreeAt(target))
        {
            _monitor.Log("Auto-chop: Tree chopped or too far, looking for another...");
            _isChopping = false;
            _targetTree = null;
            return;
        }

        // Face the tree and swing axe
        FaceTree(target);

        _chopProgress++;

        // Swing axe animation every 25 frames
        if (_chopProgress % 25 == 0)
        {
            Game1.player.animateOnce(48 + Game1.player.FacingDirection);
        }

        // Tree takes about 3 hits (simulated by 60 frames)
        if (_chopProgress >= 60)
        {
            // Remove the tree and spawn wood
            ChopTree(target);
            _monitor.Log("Auto-chop: Tree chopped down!");
            Game1.addHUDMessage(new HUDMessage("Auto-Chop: Tree chopped!"));
            _isChopping = false;
            _targetTree = null;
            _chopProgress = 0;
        }
    }

    private void ChopTree(Vector2 tile)
    {
        var currentLocation = Game1.currentLocation;
        if (currentLocation == null)
            return;

        // Check if tree exists
        if (!currentLocation.terrainFeatures.TryGetValue(tile, out var terrain))
            return;

        if (terrain is not StardewValley.TerrainFeatures.Tree tree)
            return;

        // Remove the tree
        currentLocation.terrainFeatures.Remove(tile);

        // Spawn wood debris (wood is item 388)
        // Trees drop 1-3 wood when chopped
        int woodCount = _random.Next(1, 4);
        for (int i = 0; i < woodCount; i++)
        {
            // Spawn wood item debris
            var wood = ItemRegistry.Create("(O)388");
            var debris = new Debris(wood, tile * 64f + new Vector2(32, 32));
            currentLocation.debris.Add(debris);
        }

        // Sometimes spawn a seed (like oak resin or maple seed)
        if (_random.NextDouble() < 0.3)
        {
            // Spawn a seed/sapling
            var seed = ItemRegistry.Create("(O)292"); // Mixed seeds
            var seedDebris = new Debris(seed, tile * 64f + new Vector2(32, 32));
            currentLocation.debris.Add(seedDebris);
        }

        _monitor.Log($"Auto-chop: Spawned {woodCount} wood debris");
    }

    private void WalkToFindTree()
    {
        if (_isPlayerMoving())
            return;

        int direction = _random.Next(4);
        int dx = 0, dy = 0;

        switch (direction)
        {
            case 0: dy = 1; break;  // Down
            case 1: dy = -1; break; // Up
            case 2: dx = 1; break;  // Right
            case 3: dx = -1; break; // Left
        }

        // Walk step by step
        FaceDirection(dx, dy);
        Game1.player.addedSpeed = 2;
        Game1.player.setMoving(1); // Start walking
    }

    private bool _isPlayerMoving()
    {
        return Math.Abs(Game1.player.xVelocity) > 0.1 || Math.Abs(Game1.player.yVelocity) > 0.1;
    }

    private void ResetState()
    {
        _isMovingToTree = false;
        _isChopping = false;
        _targetTree = null;
        _moveTimeout = 0;
        _chopProgress = 0;
    }
}
