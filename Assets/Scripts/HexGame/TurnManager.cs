using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public PlayerController player;
    public EnemyController enemy;

    public bool IsPlayerTurn { get; private set; } = true;
    public int RemainingEnemyCount { get; private set; }

    private readonly List<EnemyController> enemies = new List<EnemyController>();
    private UnitStats playerStats;
    private bool battleEnded;

    private void Start()
    {
        RegisterCombatants();
        StartCoroutine(BeginFirstPlayerTurn());
    }

    private void RegisterCombatants()
    {
        enemies.AddRange(FindObjectsByType<EnemyController>(FindObjectsSortMode.None));
        if (enemy != null && !enemies.Contains(enemy)) enemies.Add(enemy);

        RemainingEnemyCount = 0;
        foreach (EnemyController registeredEnemy in enemies)
        {
            if (!registeredEnemy.TryGetComponent(out UnitStats stats) || stats.IsDead) continue;
            RemainingEnemyCount++;
            stats.Died += HandleEnemyDefeated;
        }

        if (player != null && player.TryGetComponent(out playerStats))
            playerStats.Died += HandlePlayerDeath;

        Debug.Log($"Battle started with {RemainingEnemyCount} enemies.");
    }

    private IEnumerator BeginFirstPlayerTurn()
    {
        // Unit Start methods must set their initial grid coordinates first.
        yield return null;
        BeginPlayerTurn();
    }

    private void BeginPlayerTurn()
    {
        if (battleEnded) return;

        IsPlayerTurn = true;
        if (player != null && player.TryGetComponent(out ActionController actionController))
            actionController.StartTurn();

        Debug.Log("Player turn: move one tile.");
    }

    public void EndPlayerTurn()
    {
        if (!IsPlayerTurn || battleEnded)
            return;

        IsPlayerTurn = false;
        StartCoroutine(EnemyTurn());
    }

    private IEnumerator EnemyTurn()
    {
        Debug.Log("Enemy turn.");

        if (player != null)
        {
            foreach (EnemyController activeEnemy in enemies)
            {
                if (battleEnded) yield break;
                if (activeEnemy != null && activeEnemy.TryGetComponent(out UnitStats stats) && !stats.IsDead)
                    yield return activeEnemy.TakeTurn(player);
            }
        }

        BeginPlayerTurn();
    }

    public void HandlePlayerDeath(UnitStats defeatedPlayer)
    {
        if (battleEnded) return;
        battleEnded = true;
        IsPlayerTurn = false;
        Debug.Log("Player defeated. Battle lost.");
    }

    public void HandleEnemyDefeated(UnitStats defeatedEnemy)
    {
        if (battleEnded) return;

        defeatedEnemy.Died -= HandleEnemyDefeated;
        RemainingEnemyCount = Mathf.Max(0, RemainingEnemyCount - 1);
        Debug.Log($"Enemy defeated. {RemainingEnemyCount} enemies remain.");
        defeatedEnemy.gameObject.SetActive(false);

        if (RemainingEnemyCount == 0)
        {
            battleEnded = true;
            IsPlayerTurn = false;
            Debug.Log("All enemies defeated. Victory!");
        }
    }
}
