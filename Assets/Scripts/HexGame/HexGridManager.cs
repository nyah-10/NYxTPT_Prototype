
using System.Collections.Generic;
using UnityEngine;

public class HexGridManager : MonoBehaviour
{
    [Header("Map Size")]
    [Min(1)] public int width = 8;
    [Min(1)] public int height = 6;

    [Header("Tile")]
    public GameObject hexTilePrefab;
    [Min(0.01f)] public float hexRadius = 0.5f;
    public Transform tileParent;

    private readonly Dictionary<Vector2Int, HexTile> tiles = new();
    private readonly Dictionary<Vector2Int, Color> highlightColors = new();
    private Vector2Int? selectedHighlight;

    private void Awake()
    {
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

                tile.Initialize(coordinate);
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
    private SpriteRenderer tileRenderer;
    private SpriteRenderer highlightRenderer;

    public void Initialize(Vector2Int coordinate)
    {
        Coordinate = coordinate;
        CreateHighlightOverlay();
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
