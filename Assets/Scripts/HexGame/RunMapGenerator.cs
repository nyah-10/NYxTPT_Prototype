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
}

[Serializable]
public class RunMapConnection
{
    public int fromNode;
    public int toNode;
    public Vector2Int fromEntry;
    public Vector2Int toEntry;
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
            nodes.Add(new RunMapNode { id = i, room = room, debugPosition = GetDebugPosition(i) });
            Remember(room);
            if (i > 0) ConnectNode(i);
        }

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

    private void ConnectNode(int childIndex)
    {
        int parentIndex = mapShape == RunMapShape.Linear ? childIndex - 1 : UnityEngine.Random.Range(0, childIndex);
        RoomTemplate from = nodes[parentIndex].room;
        RoomTemplate to = nodes[childIndex].room;
        FindEntryPair(from, to, out Vector2Int fromEntry, out Vector2Int toEntry);
        connections.Add(new RunMapConnection { fromNode = parentIndex, toNode = childIndex, fromEntry = fromEntry, toEntry = toEntry });
    }

    private static void FindEntryPair(RoomTemplate from, RoomTemplate to, out Vector2Int fromEntry, out Vector2Int toEntry)
    {
        fromEntry = from.entry_points.Count > 0 ? from.entry_points[UnityEngine.Random.Range(0, from.entry_points.Count)] : Vector2Int.zero;
        toEntry = to.entry_points.Count > 0 ? to.entry_points[UnityEngine.Random.Range(0, to.entry_points.Count)] : Vector2Int.zero;
        // Opposing edges make the selected doorway pair useful to a later physical room compositor.
        foreach (Vector2Int candidate in to.entry_points)
            if (IsOppositeEdge(from, fromEntry, to, candidate)) { toEntry = candidate; return; }
    }

    private static bool IsOppositeEdge(RoomTemplate a, Vector2Int p, RoomTemplate b, Vector2Int q) =>
        p.x == 0 && q.x == b.GridSize.x - 1 || p.x == a.GridSize.x - 1 && q.x == 0 ||
        p.y == 0 && q.y == b.GridSize.y - 1 || p.y == a.GridSize.y - 1 && q.y == 0;

    private Vector2 GetDebugPosition(int index)
    {
        if (mapShape == RunMapShape.Linear) return new Vector2(index * 3f, 0f);
        int depth = Mathf.FloorToInt(Mathf.Log(index + 1, 2));
        int first = (1 << depth) - 1;
        return new Vector2(depth * 3f, (index - first - (1 << depth) * .5f) * 2f);
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
