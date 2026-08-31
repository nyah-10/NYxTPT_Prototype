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
            gridManager = FindFirstObjectByType<HexGridManager>();
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

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
        if (gridManager == null || !gridManager.TryGetTile(targetCoordinate, out HexTile tile))
            return false;

        currentCoordinate = targetCoordinate;
        if (movement != null) StopCoroutine(movement);
        movement = StartCoroutine(MoveToTile(tile.transform.position));
        return true;
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
        movement = null;
    }
}
