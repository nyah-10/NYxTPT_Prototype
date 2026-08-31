using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RoundPhase { CardSelection, InitiativeReveal, Execution, BattleEnded }

public class TurnManager : MonoBehaviour
{
    private sealed class QueueEntry
    {
        public MonoBehaviour Combatant;
        public int Initiative;
        public bool IsPlayer;
    }

    public PlayerController player;
    public EnemyController enemy;
    public RoundPhase Phase { get; private set; }
    public bool IsPlayerTurn => Phase == RoundPhase.CardSelection || CurrentCombatant == player;
    public MonoBehaviour CurrentCombatant { get; private set; }
    public int RemainingEnemyCount { get; private set; }
    public IReadOnlyList<string> InitiativeOrder => initiativeOrder;

    private readonly List<EnemyController> enemies = new();
    private readonly List<QueueEntry> executionQueue = new();
    private readonly List<string> initiativeOrder = new();
    private UnitStats playerStats;
    private SkillLoadout playerSkills;
    private int queueIndex;

    private void Start()
    {
        RegisterCombatants();
        if (FindFirstObjectByType<InitiativeOrderUI>() == null)
            new GameObject("Initiative Order UI").AddComponent<InitiativeOrderUI>();
        StartCoroutine(BeginFirstRound());
    }

    private IEnumerator BeginFirstRound()
    {
        yield return null;
        BeginCardSelection();
    }

    private void RegisterCombatants()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.TryGetComponent(out playerStats);
            player.TryGetComponent(out playerSkills);
            if (playerStats != null) playerStats.Died += _ => EndBattle(false);
        }

        foreach (EnemyController activeEnemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            enemies.Add(activeEnemy);
            if (activeEnemy.TryGetComponent(out UnitStats stats)) stats.Died += HandleEnemyDeath;
        }
        RemainingEnemyCount = CountLivingEnemies();
    }

    private void BeginCardSelection()
    {
        if (Phase == RoundPhase.BattleEnded) return;
        Phase = RoundPhase.CardSelection;
        CurrentCombatant = null;
        executionQueue.Clear();
        initiativeOrder.Clear();
        playerSkills?.ClearPlan();
        if (player != null && player.TryGetComponent(out ActionController actions)) actions.StartTurn();
        foreach (EnemyController activeEnemy in enemies)
            if (activeEnemy != null && activeEnemy.TryGetComponent(out UnitStats stats) && !stats.IsDead)
                activeEnemy.PrepareRoundAction();
        Debug.Log("Card selection: choose the player's actions. Monster cards are ready to reveal.");
    }

    public void SubmitPlayerActionCard()
    {
        if (Phase != RoundPhase.CardSelection) return;
        BuildInitiativeQueue(playerSkills == null ? 99 : playerSkills.GetPlannedInitiative());
        StartCoroutine(RevealAndExecute());
    }

    private void BuildInitiativeQueue(int playerInitiative)
    {
        Phase = RoundPhase.InitiativeReveal;
        executionQueue.Clear();
        if (player != null && playerStats != null && !playerStats.IsDead)
            executionQueue.Add(new QueueEntry { Combatant = player, Initiative = playerInitiative, IsPlayer = true });

        foreach (EnemyController activeEnemy in enemies)
        {
            if (activeEnemy == null || !activeEnemy.TryGetComponent(out UnitStats stats) || stats.IsDead) continue;
            executionQueue.Add(new QueueEntry { Combatant = activeEnemy, Initiative = activeEnemy.CurrentCardInitiative });
        }

        // A deterministic player-first tie break prevents confusing order changes.
        executionQueue.Sort((a, b) =>
        {
            int byInitiative = a.Initiative.CompareTo(b.Initiative);
            if (byInitiative != 0) return byInitiative;
            if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
            return a.Combatant.GetInstanceID().CompareTo(b.Combatant.GetInstanceID());
        });

        initiativeOrder.Clear();
        foreach (QueueEntry entry in executionQueue)
            initiativeOrder.Add($"{entry.Combatant.name} {entry.Initiative}");
    }

    private IEnumerator RevealAndExecute()
    {
        yield return new WaitForSeconds(.35f);
        Phase = RoundPhase.Execution;
        queueIndex = 0;
        ExecuteNextQueueEntry();
    }

    private void ExecuteNextQueueEntry()
    {
        if (Phase == RoundPhase.BattleEnded) return;
        if (queueIndex >= executionQueue.Count) { BeginCardSelection(); return; }

        QueueEntry entry = executionQueue[queueIndex++];
        if (entry.Combatant == null || !entry.Combatant.TryGetComponent(out UnitStats stats) || stats.IsDead)
        {
            ExecuteNextQueueEntry();
            return;
        }

        CurrentCombatant = entry.Combatant;
        if (stats.IsStunned)
        {
            stats.BeginTurn();
            ExecuteNextQueueEntry();
            return;
        }

        if (entry.IsPlayer)
        {
            if (playerSkills == null || !playerSkills.HasPlannedActions) { ExecuteNextQueueEntry(); return; }
            playerSkills.ExecutePlan(FindFirstObjectByType<HexGridManager>());
            StartCoroutine(WaitForPlayerActions());
            return;
        }

        StartCoroutine(ExecuteEnemy((EnemyController)entry.Combatant));
    }

    private IEnumerator WaitForPlayerActions()
    {
        yield return new WaitUntil(() => !playerSkills.IsExecutingPlan);
        ExecuteNextQueueEntry();
    }

    private IEnumerator ExecuteEnemy(EnemyController activeEnemy)
    {
        if (player != null && playerStats != null && !playerStats.IsDead) yield return activeEnemy.TakeTurn(player);
        ExecuteNextQueueEntry();
    }

    // Kept for the existing HUD button; it now locks the selected card and starts the round.
    public void EndPlayerTurn() => SubmitPlayerActionCard();

    private void HandleEnemyDeath(UnitStats stats)
    {
        stats.gameObject.SetActive(false);
        RemainingEnemyCount = CountLivingEnemies();
        if (RemainingEnemyCount == 0) EndBattle(true);
    }

    private int CountLivingEnemies()
    {
        int count = 0;
        foreach (EnemyController activeEnemy in enemies)
            if (activeEnemy != null && activeEnemy.TryGetComponent(out UnitStats stats) && !stats.IsDead) count++;
        return count;
    }

    private void EndBattle(bool victory)
    {
        Phase = RoundPhase.BattleEnded;
        CurrentCombatant = null;
        Debug.Log(victory ? "Victory!" : "Battle lost.");
    }
}
