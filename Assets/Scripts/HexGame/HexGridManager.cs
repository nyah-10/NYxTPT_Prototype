
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TerrainTilePlacement
{
    public Vector2Int coordinate;
    public TileData tileData;
}

public class HexGridManager : MonoBehaviour
{
    [Header("Map Size")]
    [Min(1)] public int width = 8;
    [Min(1)] public int height = 6;

    [Header("Tile")]
    public GameObject hexTilePrefab;
    [Min(0.01f)] public float hexRadius = 0.5f;
    public Transform tileParent;
    [Tooltip("Per-coordinate terrain overrides. Unlisted coordinates use Default Tile Data.")]
    public TileData defaultTileData;
    public List<TerrainTilePlacement> terrainPlacements = new();

    private readonly Dictionary<Vector2Int, HexTile> tiles = new();
    private readonly Dictionary<Vector2Int, Color> highlightColors = new();
    private Vector2Int? selectedHighlight;

    private void Awake()
    {
        RoomTemplate selectedRoom = RunMapGenerator.ConsumePendingRoom();
        if (selectedRoom != null) ApplyRoomTemplate(selectedRoom);
        else
        {
            // Combat grids only exist for a selected run node; remove the obsolete baked 8x6 board.
            ClearGrid();
            Debug.LogError("No RoomTemplate was selected. Start the game from the Main scene.");
        }
    }

    public void ApplyRoomTemplate(RoomTemplate room)
    {
        if (room == null) return;
        width = room.GridSize.x;
        height = room.GridSize.y;
        terrainPlacements.Clear();
        foreach (RoomTile tile in room.TileLayout)
            terrainPlacements.Add(new TerrainTilePlacement { coordinate = tile.coordinate, tileData = tile.tileData });
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        ClearGrid();

        if (hexTilePrefab == null)
        {
            Debug.LogError("Hex Tile Prefab이 지정되지 않았습니다.");
            return;
        }

        Transform parent = tileParent != null ? tileParent : transform;

        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector2Int coordinate = new Vector2Int(q, r);

                GameObject tileObject = Instantiate(
                    hexTilePrefab,
                    AxialToWorld(coordinate),
                    Quaternion.identity,
                    parent
                );

                tileObject.name = $"Hex_{q}_{r}";

                HexTile tile = tileObject.GetComponent<HexTile>();

                if (tile == null)
                    tile = tileObject.AddComponent<HexTile>();

                tile.Initialize(coordinate, GetConfiguredTileData(coordinate));
                SetTileColor(tileObject);
                tiles[coordinate] = tile;
            }
        }
    }

    // Pointy-top axial 좌표를 월드 좌표로 바꿉니다.
    public Vector3 AxialToWorld(Vector2Int coordinate)
    {
        float x = hexRadius * Mathf.Sqrt(3f) *
                  (coordinate.x + coordinate.y * 0.5f);

        float y = hexRadius * 1.5f * coordinate.y;

        return new Vector3(x, y, 0f);
    }

    public bool TryGetTile(Vector2Int coordinate, out HexTile tile)
    {
        return tiles.TryGetValue(coordinate, out tile);
    }

    private TileData GetConfiguredTileData(Vector2Int coordinate)
    {
        foreach (TerrainTilePlacement placement in terrainPlacements)
            if (placement != null && placement.coordinate == coordinate) return placement.tileData;
        return defaultTileData;
    }

    public List<Vector2Int> GetReachableCoordinates(Vector2Int start, int movementBudget)
    {
        Dictionary<Vector2Int, int> costs = new() { [start] = 0 };
        List<Vector2Int> frontier = new() { start };
        while (frontier.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < frontier.Count; i++)
                if (costs[frontier[i]] < costs[frontier[bestIndex]]) bestIndex = i;
            Vector2Int current = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            foreach (Vector2Int next in GetNeighbors(current))
            {
                if (!tiles.TryGetValue(next, out HexTile tile) || tile.BlocksMovement) continue;
                int newCost = costs[current] + tile.MoveCost;
                if (newCost > movementBudget || costs.TryGetValue(next, out int oldCost) && oldCost <= newCost) continue;
                costs[next] = newCost;
                frontier.Add(next);
            }
        }
        return new List<Vector2Int>(costs.Keys);
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, int movementBudget = int.MaxValue)
    {
        Dictionary<Vector2Int, int> costs = new() { [start] = 0 };
        Dictionary<Vector2Int, Vector2Int> previous = new();
        List<Vector2Int> frontier = new() { start };
        while (frontier.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < frontier.Count; i++) if (costs[frontier[i]] < costs[frontier[bestIndex]]) bestIndex = i;
            Vector2Int current = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            if (current == goal) break;
            foreach (Vector2Int next in GetNeighbors(current))
            {
                if (!tiles.TryGetValue(next, out HexTile tile) || tile.BlocksMovement) continue;
                int newCost = costs[current] + tile.MoveCost;
                if (newCost > movementBudget || costs.TryGetValue(next, out int old) && old <= newCost) continue;
                costs[next] = newCost;
                previous[next] = current;
                frontier.Add(next);
            }
        }
        if (goal != start && !previous.ContainsKey(goal)) return new List<Vector2Int>();
        List<Vector2Int> path = new();
        for (Vector2Int step = goal; step != start; step = previous[step]) path.Add(step);
        path.Reverse();
        return path;
    }

    public bool CanTarget(Vector2Int source, Vector2Int target, int baseRange)
    {
        if (!tiles.TryGetValue(source, out HexTile sourceTile) || !tiles.TryGetValue(target, out HexTile targetTile)) return false;
        int elevationBonus = sourceTile.ElevationLevel > targetTile.ElevationLevel ? 1 : 0;
        return HexDistance(source, target) <= baseRange + elevationBonus && HasLineOfSight(source, target);
    }

    public bool HasLineOfSight(Vector2Int source, Vector2Int target)
    {
        int distance = HexDistance(source, target);
        for (int i = 1; i < distance; i++)
        {
            float t = i / (float)distance;
            Vector3 cube = Vector3.Lerp(AxialToCube(source), AxialToCube(target), t);
            Vector2Int coordinate = CubeToAxial(CubeRound(cube));
            if (tiles.TryGetValue(coordinate, out HexTile tile) && tile.BlocksLineOfSight) return false;
        }
        return true;
    }

    private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int coordinate)
    {
        yield return coordinate + new Vector2Int(0, -1); yield return coordinate + new Vector2Int(1, -1);
        yield return coordinate + new Vector2Int(1, 0); yield return coordinate + new Vector2Int(0, 1);
        yield return coordinate + new Vector2Int(-1, 1); yield return coordinate + new Vector2Int(-1, 0);
    }

    private static Vector3 AxialToCube(Vector2Int axial) => new(axial.x, -axial.x - axial.y, axial.y);
    private static Vector2Int CubeToAxial(Vector3 cube) => new(Mathf.RoundToInt(cube.x), Mathf.RoundToInt(cube.z));
    private static Vector3 CubeRound(Vector3 cube)
    {
        int x = Mathf.RoundToInt(cube.x), y = Mathf.RoundToInt(cube.y), z = Mathf.RoundToInt(cube.z);
        float dx = Mathf.Abs(x - cube.x), dy = Mathf.Abs(y - cube.y), dz = Mathf.Abs(z - cube.z);
        if (dx > dy && dx > dz) x = -y - z; else if (dy > dz) y = -x - z; else z = -x - y;
        return new Vector3(x, y, z);
    }

    public List<Vector2Int> GetCoordinatesInRange(Vector2Int center, int range)
    {
        List<Vector2Int> result = new();
        foreach (Vector2Int coordinate in tiles.Keys)
        {
            if (HexDistance(center, coordinate) <= range)
                result.Add(coordinate);
        }
        return result;
    }

    public void SetHighlights(IEnumerable<Vector2Int> coordinates, Color color)
    {
        ClearHighlights();
        foreach (Vector2Int coordinate in coordinates)
        {
            if (!tiles.TryGetValue(coordinate, out HexTile tile)) continue;
            highlightColors[coordinate] = color;
            tile.SetHighlight(color);
        }
    }

    public void SetSelectedHighlight(Vector2Int coordinate)
    {
        selectedHighlight = highlightColors.ContainsKey(coordinate) ? coordinate : null;
        RefreshHighlights();
    }

    public void ClearHighlights()
    {
        highlightColors.Clear();
        selectedHighlight = null;
        foreach (HexTile tile in tiles.Values) tile.ClearHighlight();
    }

    private void RefreshHighlights()
    {
        foreach (KeyValuePair<Vector2Int, Color> entry in highlightColors)
        {
            if (!tiles.TryGetValue(entry.Key, out HexTile tile)) continue;
            Color color = entry.Value;
            // The selected target is brighter and more opaque without hiding the tile texture.
            if (selectedHighlight == entry.Key)
            {
                color = Color.Lerp(color, Color.white, .42f);
                color.a = .82f;
            }
            tile.SetHighlight(color);
        }
    }

    public static int HexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x;
        int dr = a.y - b.y;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }

    private static void SetTileColor(GameObject tileObject)
    {
        SpriteRenderer renderer = tileObject.GetComponent<SpriteRenderer>();

        if (renderer == null)
            return;

        renderer.color = Color.white;
    }

    private void ClearGrid()
    {
        tiles.Clear();

        Transform parent = tileParent != null ? tileParent : transform;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(parent.GetChild(i).gameObject);
            else
                DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}

public class HexTile : MonoBehaviour
{
    public Vector2Int Coordinate { get; private set; }
    [SerializeField] private TileData tileData;
    [SerializeField, Min(0)] private int currentHP;
    private bool enterEffectConsumed;
    private bool destroyed;
    private SpriteRenderer tileRenderer;
    private SpriteRenderer highlightRenderer;

    public TileData Data => tileData;
    public TileType TileType => destroyed ? TileType.Normal : tileData == null ? TileType.Normal : tileData.tileType;
    public int MoveCost => Mathf.Max(1, tileData == null ? 1 : tileData.moveCost);
    public bool BlocksMovement => !destroyed && tileData != null && tileData.blocksMovement;
    public bool BlocksLineOfSight => !destroyed && tileData != null && tileData.blocksLineOfSight;
    public int ElevationLevel => destroyed || tileData == null ? 0 : tileData.elevationLevel;
    public bool IsDestructible => !destroyed && tileData != null && tileData.tileType == TileType.DestructibleWall;
    public int CurrentHP => currentHP;

    public void Initialize(Vector2Int coordinate, TileData configuredData = null)
    {
        Coordinate = coordinate;
        if (configuredData != null) tileData = configuredData;
        destroyed = false;
        currentHP = IsDestructible ? Mathf.Max(1, tileData.maxHP) : 0;
        enterEffectConsumed = false;
        CreateHighlightOverlay();
    }

    public void ApplyEnterEffect(UnitStats unit)
    {
        if (unit == null || tileData == null || tileData.onEnterEffect == null || enterEffectConsumed) return;
        SkillEffect effect = tileData.onEnterEffect;
        if (effect.type == SkillEffectType.Stun) unit.AddStun(effect.duration);
        else if (effect.type == SkillEffectType.Immobilize) unit.AddImmobilize(effect.duration);
        else if (effect.type == SkillEffectType.Damage) unit.TakeDamage(effect.value);
        else if (effect.type == SkillEffectType.Heal) unit.RestoreHealth(effect.value);
        else if (effect.type == SkillEffectType.Shield) unit.AddShield(effect.value, effect.duration);
        if (tileData.triggerMode == TileTriggerMode.Once) enterEffectConsumed = true;
    }

    public bool TakeTerrainDamage(int amount)
    {
        if (!IsDestructible || amount <= 0) return false;
        currentHP = Mathf.Max(0, currentHP - amount);
        if (currentHP > 0) return false;
        if (tileData.destroyedTile != null) tileData = tileData.destroyedTile;
        else destroyed = true;
        return true;
    }

    public void SetHighlight(Color color)
    {
        if (highlightRenderer == null)
            CreateHighlightOverlay();

        if (highlightRenderer == null)
            return;

        // A translucent overlay keeps the stone texture visible while making range tiles distinct.
        color.a = Mathf.Clamp(color.a, 0.4f, 0.85f);
        tileRenderer.color = Color.Lerp(Color.white, color, color.a > .7f ? .58f : .35f);
        highlightRenderer.color = color;
        highlightRenderer.enabled = true;
    }

    public void ClearHighlight()
    {
        if (highlightRenderer != null)
            highlightRenderer.enabled = false;

        if (tileRenderer != null)
            tileRenderer.color = Color.white;
    }

    private void CreateHighlightOverlay()
    {
        if (highlightRenderer != null)
            return;

        tileRenderer = GetComponent<SpriteRenderer>();
        if (tileRenderer == null || tileRenderer.sprite == null)
            return;

        GameObject overlay = new GameObject("Range Highlight", typeof(SpriteRenderer));
        overlay.transform.SetParent(transform, false);
        highlightRenderer = overlay.GetComponent<SpriteRenderer>();
        highlightRenderer.sprite = tileRenderer.sprite;
        highlightRenderer.sortingLayerID = tileRenderer.sortingLayerID;
        highlightRenderer.sortingOrder = tileRenderer.sortingOrder + 1;
        highlightRenderer.enabled = false;
    }
}
