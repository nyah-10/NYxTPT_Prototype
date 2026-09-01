using System.Collections;
using UnityEngine;

// Player movement is invoked only by a committed sub-skill.
public class PlayerController : MonoBehaviour
{
    public HexGridManager gridManager;
    public TurnManager turnManager;
    public Vector2Int startCoordinate = Vector2Int.zero;
    [Min(0.05f)] public float moveDuration = .3f;

    private Vector2Int currentCoordinate;
    private Coroutine movement;
    public Vector2Int CurrentCoordinate => currentCoordinate;
    public bool IsMoving => movement != null;

    private void Start()
    {
        if (gridManager == null)
            gridManager = FindAnyObjectByType<HexGridManager>();
        if (turnManager == null)
            turnManager = FindAnyObjectByType<TurnManager>();

        if (gridManager != null)
            startCoordinate = gridManager.PlayerSpawnCoordinate;

        if (gridManager == null || !gridManager.TryGetTile(startCoordinate, out HexTile tile))
        {
            enabled = false;
            return;
        }

        currentCoordinate = startCoordinate;
        transform.position = tile.transform.position;
    }

    public bool TryMoveTo(Vector2Int targetCoordinate)
    {
        if (gridManager == null || !gridManager.TryGetTile(targetCoordinate, out HexTile tile) || tile.BlocksMovement)
            return false;

        var path = gridManager.FindPath(currentCoordinate, targetCoordinate);
        if (path.Count == 0 && targetCoordinate != currentCoordinate) return false;
        if (movement != null) StopCoroutine(movement);
        movement = StartCoroutine(MoveAlongPath(path));
        return true;
    }

    private IEnumerator MoveAlongPath(System.Collections.Generic.List<Vector2Int> path)
    {
        foreach (Vector2Int coordinate in path)
        {
            if (!gridManager.TryGetTile(coordinate, out HexTile tile)) break;
            yield return MoveToTile(tile.transform.position);
            currentCoordinate = coordinate;
            tile.ApplyEnterEffect(GetComponent<UnitStats>());
        }
        movement = null;
    }

    private IEnumerator MoveToTile(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
            transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }

        transform.position = targetPosition;
    }
}
