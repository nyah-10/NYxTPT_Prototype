using System;
using UnityEngine;

public class UnitStats : MonoBehaviour
{
    [Min(1)] public int MaxHP = 10;
    public int CurrentHP { get; private set; }

    private bool isDead;
    private int shieldAmount;
    private int shieldTurns;
    private int stunTurns;
    private int immobilizeTurns;

    public bool IsStunned => stunTurns > 0;
    public bool IsImmobilized => immobilizeTurns > 0;
    public bool IsDead => isDead;
    public event Action<UnitStats> Died;

    private void Awake()
    {
        CurrentHP = MaxHP;
    }

    public void TakeDamage(int amount)
    {
        if (isDead || amount <= 0)
            return;

        CurrentHP = Mathf.Max(0, CurrentHP - Mathf.Max(0, amount - shieldAmount));

        if (CurrentHP <= 0)
            Die();
    }

    public void AddShield(int amount, int duration)
    {
        shieldAmount = Mathf.Max(shieldAmount, amount);
        shieldTurns = Mathf.Max(shieldTurns, duration);
    }

    public void AddStun(int duration) => stunTurns = Mathf.Max(stunTurns, duration);
    public void AddImmobilize(int duration) => immobilizeTurns = Mathf.Max(immobilizeTurns, duration);

    public void BeginTurn()
    {
        if (shieldTurns > 0 && --shieldTurns == 0) shieldAmount = 0;
        if (stunTurns > 0) stunTurns--;
        if (immobilizeTurns > 0) immobilizeTurns--;
    }

    public void RestoreHealth(int amount)
    {
        if (isDead || amount <= 0)
            return;

        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }

    public virtual void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log($"{name} died.");
        Died?.Invoke(this);
    }
}
