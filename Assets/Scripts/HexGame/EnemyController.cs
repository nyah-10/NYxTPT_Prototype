using System.Collections;
using UnityEngine;

[RequireComponent(typeof(UnitStats))]
public class EnemyController : MonoBehaviour
{
    public HexGridManager gridManager;
    public Vector2Int startCoordinate = new Vector2Int(7, 5);
    [Min(0.01f)] public float moveDuration = 0.25f;
    [Min(1)] public int longAttackRange = 3;
    [Min(1)] public int longAttackDamage = 1;
    [Min(1)] public int closeAttackRange = 1;
    [Min(1)] public int closeAttackDamage = 4;
    [Range(1, 2)] public int moveRange = 2;
    [Header("Round Action Cards")]
    public int[] actionCardInitiatives = { 18, 32, 46, 61, 74, 89 };

    private Vector2Int currentCoordinate;
    public Vector2Int CurrentCoordinate => currentCoordinate;
    public int CurrentCardInitiative { get; private set; } = 50;

    public void PrepareRoundAction()
    {
        if (actionCardInitiatives == null || actionCardInitiatives.Length == 0)
        {
            CurrentCardInitiative = 50;
            return;
        }

        CurrentCardInitiative = Mathf.Max(1, actionCardInitiatives[Random.Range(0, actionCardInitiatives.Length)]);
    }

    private static readonly Vector2Int[] NeighborDirections =
    {
        new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0)
    };

    private void Start()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<HexGridManager>();

        if (gridManager != null && gridManager.TryGetTile(startCoordinate, out HexTile startTile))
        {
            currentCoordinate = startCoordinate;
            transform.position = startTile.transform.position;
        }
    }

    public IEnumerator TakeTurn(PlayerController player)
    {
        UnitStats stats = GetComponent<UnitStats>();
        if (stats != null && stats.IsDead)
            yield break;

        if (stats != null && stats.IsStunned)
        {
            stats.BeginTurn();
            yield break;
        }

        if (stats != null && stats.IsImmobilized)
        {
            stats.BeginTurn();
            yield break;
        }

        if (stats != null)
            stats.BeginTurn();

        Vector2Int playerCoordinate = player.CurrentCoordinate;
        int distance = HexDistance(currentCoordinate, playerCoordinate);

        if (distance > longAttackRange && !stats.IsImmobilized)
        {
            Vector2Int target = FindBestDestination(playerCoordinate);
            if (target != currentCoordinate && gridManager.TryGetTile(target, out HexTile targetTile))
                yield return MoveToTile(target, targetTile.transform.position);
        }

        AttackPlayer(player);
    }

    private Vector2Int FindBestDestination(Vector2Int playerCoordinate)
    {
        Vector2Int bestCoordinate = currentCoordinate;
        int bestDistance = HexDistance(currentCoordinate, playerCoordinate);

        // Evaluate every reachable hex so the movement skill can travel up to two tiles.
        for (int step = 1; step <= moveRange; step++)
        {
            foreach (Vector2Int direction in NeighborDirections)
            {
                Vector2Int candidate = currentCoordinate + direction * step;
                if (candidate == playerCoordinate || !gridManager.TryGetTile(candidate, out _))
                    continue;

                int distance = HexDistance(candidate, playerCoordinate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCoordinate = candidate;
                }
            }
        }

        return bestCoordinate;
    }

    private void AttackPlayer(PlayerController player)
    {
        UnitStats playerStats = player.GetComponent<UnitStats>();
        if (playerStats == null || playerStats.IsDead)
            return;

        int distance = HexDistance(currentCoordinate, player.CurrentCoordinate);
        if (distance <= closeAttackRange)
        {
            Debug.Log($"{name} used its close main skill for {closeAttackDamage} damage.");
            playerStats.TakeDamage(closeAttackDamage);
        }
        else if (distance <= longAttackRange)
        {
            Debug.Log($"{name} used its ranged main skill for {longAttackDamage} damage.");
            playerStats.TakeDamage(longAttackDamage);
        }
    }

    private static int HexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x;
        int dr = a.y - b.y;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }

    private IEnumerator MoveToTile(Vector2Int targetCoordinate, Vector3 targetPosition)
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
        currentCoordinate = targetCoordinate;
    }

    public bool ForceMoveTo(Vector2Int targetCoordinate)
    {
        if (gridManager == null || !gridManager.TryGetTile(targetCoordinate, out HexTile tile))
            return false;

        currentCoordinate = targetCoordinate;
        transform.position = tile.transform.position;
        return true;
    }
}
