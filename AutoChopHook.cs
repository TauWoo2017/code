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
    private int _chopCount;
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

        // Check terrain features (Trees)
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
        _chopCount = 0;

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

        if (dist <= 1.5)
        {
            _isMovingToTree = false;
            _isChopping = true;
            _chopProgress = 0;
            FaceTree(target);
            _monitor.Log("Auto-chop: In range, starting to chop...");
            return;
        }

        if (!_isPlayerMoving())
        {
            float dx = target.X - playerPos.X;
            float dy = target.Y - playerPos.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length > 0)
            {
                dx /= length;
                dy /= length;
            }

            FaceDirection(dx, dy);
            Game1.player.setTrajectory((int)(dx * 300), (int)(dy * 300));
        }

        if (_moveTimeout <= 0)
        {
            _monitor.Log("Auto-chop: Move timeout, looking for another tree...");
            _isMovingToTree = false;
            _targetTree = null;
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

        if (dist > 3 || !IsTreeAt(target))
        {
            _monitor.Log("Auto-chop: Tree gone or too far, looking for another...");
            _isChopping = false;
            _targetTree = null;
            return;
        }

        FaceTree(target);

        _chopProgress++;

        // Swing axe animation
        if (_chopProgress % 20 == 0)
        {
            Game1.player.animateOnce(48 + Game1.player.FacingDirection);
        }

        // Tree takes 3 hits
        if (_chopProgress >= 60)
        {
            // Remove the tree
            RemoveTree(target);
            _monitor.Log("Auto-chop: Tree chopped down!");
            Game1.addHUDMessage(new HUDMessage("Auto-Chop: Tree chopped!"));
            _isChopping = false;
            _targetTree = null;
            _chopProgress = 0;
        }
    }

    private void RemoveTree(Vector2 tile)
    {
        var currentLocation = Game1.currentLocation;
        if (currentLocation == null)
            return;

        if (currentLocation.terrainFeatures != null &&
            currentLocation.terrainFeatures.TryGetValue(tile, out var terrain))
        {
            if (terrain is StardewValley.TerrainFeatures.Tree)
            {
                currentLocation.terrainFeatures.Remove(tile);
            }
        }
    }

    private void WalkToFindTree()
    {
        if (_isPlayerMoving())
            return;

        int direction = _random.Next(4);
        int dx = 0, dy = 0;

        switch (direction)
        {
            case 0: dy = 1; break;
            case 1: dy = -1; break;
            case 2: dx = 1; break;
            case 3: dx = -1; break;
        }

        Game1.player.setTrajectory(dx * 200, dy * 200);
        Game1.player.faceDirection(direction);
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
        _chopCount = 0;
        _chopProgress = 0;
    }
}
