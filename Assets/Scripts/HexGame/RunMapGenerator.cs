using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RunMapShape { Linear, BranchingTree }

[Serializable]
public class RunMapNode
{
    public int id;
    public RoomTemplate room;
    public Vector2 debugPosition;
    public Vector2Int mapOffset;
}

[Serializable]
public class RunMapConnection
{
    public int fromNode;
    public int toNode;
    public Vector2Int fromEntry;
    public Vector2Int toEntry;
}

public class GeneratedDungeonLayout
{
    public Vector2Int gridSize;
    public List<RoomTile> tiles = new();
    public Vector2Int playerSpawn;
    public Vector2Int enemySpawn;
}

public class RunMapGenerator : MonoBehaviour
{
    [Header("Room Pool")]
    public List<RoomTemplate> roomTemplates = new();
    [Range(3, 6)] public int minimumRooms = 3;
    [Range(3, 6)] public int maximumRooms = 6;
    [Min(0)] public int recentTemplateCooldown = 2;
    [Range(0f, 1f)] public float difficulty;
    [Min(0f)] public float hazardWeightAtEasy = .25f;
    [Min(0f)] public float hazardWeightAtHard = 3f;
    public RunMapShape mapShape = RunMapShape.Linear;

    [Header("Scene Integration")]
    [Tooltip("Optional. Empty means apply the room to the grid in the current scene.")]
    public string combatSceneName;
    public bool generateOnStart = true;

    [Header("Debug")]
    public bool logGeneratedMap = true;
    public bool drawGizmos = true;
    public List<RunMapNode> nodes = new();
    public List<RunMapConnection> connections = new();

    public static RoomTemplate PendingRoom { get; private set; }
    public static GeneratedDungeonLayout PendingDungeon { get; private set; }

    private readonly Queue<RoomTemplate> recentRooms = new();

    private void Start()
    {
        if (generateOnStart) GenerateMap();
    }

    [ContextMenu("Generate Run Map")]
    public void GenerateMap()
    {
        nodes.Clear();
        connections.Clear();
        int count = UnityEngine.Random.Range(Mathf.Min(minimumRooms, maximumRooms), Mathf.Max(minimumRooms, maximumRooms) + 1);
        for (int i = 0; i < count; i++)
        {
            RoomTemplate room = ChooseRoom();
            if (room == null) break;
            nodes.Add(new RunMapNode { id = i, room = room });
            Remember(room);
        }

        ComposeDungeon();

        if (logGeneratedMap)
            Debug.Log($"Generated {nodes.Count}-room {mapShape} map: " + string.Join(" -> ", nodes.ConvertAll(n => n.room.display_name)));
    }

    public void SelectNode(int nodeId)
    {
        RunMapNode node = nodes.Find(candidate => candidate.id == nodeId);
        if (node?.room == null) return;
        PendingRoom = node.room;
        if (!string.IsNullOrWhiteSpace(combatSceneName)) SceneManager.LoadScene(combatSceneName);
        else FindAnyObjectByType<HexGridManager>()?.ApplyRoomTemplate(PendingRoom);
    }

    public void LoadGeneratedDungeon()
    {
        if (nodes.Count == 0) GenerateMap();
        PendingDungeon = BuildLayout();
        if (PendingDungeon == null || PendingDungeon.tiles.Count == 0) return;
        PendingRoom = null;
        if (!string.IsNullOrWhiteSpace(combatSceneName)) SceneManager.LoadScene(combatSceneName);
        else FindAnyObjectByType<HexGridManager>()?.ApplyDungeonLayout(PendingDungeon);
    }

    public static GeneratedDungeonLayout ConsumePendingDungeon()
    {
        GeneratedDungeonLayout layout = PendingDungeon;
        PendingDungeon = null;
        return layout;
    }

    public static RoomTemplate ConsumePendingRoom()
    {
        RoomTemplate room = PendingRoom;
        PendingRoom = null;
        return room;
    }

    private RoomTemplate ChooseRoom()
    {
        List<RoomTemplate> candidates = roomTemplates.FindAll(room => room != null && !recentRooms.Contains(room));
        if (candidates.Count == 0) candidates = roomTemplates.FindAll(room => room != null);
        float total = 0f;
        List<float> weights = new();
        foreach (RoomTemplate room in candidates)
        {
            bool hazard = room.tags.Exists(tag => string.Equals(tag, "hazard", StringComparison.OrdinalIgnoreCase));
            float weight = hazard ? Mathf.Lerp(hazardWeightAtEasy, hazardWeightAtHard, difficulty) : 1f;
            weights.Add(Mathf.Max(0.001f, weight));
            total += weights[^1];
        }
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < candidates.Count; i++) if ((roll -= weights[i]) <= 0f) return candidates[i];
        return candidates.Count > 0 ? candidates[^1] : null;
    }

    private void Remember(RoomTemplate room)
    {
        if (recentTemplateCooldown <= 0) return;
        recentRooms.Enqueue(room);
        while (recentRooms.Count > recentTemplateCooldown) recentRooms.Dequeue();
    }

    private void ComposeDungeon()
    {
        connections.Clear();
        if (nodes.Count == 0) return;
        HashSet<Vector2Int> occupied = new();
        nodes[0].mapOffset = Vector2Int.zero;
        AddOccupied(nodes[0], occupied);

        for (int childIndex = 1; childIndex < nodes.Count; childIndex++)
        {
            bool placed = false;
            List<int> parents = ParentCandidates(childIndex);
            foreach (int parentIndex in parents)
            {
                if (!TryPlace(nodes[parentIndex], nodes[childIndex], occupied,
                    out Vector2Int offset, out Vector2Int fromEntry, out Vector2Int toEntry)) continue;
                nodes[childIndex].mapOffset = offset;
                connections.Add(new RunMapConnection { fromNode = parentIndex, toNode = childIndex, fromEntry = fromEntry, toEntry = toEntry });
                placed = true;
                break;
            }

            if (!placed)
            {
                RunMapNode parent = nodes[childIndex - 1];
                int rightEdge = MaxOccupiedX(occupied) + 1;
                nodes[childIndex].mapOffset = new Vector2Int(rightEdge, parent.mapOffset.y);
                connections.Add(new RunMapConnection { fromNode = parent.id, toNode = childIndex });
                Debug.LogWarning($"No collision-free entry pair for {nodes[childIndex].room.name}; placed it at the dungeon frontier.");
            }
            AddOccupied(nodes[childIndex], occupied);
        }

        NormalizeOffsets();
    }

    private List<int> ParentCandidates(int childIndex)
    {
        List<int> result = new();
        if (mapShape == RunMapShape.Linear) { result.Add(childIndex - 1); return result; }
        for (int i = 0; i < childIndex; i++) result.Add(i);
        for (int i = result.Count - 1; i > 0; i--)
        {
            int swap = UnityEngine.Random.Range(0, i + 1);
            (result[i], result[swap]) = (result[swap], result[i]);
        }
        return result;
    }

    private static bool TryPlace(RunMapNode parent, RunMapNode child, HashSet<Vector2Int> occupied,
        out Vector2Int offset, out Vector2Int fromEntry, out Vector2Int toEntry)
    {
        foreach (Vector2Int from in parent.room.entry_points)
        foreach (Vector2Int to in child.room.entry_points)
        {
            if (!IsOppositeEdge(parent.room, from, child.room, to)) continue;
            Vector2Int candidate = parent.mapOffset + from + Outward(parent.room, from) - to;
            if (Overlaps(child.room, candidate, occupied)) continue;
            offset = candidate; fromEntry = from; toEntry = to; return true;
        }
        offset = default; fromEntry = default; toEntry = default; return false;
    }

    private static bool IsOppositeEdge(RoomTemplate a, Vector2Int p, RoomTemplate b, Vector2Int q) =>
        p.x == 0 && q.x == b.GridSize.x - 1 || p.x == a.GridSize.x - 1 && q.x == 0 ||
        p.y == 0 && q.y == b.GridSize.y - 1 || p.y == a.GridSize.y - 1 && q.y == 0;

    private static Vector2Int Outward(RoomTemplate room, Vector2Int entry)
    {
        if (entry.x == 0) return Vector2Int.left;
        if (entry.x == room.GridSize.x - 1) return Vector2Int.right;
        if (entry.y == 0) return Vector2Int.down;
        return Vector2Int.up;
    }

    private static bool Overlaps(RoomTemplate room, Vector2Int offset, HashSet<Vector2Int> occupied)
    {
        foreach (RoomTile tile in room.TileLayout)
            if (tile.tileData != null && occupied.Contains(tile.coordinate + offset)) return true;
        return false;
    }

    private static void AddOccupied(RunMapNode node, HashSet<Vector2Int> occupied)
    {
        foreach (RoomTile tile in node.room.TileLayout)
            if (tile.tileData != null) occupied.Add(tile.coordinate + node.mapOffset);
    }

    private static int MaxOccupiedX(HashSet<Vector2Int> occupied)
    {
        int maximum = 0;
        foreach (Vector2Int coordinate in occupied) maximum = Mathf.Max(maximum, coordinate.x);
        return maximum;
    }

    private void NormalizeOffsets()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (RunMapNode node in nodes)
        foreach (RoomTile tile in node.room.TileLayout)
        {
            Vector2Int coordinate = tile.coordinate + node.mapOffset;
            minX = Mathf.Min(minX, coordinate.x); minY = Mathf.Min(minY, coordinate.y);
        }
        Vector2Int shift = minX == int.MaxValue ? Vector2Int.zero : new Vector2Int(-minX, -minY);
        foreach (RunMapNode node in nodes)
        {
            node.mapOffset += shift;
            node.debugPosition = node.mapOffset + node.room.GridSize / 2;
        }
    }

    private GeneratedDungeonLayout BuildLayout()
    {
        Dictionary<Vector2Int, TileData> combined = new();
        int maxX = -1, maxY = -1;
        foreach (RunMapNode node in nodes)
        foreach (RoomTile tile in node.room.TileLayout)
        {
            if (tile.tileData == null) continue;
            Vector2Int coordinate = tile.coordinate + node.mapOffset;
            combined[coordinate] = tile.tileData;
            maxX = Mathf.Max(maxX, coordinate.x); maxY = Mathf.Max(maxY, coordinate.y);
        }
        if (combined.Count == 0) return null;
        GeneratedDungeonLayout layout = new() { gridSize = new Vector2Int(maxX + 1, maxY + 1) };
        foreach (KeyValuePair<Vector2Int, TileData> tile in combined)
            layout.tiles.Add(new RoomTile { coordinate = tile.Key, tileData = tile.Value });
        layout.playerSpawn = FindSpawn(nodes[0], combined, false);
        layout.enemySpawn = FindSpawn(nodes[^1], combined, true);
        return layout;
    }

    private static Vector2Int FindSpawn(RunMapNode node, Dictionary<Vector2Int, TileData> combined, bool reverse)
    {
        IReadOnlyList<RoomTile> tiles = node.room.TileLayout;
        for (int step = 0; step < tiles.Count; step++)
        {
            int index = reverse ? tiles.Count - 1 - step : step;
            Vector2Int coordinate = tiles[index].coordinate + node.mapOffset;
            if (combined.TryGetValue(coordinate, out TileData data) && data != null && !data.blocksMovement) return coordinate;
        }
        return node.mapOffset;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        foreach (RunMapConnection edge in connections)
        {
            RunMapNode a = nodes.Find(node => node.id == edge.fromNode);
            RunMapNode b = nodes.Find(node => node.id == edge.toNode);
            if (a != null && b != null) Gizmos.DrawLine(a.debugPosition, b.debugPosition);
        }
        foreach (RunMapNode node in nodes)
        {
            Gizmos.color = node.room != null && node.room.tags.Contains("hazard") ? Color.red : Color.green;
            Gizmos.DrawSphere(node.debugPosition, .3f);
        }
    }
}
