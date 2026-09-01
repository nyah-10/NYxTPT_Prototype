using UnityEngine;

public enum TileType
{
    Normal,
    Elevated,
    Obstacle,
    Trap,
    Swamp,
    DestructibleWall
}

public enum TileTriggerMode
{
    Persistent,
    Once
}

[CreateAssetMenu(fileName = "Tile_", menuName = "Hex Roguelike/Tile Data")]
public class TileData : ScriptableObject
{
    [Header("Terrain")]
    public TileType tileType = TileType.Normal;
    [Min(1)] public int moveCost = 1;
    public bool blocksLineOfSight;
    public bool blocksMovement;
    public int elevationLevel;

    [Header("Enter Effect")]
    [Tooltip("Leave empty when this terrain has no enter effect.")]
    public SkillEffect onEnterEffect;
    public TileTriggerMode triggerMode = TileTriggerMode.Persistent;

    [Header("Destructible Wall")]
    [Min(1)] public int maxHP = 1;
    public TileData destroyedTile;
}
