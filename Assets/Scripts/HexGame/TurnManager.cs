using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RoundPhase { CardSelection, InitiativeReveal, Execution, BattleEnded }
public enum QueueEntryState { Pending, Acting, Completed, SkippedDead, SkippedDisabled }

public readonly struct TurnQueueItem
{
    public readonly MonoBehaviour Combatant;
    public readonly int Initiative;
    public readonly bool IsPlayer;
    public readonly QueueEntryState State;

    public TurnQueueItem(MonoBehaviour combatant, int initiative, bool isPlayer, QueueEntryState state)
    {
        Combatant = combatant;
        Initiative = initiative;
        IsPlayer = isPlayer;
        State = state;
    }
}

public class TurnManager : MonoBehaviour
{
    private sealed class QueueEntry
    {
        public MonoBehaviour Combatant;
        public int Initiative;
        public bool IsPlayer;
        public int RegistrationOrder;
        public QueueEntryState State;
    }

    public PlayerController player;
    public EnemyController enemy;
    [Header("Execution Timing")]
    [Min(0f)] public float turnTransitionDelay = 0.8f;
    public RoundPhase Phase { get; private set; }
    public bool IsPlayerTurn => Phase == RoundPhase.CardSelection || CurrentCombatant is PlayerController;
    public bool CanPlayerAct(PlayerController activePlayer) => Phase == RoundPhase.CardSelection || CurrentCombatant == activePlayer;
    public MonoBehaviour CurrentCombatant { get; private set; }
    public int RemainingEnemyCount { get; private set; }
    public IReadOnlyList<string> InitiativeOrder => initiativeOrder;
    public event System.Action QueueChanged;
    public event System.Action<string> FeedbackRequested;

    public void ReportFeedback(string message) => FeedbackRequested?.Invoke(message);

    private readonly List<EnemyController> enemies = new();
    private readonly List<PlayerController> players = new();
    private readonly HashSet<SkillLoadout> submittedPlayers = new();
    private readonly List<QueueEntry> executionQueue = new();
    private readonly List<string> initiativeOrder = new();
    private SkillLoadout playerSkills;
    private int queueIndex;
    private int registrationOrder;

    public List<TurnQueueItem> GetTurnQueueSnapshot()
    {
        List<TurnQueueItem> snapshot = new(executionQueue.Count);
        foreach (QueueEntry entry in executionQueue)
            snapshot.Add(new TurnQueueItem(entry.Combatant, entry.Initiative, entry.IsPlayer, entry.State));
        return snapshot;
    }

    private void Start()
    {
        if (FindAnyObjectByType<InitiativeOrderUI>() == null)
            new GameObject("Initiative Order UI").AddComponent<InitiativeOrderUI>();
        BeginCombat();
    }

    public void BeginCombat()
    {
        StopAllCoroutines();
        UnregisterCombatants();
        executionQueue.Clear();
        initiativeOrder.Clear();
        submittedPlayers.Clear();
        queueIndex = 0;
        registrationOrder = 0;
        CurrentCombatant = null;
        RegisterCombatants();
        playerSkills?.ClearPlan();
        StartCoroutine(BeginFirstRound());
    }

    private IEnumerator BeginFirstRound() { yield return null; BeginCardSelection(); }

    private void UnregisterCombatants()
    {
        foreach (PlayerController activePlayer in players)
            if (activePlayer != null && activePlayer.TryGetComponent(out UnitStats stats)) stats.Died -= HandlePlayerDeath;
        players.Clear();
        foreach (EnemyController activeEnemy in enemies)
            if (activeEnemy != null && activeEnemy.TryGetComponent(out UnitStats stats)) stats.Died -= HandleEnemyDeath;
        enemies.Clear();
    }

    private void RegisterCombatants()
    {
        foreach (PlayerController activePlayer in FindObjectsByType<PlayerController>())
        {
            players.Add(activePlayer);
            if (activePlayer.TryGetComponent(out UnitStats stats)) stats.Died += HandlePlayerDeath;
        }
        if (player == null && players.Count > 0) player = players[0];
        if (player != null) player.TryGetComponent(out playerSkills);

        foreach (EnemyController activeEnemy in FindObjectsByType<EnemyController>())
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
        submittedPlayers.Clear();
        foreach (PlayerController activePlayer in players)
        {
            if (activePlayer == null || !activePlayer.TryGetComponent(out UnitStats stats) || stats.IsDead) continue;
            activePlayer.GetComponent<SkillLoadout>()?.ClearPlan();
            activePlayer.GetComponent<ActionController>()?.StartTurn();
        }
        foreach (EnemyController activeEnemy in enemies)
            if (activeEnemy != null && activeEnemy.TryGetComponent(out UnitStats stats) && !stats.IsDead)
                activeEnemy.PrepareRoundAction();
        FeedbackRequested?.Invoke("플레이어 카드를 선택하세요");
        QueueChanged?.Invoke();
        Debug.Log("Card selection: choose the player's actions. Monster cards are ready to reveal.");
    }

    public void SubmitPlayerActionCard() => SubmitPlayerActionCard(playerSkills);

    public void SubmitPlayerActionCard(SkillLoadout submittingLoadout)
    {
        if (Phase != RoundPhase.CardSelection) return;
        if (submittingLoadout != null) submittedPlayers.Add(submittingLoadout);
        int livingPlayers = 0;
        foreach (PlayerController activePlayer in players)
            if (activePlayer != null && activePlayer.TryGetComponent(out UnitStats stats) && !stats.IsDead) livingPlayers++;
        if (submittedPlayers.Count < livingPlayers)
        {
            FeedbackRequested?.Invoke($"카드 선택 대기 중 ({submittedPlayers.Count}/{livingPlayers})");
            return;
        }
        BuildInitiativeQueue();
        StartCoroutine(RevealAndExecute());
    }

    private void BuildInitiativeQueue()
    {
        Phase = RoundPhase.InitiativeReveal;
        executionQueue.Clear();
        foreach (PlayerController activePlayer in players)
        {
            if (activePlayer == null || !activePlayer.TryGetComponent(out UnitStats stats) || stats.IsDead) continue;
            SkillLoadout skills = activePlayer.GetComponent<SkillLoadout>();
            executionQueue.Add(new QueueEntry { Combatant = activePlayer, Initiative = skills == null ? 99 : skills.GetPlannedInitiative(), IsPlayer = true, RegistrationOrder = ++registrationOrder, State = QueueEntryState.Pending });
        }

        foreach (EnemyController activeEnemy in enemies)
        {
            if (activeEnemy == null || !activeEnemy.TryGetComponent(out UnitStats stats) || stats.IsDead) continue;
            executionQueue.Add(new QueueEntry { Combatant = activeEnemy, Initiative = activeEnemy.CurrentCardInitiative, RegistrationOrder = ++registrationOrder, State = QueueEntryState.Pending });
        }

        executionQueue.Sort(CompareQueueEntries);

        initiativeOrder.Clear();
        foreach (QueueEntry entry in executionQueue)
            initiativeOrder.Add($"{entry.Combatant.name} {entry.Initiative}");
        QueueChanged?.Invoke();
    }

    private static int CompareQueueEntries(QueueEntry a, QueueEntry b)
    {
        int byInitiative = a.Initiative.CompareTo(b.Initiative);
        if (byInitiative != 0) return byInitiative;

        // Keep tie rules isolated so later initiative-rule changes do not affect queue construction.
        if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
        return a.RegistrationOrder.CompareTo(b.RegistrationOrder);
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
            entry.State = QueueEntryState.SkippedDead;
            FeedbackRequested?.Invoke($"{entry.Combatant?.name ?? "유닛"}: 사망으로 턴 건너뜀");
            QueueChanged?.Invoke();
            ProceedToNextQueueEntry();
            return;
        }

        CurrentCombatant = entry.Combatant;
        entry.State = QueueEntryState.Acting;
        FeedbackRequested?.Invoke($"{entry.Combatant.name}의 턴");
        QueueChanged?.Invoke();
        if (stats.IsStunned)
        {
            stats.BeginTurn();
            entry.State = QueueEntryState.SkippedDisabled;
            FeedbackRequested?.Invoke($"{entry.Combatant.name}: 행동 불가로 턴 건너뜀");
            QueueChanged?.Invoke();
            ProceedToNextQueueEntry();
            return;
        }

        if (entry.IsPlayer)
        {
            SkillLoadout skills = entry.Combatant.GetComponent<SkillLoadout>();
            if (skills == null || !skills.HasPlannedActions) { CompleteCurrentEntry(); ProceedToNextQueueEntry(); return; }
            skills.ExecutePlan(FindAnyObjectByType<HexGridManager>());
            StartCoroutine(WaitForPlayerActions(skills));
            return;
        }

        StartCoroutine(ExecuteEnemy((EnemyController)entry.Combatant));
    }

    private IEnumerator WaitForPlayerActions(SkillLoadout skills)
    {
        yield return new WaitUntil(() => skills == null || !skills.IsExecutingPlan);
        CompleteCurrentEntry();
        ProceedToNextQueueEntry();
    }

    private IEnumerator ExecuteEnemy(EnemyController activeEnemy)
    {
        PlayerController targetablePlayer = FindLivingPlayer();
        if (targetablePlayer != null) yield return activeEnemy.TakeTurn(targetablePlayer);
        CompleteCurrentEntry();
        ProceedToNextQueueEntry();
    }

    private void ProceedToNextQueueEntry()
    {
        StartCoroutine(ProceedAfterTurnDelay());
    }

    private IEnumerator ProceedAfterTurnDelay()
    {
        if (turnTransitionDelay > 0f)
            yield return new WaitForSeconds(turnTransitionDelay);

        ExecuteNextQueueEntry();
    }

    private void CompleteCurrentEntry()
    {
        foreach (QueueEntry entry in executionQueue)
            if (entry.Combatant == CurrentCombatant && entry.State == QueueEntryState.Acting)
            {
                entry.State = QueueEntryState.Completed;
                break;
            }
        QueueChanged?.Invoke();
    }

    // Kept for the existing HUD button; it now locks the selected card and starts the round.
    public void EndPlayerTurn() => SubmitPlayerActionCard();

    private void HandleEnemyDeath(UnitStats stats)
    {
        stats.gameObject.SetActive(false);
        RemainingEnemyCount = CountLivingEnemies();
        if (RemainingEnemyCount == 0) EndBattle(true);
    }

    private void HandlePlayerDeath(UnitStats stats)
    {
        if (FindLivingPlayer() == null) EndBattle(false);
    }

    private PlayerController FindLivingPlayer()
    {
        foreach (PlayerController activePlayer in players)
            if (activePlayer != null && activePlayer.TryGetComponent(out UnitStats stats) && !stats.IsDead) return activePlayer;
        return null;
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
