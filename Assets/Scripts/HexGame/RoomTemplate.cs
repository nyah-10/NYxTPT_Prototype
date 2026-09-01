using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RoomTile
{
    public Vector2Int coordinate;
    public TileData tileData;
}

[CreateAssetMenu(fileName = "Room_", menuName = "Hex Roguelike/Room Template")]
public class RoomTemplate : ScriptableObject
{
    public string room_id;
    public string display_name;
    [SerializeField] private Vector2Int grid_size = new(8, 6);
    [SerializeField] private List<RoomTile> tile_layout = new();
    public List<Vector2Int> entry_points = new();
    public List<string> tags = new();

    public Vector2Int GridSize => new(Mathf.Max(1, grid_size.x), Mathf.Max(1, grid_size.y));
    public IReadOnlyList<RoomTile> TileLayout => tile_layout;

    public void Resize(Vector2Int size)
    {
        grid_size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        tile_layout.RemoveAll(tile => !Contains(tile.coordinate));
        entry_points.RemoveAll(point => !Contains(point) || !IsEdge(point));
    }

    public TileData GetTile(Vector2Int coordinate)
    {
        for (int i = tile_layout.Count - 1; i >= 0; i--)
            if (tile_layout[i].coordinate == coordinate) return tile_layout[i].tileData;
        return null;
    }

    public void SetTile(Vector2Int coordinate, TileData data)
    {
        if (!Contains(coordinate)) return;
        tile_layout.RemoveAll(tile => tile.coordinate == coordinate);
        if (data != null) tile_layout.Add(new RoomTile { coordinate = coordinate, tileData = data });
    }

    public bool IsEdge(Vector2Int point) => Contains(point) &&
        (point.x == 0 || point.y == 0 || point.x == GridSize.x - 1 || point.y == GridSize.y - 1);

    public bool Contains(Vector2Int point) => point.x >= 0 && point.y >= 0 &&
        point.x < GridSize.x && point.y < GridSize.y;

    private void OnValidate()
    {
        Resize(grid_size);
        if (string.IsNullOrWhiteSpace(room_id)) room_id = name;
        tags.RemoveAll(string.IsNullOrWhiteSpace);
    }
}
