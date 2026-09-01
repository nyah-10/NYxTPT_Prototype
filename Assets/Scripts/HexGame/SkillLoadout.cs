using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ActionController))]
public class SkillLoadout : MonoBehaviour
{
    public SkillDefinition[] mainSkills = new SkillDefinition[2];
    public SkillDefinition[] subSkills = new SkillDefinition[2];

    private ActionController actionController;
    private PlayerController player;
    private readonly List<PlannedAction> plannedActions = new();
    private SkillDefinition leadingCard;

    private readonly struct PlannedAction
    {
        public readonly SkillDefinition Skill;

        public PlannedAction(SkillDefinition skill)
        {
            Skill = skill;
        }
    }

    public bool HasPlannedActions => plannedActions.Count > 0;
    public bool HasCompletePlan => HasPlannedSlot(SkillActionSlot.Main) && HasPlannedSlot(SkillActionSlot.Sub);
    public bool HasLeadingCard => leadingCard != null;
    public bool IsExecutingPlan { get; private set; }
    public event System.Action<string> FeedbackRequested;

    public int GetPlannedInitiative()
    {
        return leadingCard == null ? 99 : Mathf.Max(1, leadingCard.initiative);
    }

    public bool HasPlannedSkill(SkillDefinition skill)
    {
        foreach (PlannedAction action in plannedActions)
            if (action.Skill == skill) return true;
        return false;
    }

    public bool IsLeadingCard(SkillDefinition skill) => leadingCard == skill;

    public bool SetLeadingCard(SkillDefinition skill)
    {
        if (!HasPlannedSkill(skill)) return false;
        leadingCard = skill;
        return true;
    }

    private void Awake()
    {
        actionController = GetComponent<ActionController>();
        player = GetComponent<PlayerController>();
    }

    public bool Commit(SkillDefinition skill, Vector2Int targetCoordinate, HexGridManager grid)
    {
        if (skill == null || grid == null || !CanUse(skill)) return false;
        if (!grid.TryGetTile(targetCoordinate, out _)) return false;

        Vector2Int source = player.CurrentCoordinate;
        if (!skill.targetsSelf && !grid.CanTarget(source, targetCoordinate, skill.range))
            return false;

        UnitStats target = FindUnitAt(targetCoordinate);
        bool terrainTarget = skill.canDestroyTerrain && grid.TryGetTile(targetCoordinate, out HexTile terrainTile) && terrainTile.IsDestructible;
        if ((skill.targetsEnemies || skill.targetsAllies) && target == null && !terrainTarget && !HasMovementEffect(skill))
            return false;

        Spend(skill);
        foreach (SkillEffect effect in skill.effects)
            ApplyEffect(skill, effect, target, targetCoordinate, source, grid);
        PlayParticleEffect(skill, source, targetCoordinate, grid);

        return true;
    }

    public bool Plan(SkillDefinition skill, Vector2Int targetCoordinate, HexGridManager grid)
    {
        if (skill == null || grid == null || !CanUse(skill)) return false;
        // Planning reserves only the card and order. Board choices belong to execution.
        plannedActions.Add(new PlannedAction(skill));
        return true;
    }

    public void ExecutePlan(HexGridManager grid)
    {
        if (grid == null || plannedActions.Count == 0) return;
        List<PlannedAction> executionQueue = new(plannedActions);
        plannedActions.Clear();
        IsExecutingPlan = true;
        StartCoroutine(ExecutePlanInOrder(executionQueue, grid));
    }

    private IEnumerator ExecutePlanInOrder(List<PlannedAction> executionQueue, HexGridManager grid)
    {
        foreach (PlannedAction action in executionQueue)
        {
            Vector2Int targetCoordinate = player.CurrentCoordinate;
            if (HasMovementEffect(action.Skill))
            {
                List<Vector2Int> destinations = FindValidDestinations(grid, player.CurrentCoordinate, action.Skill.range);
                if (destinations.Count == 0)
                {
                    FeedbackRequested?.Invoke("이동할 칸 없음");
                    continue;
                }

                FeedbackRequested?.Invoke("이동할 칸을 선택하세요");
                grid.SetHighlights(destinations, new Color(.15f, .8f, 1f, .8f));
                Vector2Int? selectedDestination = null;
                yield return WaitForTileSelection(destinations, result => selectedDestination = result);
                grid.ClearHighlights();
                if (!selectedDestination.HasValue) continue;
                targetCoordinate = selectedDestination.Value;
            }
            else if (RequiresExecutionTarget(action.Skill))
            {
                List<UnitStats> candidates = FindValidTargets(action.Skill, player.CurrentCoordinate, grid);
                List<Vector2Int> terrainCandidates = FindDestructibleTerrain(action.Skill, player.CurrentCoordinate, grid);
                if (terrainCandidates.Count > 0)
                {
                    List<Vector2Int> selectableCoordinates = CoordinatesOf(candidates);
                    selectableCoordinates.AddRange(terrainCandidates);
                    FeedbackRequested?.Invoke("공격할 대상 또는 지형을 선택하세요");
                    grid.SetHighlights(selectableCoordinates, new Color(1f, .45f, .08f, .8f));
                    Vector2Int? selectedCoordinate = null;
                    yield return WaitForTileSelection(selectableCoordinates, result => selectedCoordinate = result);
                    grid.ClearHighlights();
                    if (!selectedCoordinate.HasValue) continue;
                    targetCoordinate = selectedCoordinate.Value;
                    CommitPlanned(action.Skill, targetCoordinate, grid);
                    continue;
                }
                if (candidates.Count == 0)
                {
                    FeedbackRequested?.Invoke("타겟 없음");
                    Debug.Log($"{name}: {action.Skill.displayName} - 타겟 없음");
                    continue;
                }

                UnitStats selected = candidates.Count == 1 ? candidates[0] : null;
                if (selected == null)
                {
                    FeedbackRequested?.Invoke("공격할 대상을 선택하세요");
                    grid.SetHighlights(CoordinatesOf(candidates), new Color(1f, .25f, .12f, .75f));
                    yield return WaitForTargetSelection(candidates, result => selected = result);
                    grid.ClearHighlights();
                }

                if (selected == null || selected.IsDead) continue;
                targetCoordinate = CoordinateOf(selected);
            }

            CommitPlanned(action.Skill, targetCoordinate, grid);
            // A movement-first plan must visually arrive before its following attack fires.
            if (HasMovementEffect(action.Skill))
                yield return new WaitUntil(() => !player.IsMoving);
        }
        IsExecutingPlan = false;
    }

    public void ClearPlan()
    {
        plannedActions.Clear();
        leadingCard = null;
    }

    public Vector2Int GetPlanningSource() => player.CurrentCoordinate;

    public bool HasPlannedSlot(SkillActionSlot slot)
    {
        foreach (PlannedAction action in plannedActions)
            if (action.Skill.actionSlot == slot) return true;
        return false;
    }

    public bool CanUse(SkillDefinition skill) => skill != null && !IsExecutingPlan &&
        (player.turnManager == null || player.turnManager.CanPlayerAct(player)) &&
        !HasPlannedSlot(skill.actionSlot) && (skill.actionSlot == SkillActionSlot.Main
        ? actionController.MainActionPoint > 0 : actionController.SubActionPoint > 0);

    private void CommitPlanned(SkillDefinition skill, Vector2Int targetCoordinate, HexGridManager grid)
    {
        Vector2Int source = player.CurrentCoordinate;
        UnitStats target = FindUnitAt(targetCoordinate);
        Spend(skill);
        foreach (SkillEffect effect in skill.effects)
            ApplyEffect(skill, effect, target, targetCoordinate, source, grid);
        PlayParticleEffect(skill, source, targetCoordinate, grid);
    }

    private IEnumerator WaitForTargetSelection(List<UnitStats> candidates, System.Action<UnitStats> select)
    {
        while (true)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Vector3 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                RaycastHit2D hit = Physics2D.Raycast(point, Vector2.zero);
                HexTile tile = hit.collider == null ? null : hit.collider.GetComponent<HexTile>();
                UnitStats clicked = tile == null ? null : FindUnitAt(tile.Coordinate);
                if (clicked != null && candidates.Contains(clicked) && !clicked.IsDead)
                {
                    select(clicked);
                    yield break;
                }
            }
            yield return null;
        }
    }

    private IEnumerator WaitForTileSelection(List<Vector2Int> candidates, System.Action<Vector2Int> select)
    {
        while (true)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Vector3 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                RaycastHit2D hit = Physics2D.Raycast(point, Vector2.zero);
                HexTile tile = hit.collider == null ? null : hit.collider.GetComponent<HexTile>();
                if (tile != null && candidates.Contains(tile.Coordinate))
                {
                    select(tile.Coordinate);
                    yield break;
                }
            }
            yield return null;
        }
    }

    private static List<Vector2Int> FindValidDestinations(HexGridManager grid, Vector2Int source, int range)
    {
        List<Vector2Int> result = grid.GetReachableCoordinates(source, range);
        result.Remove(source);
        result.RemoveAll(coordinate => FindUnitAt(coordinate) != null);
        return result;
    }

    private List<UnitStats> FindValidTargets(SkillDefinition skill, Vector2Int source, HexGridManager grid)
    {
        List<UnitStats> result = new();
        foreach (UnitStats stats in FindObjectsByType<UnitStats>())
        {
            if (stats == null || stats.IsDead || stats.gameObject == gameObject) continue;
            bool isEnemy = stats.GetComponent<EnemyController>() != null;
            if (isEnemy != skill.targetsEnemies || !grid.CanTarget(source, CoordinateOf(stats), skill.range)) continue;
            result.Add(stats);
        }
        return result;
    }

    private static List<Vector2Int> FindDestructibleTerrain(SkillDefinition skill, Vector2Int source, HexGridManager grid)
    {
        List<Vector2Int> result = new();
        if (!skill.canDestroyTerrain) return result;
        foreach (Vector2Int coordinate in grid.GetCoordinatesInRange(source, skill.range + 1))
            if (grid.CanTarget(source, coordinate, skill.range) && grid.TryGetTile(coordinate, out HexTile tile) && tile.IsDestructible)
                result.Add(coordinate);
        return result;
    }

    private static List<Vector2Int> CoordinatesOf(List<UnitStats> units)
    {
        List<Vector2Int> result = new(units.Count);
        foreach (UnitStats unit in units) result.Add(CoordinateOf(unit));
        return result;
    }

    private static Vector2Int CoordinateOf(UnitStats stats)
    {
        PlayerController targetPlayer = stats.GetComponent<PlayerController>();
        if (targetPlayer != null) return targetPlayer.CurrentCoordinate;
        return stats.GetComponent<EnemyController>().CurrentCoordinate;
    }

    private static bool RequiresExecutionTarget(SkillDefinition skill) =>
        skill != null && !skill.targetsSelf && (skill.targetsEnemies || skill.targetsAllies) && !HasMovementEffect(skill);

    private void Spend(SkillDefinition skill)
    {
        if (skill.actionSlot == SkillActionSlot.Main) actionController.UseMainSkill();
        else actionController.UseMoveAction();
    }

    private static void PlayParticleEffect(SkillDefinition skill, Vector2Int source, Vector2Int target, HexGridManager grid)
    {
        if (!grid.TryGetTile(source, out HexTile sourceTile) || !grid.TryGetTile(target, out HexTile targetTile)) return;
        SkillParticleEffects.Play(skill, sourceTile.transform.position, targetTile.transform.position);
    }

    private void ApplyEffect(SkillDefinition skill, SkillEffect effect, UnitStats target, Vector2Int targetCoordinate, Vector2Int source, HexGridManager grid)
    {
        switch (effect.type)
        {
            case SkillEffectType.Damage:
                if (target != null) target.TakeDamage(effect.value, GetComponent<UnitStats>());
                else if (skill.canDestroyTerrain && grid.TryGetTile(targetCoordinate, out HexTile terrain)) terrain.TakeTerrainDamage(effect.value);
                break;
            case SkillEffectType.Heal: (target ?? GetComponent<UnitStats>())?.RestoreHealth(effect.value); break;
            case SkillEffectType.Shield: (target ?? GetComponent<UnitStats>())?.AddShield(effect.value, effect.duration); break;
            case SkillEffectType.Stun: target?.AddStun(effect.duration); break;
            case SkillEffectType.Immobilize: target?.AddImmobilize(effect.duration); break;
            case SkillEffectType.Move:
            case SkillEffectType.Jump:
                if (!GetComponent<UnitStats>().IsImmobilized) player.TryMoveTo(targetCoordinate);
                break;
            case SkillEffectType.Push: MoveTarget(target, targetCoordinate - source, effect.value, grid); break;
            case SkillEffectType.Pull: MoveTarget(target, source - targetCoordinate, effect.value, grid); break;
        }
    }

    private static bool HasMovementEffect(SkillDefinition skill)
    {
        foreach (SkillEffect effect in skill.effects)
            if (effect.type == SkillEffectType.Move || effect.type == SkillEffectType.Jump) return true;
        return false;
    }

    private static UnitStats FindUnitAt(Vector2Int coordinate)
    {
        foreach (UnitStats stats in FindObjectsByType<UnitStats>())
        {
            PlayerController player = stats.GetComponent<PlayerController>();
            EnemyController enemy = stats.GetComponent<EnemyController>();
            if ((player != null && player.CurrentCoordinate == coordinate) ||
                (enemy != null && enemy.CurrentCoordinate == coordinate)) return stats;
        }
        return null;
    }

    private static void MoveTarget(UnitStats target, Vector2Int direction, int distance, HexGridManager grid)
    {
        if (target == null || direction == Vector2Int.zero) return;
        direction = new Vector2Int(Mathf.Clamp(direction.x, -1, 1), Mathf.Clamp(direction.y, -1, 1));
        PlayerController player = target.GetComponent<PlayerController>();
        EnemyController enemy = target.GetComponent<EnemyController>();
        Vector2Int start = player != null ? player.CurrentCoordinate : enemy.CurrentCoordinate;
        Vector2Int destination = start + direction * distance;
        if (grid.TryGetTile(destination, out HexTile destinationTile) && !destinationTile.BlocksMovement)
        {
            if (player != null) player.TryMoveTo(destination);
            else enemy.ForceMoveTo(destination);
        }
    }
}
