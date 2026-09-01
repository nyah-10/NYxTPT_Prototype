using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MonsterTargetRule { Nearest, LowestHp, HighestThreat, FixedPriority }
public enum MonsterActionOrder { MoveThenAttack, AttackThenMove }

[System.Serializable]
public class MonsterAbilityCard
{
    public string id = "basic";
    [Min(1)] public int initiative = 50;
    [Min(0)] public int move = 2;
    [Min(0)] public int attackValue = 2;
    [Min(1)] public int range = 1;
    public MonsterActionOrder executionOrder = MonsterActionOrder.MoveThenAttack;
    public MonsterTargetRule targetRule = MonsterTargetRule.Nearest;
}

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
    public MonsterAbilityCard[] abilityDeck;
    [Tooltip("Legacy initiative-only deck. Used only when Ability Deck is empty.")]
    public int[] actionCardInitiatives = { 18, 32, 46, 61, 74, 89 };

    private Vector2Int currentCoordinate;
    public Vector2Int CurrentCoordinate => currentCoordinate;
    public int CurrentCardInitiative { get; private set; } = 50;
    public MonsterAbilityCard CurrentCard { get; private set; }
    public string RevealedCardSummary => CurrentCard == null ? "준비 중" :
        $"{CurrentCard.id} [{CurrentCard.initiative}] 이동 {CurrentCard.move} / 공격 {CurrentCard.attackValue} (사거리 {CurrentCard.range}, {CurrentCard.targetRule})";

    public void PrepareRoundAction()
    {
        if (abilityDeck != null && abilityDeck.Length > 0)
        {
            CurrentCard = abilityDeck[Random.Range(0, abilityDeck.Length)];
            CurrentCardInitiative = Mathf.Max(1, CurrentCard.initiative);
            return;
        }

        if (actionCardInitiatives == null || actionCardInitiatives.Length == 0)
        {
            CurrentCardInitiative = 50;
        }
        else CurrentCardInitiative = Mathf.Max(1, actionCardInitiatives[Random.Range(0, actionCardInitiatives.Length)]);

        CurrentCard = new MonsterAbilityCard
        {
            id = "기본 행동",
            initiative = CurrentCardInitiative,
            move = moveRange,
            attackValue = closeAttackDamage,
            range = closeAttackRange
        };
    }

    private static readonly Vector2Int[] NeighborDirections =
    {
        new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0)
    };

    private void Start()
    {
        if (gridManager == null)
            gridManager = FindAnyObjectByType<HexGridManager>();

        if (gridManager != null)
            startCoordinate = gridManager.EnemySpawnCoordinate;

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

        MonsterAbilityCard card = CurrentCard ?? new MonsterAbilityCard();
        if (card.executionOrder == MonsterActionOrder.AttackThenMove)
        {
            TryAttack(card);
            yield return TryMove(card, stats);
        }
        else
        {
            yield return TryMove(card, stats);
            TryAttack(card);
        }
    }

    private IEnumerator TryMove(MonsterAbilityCard card, UnitStats stats)
    {
        UnitStats focus = ResolveMovementTarget(card.targetRule);
        if (focus == null || card.move <= 0 || stats.IsImmobilized) yield break;
        Vector2Int focusCoordinate = CoordinateOf(focus);
        if (gridManager.CanTarget(currentCoordinate, focusCoordinate, card.range)) yield break;

        List<Vector2Int> path = gridManager.FindPath(currentCoordinate, focusCoordinate);
        int spentMovement = 0;
        foreach (Vector2Int step in path)
        {
            if (!gridManager.TryGetTile(step, out HexTile tile) || IsOccupiedByOtherUnit(step)) break;
            int nextCost = spentMovement + tile.MoveCost;
            if (nextCost > card.move) break;
            spentMovement = nextCost;
            yield return MoveToTile(step, tile.transform.position);
            tile.ApplyEnterEffect(stats);
        }
    }

    private UnitStats ResolveMovementTarget(MonsterTargetRule rule)
    {
        UnitStats best = null;
        int bestPathLength = int.MaxValue;
        foreach (PlayerController candidate in FindObjectsByType<PlayerController>())
        {
            UnitStats stats = candidate.GetComponent<UnitStats>();
            if (stats == null || stats.IsDead || !gridManager.TryGetTile(candidate.CurrentCoordinate, out _)) continue;
            List<Vector2Int> path = gridManager.FindPath(currentCoordinate, candidate.CurrentCoordinate);
            if (path.Count == 0 && currentCoordinate != candidate.CurrentCoordinate) continue;
            if (best == null || IsBetterMovementTarget(stats, best, rule, path.Count, bestPathLength))
            {
                best = stats;
                bestPathLength = path.Count;
            }
        }
        return best;
    }

    private bool IsBetterMovementTarget(UnitStats candidate, UnitStats current, MonsterTargetRule rule,
        int candidatePathLength, int currentPathLength)
    {
        if (rule == MonsterTargetRule.LowestHp && candidate.CurrentHP != current.CurrentHP)
            return candidate.CurrentHP < current.CurrentHP;
        if (rule == MonsterTargetRule.HighestThreat)
        {
            UnitStats self = GetComponent<UnitStats>();
            int candidateThreat = self.GetThreatFrom(candidate);
            int currentThreat = self.GetThreatFrom(current);
            if (candidateThreat != currentThreat) return candidateThreat > currentThreat;
        }
        if (rule == MonsterTargetRule.FixedPriority && RolePriority(candidate.Role) != RolePriority(current.Role))
            return RolePriority(candidate.Role) > RolePriority(current.Role);
        return candidatePathLength < currentPathLength;
    }

    private void TryAttack(MonsterAbilityCard card)
    {
        UnitStats target = ResolveTarget(card.targetRule, card.range);
        if (target == null)
        {
            Debug.Log($"{name}: {card.id} - 타겟 없음");
            FindAnyObjectByType<TurnManager>()?.ReportFeedback($"{name}: 타겟 없음");
            return;
        }
        target.TakeDamage(card.attackValue, GetComponent<UnitStats>());
    }

    private UnitStats ResolveTarget(MonsterTargetRule rule, int range)
    {
        UnitStats best = null;
        foreach (PlayerController candidate in FindObjectsByType<PlayerController>())
        {
            UnitStats stats = candidate.GetComponent<UnitStats>();
            int distance = HexDistance(currentCoordinate, candidate.CurrentCoordinate);
            if (stats == null || stats.IsDead || !gridManager.CanTarget(currentCoordinate, candidate.CurrentCoordinate, range)) continue;
            if (best == null || IsBetterTarget(stats, best, rule, distance)) best = stats;
        }
        return best;
    }

    private bool IsBetterTarget(UnitStats candidate, UnitStats current, MonsterTargetRule rule, int candidateDistance)
    {
        int currentDistance = HexDistance(currentCoordinate, CoordinateOf(current));
        if (rule == MonsterTargetRule.LowestHp && candidate.CurrentHP != current.CurrentHP)
            return candidate.CurrentHP < current.CurrentHP;
        if (rule == MonsterTargetRule.HighestThreat)
        {
            UnitStats self = GetComponent<UnitStats>();
            int candidateThreat = self.GetThreatFrom(candidate);
            int currentThreat = self.GetThreatFrom(current);
            if (candidateThreat != currentThreat) return candidateThreat > currentThreat;
        }
        if (rule == MonsterTargetRule.FixedPriority && RolePriority(candidate.Role) != RolePriority(current.Role))
            return RolePriority(candidate.Role) > RolePriority(current.Role);
        // Equal rule scores always fall back to nearest for deterministic, readable behavior.
        return candidateDistance < currentDistance;
    }

    private static int RolePriority(CombatRole role) => role switch
    {
        CombatRole.Healer => 4,
        CombatRole.Support => 3,
        CombatRole.Damage => 2,
        CombatRole.Tank => 1,
        _ => 0
    };

    private static Vector2Int CoordinateOf(UnitStats stats) => stats.GetComponent<PlayerController>().CurrentCoordinate;

    private bool IsOccupiedByOtherUnit(Vector2Int coordinate)
    {
        foreach (UnitStats unit in FindObjectsByType<UnitStats>())
        {
            if (unit == null || unit.gameObject == gameObject || unit.IsDead) continue;
            PlayerController player = unit.GetComponent<PlayerController>();
            EnemyController enemy = unit.GetComponent<EnemyController>();
            if (player != null && player.CurrentCoordinate == coordinate ||
                enemy != null && enemy.CurrentCoordinate == coordinate) return true;
        }
        return false;
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
        if (gridManager == null || !gridManager.TryGetTile(targetCoordinate, out HexTile tile) || tile.BlocksMovement)
            return false;

        currentCoordinate = targetCoordinate;
        transform.position = tile.transform.position;
        tile.ApplyEnterEffect(GetComponent<UnitStats>());
        return true;
    }
}
