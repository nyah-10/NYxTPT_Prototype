using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ActionController))]
public class SkillLoadout : MonoBehaviour
{
    public SkillDefinition[] mainSkills = new SkillDefinition[2];
    public SkillDefinition[] subSkills = new SkillDefinition[2];

    private ActionController actionController;
    private PlayerController player;
    private readonly List<PlannedAction> plannedActions = new();

    private readonly struct PlannedAction
    {
        public readonly SkillDefinition Skill;
        public readonly Vector2Int Target;

        public PlannedAction(SkillDefinition skill, Vector2Int target)
        {
            Skill = skill;
            Target = target;
        }
    }

    public bool HasPlannedActions => plannedActions.Count > 0;
    public bool HasCompletePlan => HasPlannedSlot(SkillActionSlot.Main) && HasPlannedSlot(SkillActionSlot.Sub);
    public bool IsExecutingPlan { get; private set; }

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
        if (!skill.targetsSelf && HexGridManager.HexDistance(source, targetCoordinate) > skill.range)
            return false;

        UnitStats target = FindUnitAt(targetCoordinate);
        if ((skill.targetsEnemies || skill.targetsAllies) && target == null && !HasMovementEffect(skill))
            return false;

        Spend(skill);
        foreach (SkillEffect effect in skill.effects)
            ApplyEffect(effect, target, targetCoordinate, source, grid);
        PlayParticleEffect(skill, source, targetCoordinate, grid);

        return true;
    }

    public bool Plan(SkillDefinition skill, Vector2Int targetCoordinate, HexGridManager grid)
    {
        if (skill == null || grid == null || !CanUse(skill) || !grid.TryGetTile(targetCoordinate, out _)) return false;

        Vector2Int source = GetPlanningSource();
        if (!skill.targetsSelf && HexGridManager.HexDistance(source, targetCoordinate) > skill.range) return false;

        UnitStats target = FindUnitAt(targetCoordinate);
        if ((skill.targetsEnemies || skill.targetsAllies) && target == null && !HasMovementEffect(skill)) return false;

        plannedActions.Add(new PlannedAction(skill, targetCoordinate));
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
            CommitPlanned(action, grid);
            // A movement-first plan must visually arrive before its following attack fires.
            if (HasMovementEffect(action.Skill))
                yield return new WaitUntil(() => !player.IsMoving);
        }
        IsExecutingPlan = false;
    }

    public void ClearPlan() => plannedActions.Clear();

    public Vector2Int GetPlanningSource()
    {
        Vector2Int source = player.CurrentCoordinate;
        foreach (PlannedAction action in plannedActions)
            if (HasMovementEffect(action.Skill)) source = action.Target;
        return source;
    }

    public bool HasPlannedSlot(SkillActionSlot slot)
    {
        foreach (PlannedAction action in plannedActions)
            if (action.Skill.actionSlot == slot) return true;
        return false;
    }

    public bool CanUse(SkillDefinition skill) => skill != null && !IsExecutingPlan && !HasPlannedSlot(skill.actionSlot) && (skill.actionSlot == SkillActionSlot.Main
        ? actionController.MainActionPoint > 0 : actionController.SubActionPoint > 0);

    private void CommitPlanned(PlannedAction action, HexGridManager grid)
    {
        Vector2Int source = player.CurrentCoordinate;
        UnitStats target = FindUnitAt(action.Target);
        Spend(action.Skill);
        foreach (SkillEffect effect in action.Skill.effects)
            ApplyEffect(effect, target, action.Target, source, grid);
        PlayParticleEffect(action.Skill, source, action.Target, grid);
    }

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

    private void ApplyEffect(SkillEffect effect, UnitStats target, Vector2Int targetCoordinate, Vector2Int source, HexGridManager grid)
    {
        switch (effect.type)
        {
            case SkillEffectType.Damage: target?.TakeDamage(effect.value); break;
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
        foreach (UnitStats stats in FindObjectsByType<UnitStats>(FindObjectsSortMode.None))
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
        if (grid.TryGetTile(destination, out _))
        {
            if (player != null) player.TryMoveTo(destination);
            else enemy.ForceMoveTo(destination);
        }
    }
}
