#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RoomTemplateEditorWindow : EditorWindow
{
    private RoomTemplate room;
    private TileData brush;
    private Vector2 scroll;
    private int width = 8;
    private int height = 6;

    [MenuItem("Tools/Hex Roguelike/Room Template Painter")]
    public static void Open() => GetWindow<RoomTemplateEditorWindow>("Room Painter");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Left click paints the selected TileData. Right click erases. Shift-click toggles an entry point (edge cells only).", MessageType.Info);
        room = (RoomTemplate)EditorGUILayout.ObjectField("Room Template", room, typeof(RoomTemplate), false);
        using (new EditorGUILayout.HorizontalScope())
        {
            width = EditorGUILayout.IntField("Width", width);
            height = EditorGUILayout.IntField("Height", height);
            if (GUILayout.Button("Create New", GUILayout.Width(100))) CreateRoom();
        }
        if (GUILayout.Button("Create 6 Sample Rooms + Terrain Palette")) CreateSamples();
        if (room == null) return;

        SerializedObject serializedRoom = new(room);
        serializedRoom.Update();
        EditorGUILayout.PropertyField(serializedRoom.FindProperty("room_id"));
        EditorGUILayout.PropertyField(serializedRoom.FindProperty("display_name"));
        EditorGUILayout.PropertyField(serializedRoom.FindProperty("tags"), true);
        serializedRoom.ApplyModifiedProperties();
        brush = (TileData)EditorGUILayout.ObjectField("Tile Brush", brush, typeof(TileData), false);

        Vector2Int size = room.GridSize;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int y = size.y - 1; y >= 0; y--)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int x = 0; x < size.x; x++) DrawCell(new Vector2Int(x, y));
        }
        EditorGUILayout.EndScrollView();

        if (GUI.changed) EditorUtility.SetDirty(room);
    }

    private void DrawCell(Vector2Int coordinate)
    {
        TileData data = room.GetTile(coordinate);
        bool entry = room.entry_points.Contains(coordinate);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = entry ? new Color(1f, .65f, .15f) : TileColor(data);
        string label = data == null ? "·" : data.tileType.ToString()[..1];
        Rect cell = GUILayoutUtility.GetRect(34, 30, GUILayout.Width(34), GUILayout.Height(30));
        GUI.Button(cell, new GUIContent(label, $"{coordinate}: {(data == null ? "Default" : data.name)}"));
        Event current = Event.current;
        if (current.type == EventType.MouseDown && cell.Contains(current.mousePosition))
        {
            Undo.RecordObject(room, "Paint Room Template");
            if (current.shift && room.IsEdge(coordinate))
            {
                if (entry) room.entry_points.Remove(coordinate); else room.entry_points.Add(coordinate);
            }
            else room.SetTile(coordinate, current.button == 1 ? null : brush);
            EditorUtility.SetDirty(room);
            current.Use();
            Repaint();
        }
        GUI.backgroundColor = old;
    }

    private void CreateRoom()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Room Template", "Room_New", "asset", "Choose a location for the room asset.");
        if (string.IsNullOrEmpty(path)) return;
        room = CreateInstance<RoomTemplate>();
        room.Resize(new Vector2Int(width, height));
        room.room_id = System.IO.Path.GetFileNameWithoutExtension(path);
        room.display_name = room.room_id;
        AssetDatabase.CreateAsset(room, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = room;
    }

    private static Color TileColor(TileData data)
    {
        if (data == null) return new Color(.55f, .55f, .55f);
        return data.tileType switch
        {
            TileType.Trap => new Color(.95f, .3f, .25f),
            TileType.Obstacle or TileType.DestructibleWall => new Color(.25f, .25f, .25f),
            TileType.Elevated => new Color(.45f, .75f, 1f),
            TileType.Swamp => new Color(.3f, .65f, .35f),
            _ => Color.white
        };
    }

    [MenuItem("Tools/Hex Roguelike/Create Sample Rooms")]
    public static void CreateSamples()
    {
        EnsureFolder("Assets", "RoomTemplates");
        EnsureFolder("Assets/RoomTemplates", "Terrain");
        TileData normal = Terrain("Normal", TileType.Normal, false, 1);
        TileData wall = Terrain("Wall", TileType.Obstacle, true, 1);
        TileData elevated = Terrain("Elevated", TileType.Elevated, false, 1, 1);
        TileData hazard = Terrain("Hazard", TileType.Trap, false, 1);
        TileData swamp = Terrain("Swamp", TileType.Swamp, false, 2);

        CreateSample("StraightPassage", "Straight Passage", 8, 4, normal, wall, new[] { "narrow" }, 0);
        CreateSample("OpenArena", "Open Arena", 8, 6, normal, wall, new[] { "open" }, 1);
        CreateSample("HighGround", "High Ground", 8, 6, normal, elevated, new[] { "open", "elevated" }, 2);
        CreateSample("SpikeCrossing", "Spike Crossing", 8, 6, normal, hazard, new[] { "narrow", "hazard" }, 3);
        CreateSample("FloodedRuins", "Flooded Ruins", 8, 6, normal, swamp, new[] { "open", "hazard" }, 4);
        CreateSample("BrokenRamparts", "Broken Ramparts", 9, 6, normal, wall, new[] { "elevated", "hazard" }, 5);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created six editable room templates in Assets/RoomTemplates.");
    }

    private static TileData Terrain(string name, TileType type, bool blocking, int moveCost, int elevation = 0)
    {
        string path = $"Assets/RoomTemplates/Terrain/{name}.asset";
        TileData data = AssetDatabase.LoadAssetAtPath<TileData>(path);
        if (data == null) { data = CreateInstance<TileData>(); AssetDatabase.CreateAsset(data, path); }
        data.tileType = type; data.blocksMovement = blocking; data.blocksLineOfSight = blocking;
        data.moveCost = moveCost; data.elevationLevel = elevation;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void CreateSample(string id, string title, int width, int height, TileData floor, TileData feature, string[] tags, int pattern)
    {
        string path = $"Assets/RoomTemplates/{id}.asset";
        RoomTemplate sample = AssetDatabase.LoadAssetAtPath<RoomTemplate>(path);
        if (sample == null) { sample = CreateInstance<RoomTemplate>(); AssetDatabase.CreateAsset(sample, path); }
        sample.room_id = id; sample.display_name = title; sample.Resize(new Vector2Int(width, height));
        sample.tags = new List<string>(tags); sample.entry_points.Clear();
        sample.entry_points.Add(new Vector2Int(0, height / 2)); sample.entry_points.Add(new Vector2Int(width - 1, height / 2));
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool marked = pattern switch
            {
                0 => y == 0 || y == height - 1,
                1 => (x == width / 2 && y == height / 2),
                2 => y >= height / 2 && x > 1 && x < width - 2,
                3 => x == width / 2 || y == height / 2,
                4 => (x + y) % 3 == 0,
                _ => (x == 2 || x == width - 3) && y != height / 2
            };
            sample.SetTile(new Vector2Int(x, y), marked ? feature : floor);
        }
        EditorUtility.SetDirty(sample);
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
