using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatRole { Damage, Tank, Support, Healer }

public class UnitStats : MonoBehaviour
{
    [Min(1)] public int MaxHP = 10;
    public CombatRole Role;
    public int CurrentHP { get; private set; }

    private bool isDead;
    private int shieldAmount;
    private int shieldTurns;
    private int stunTurns;
    private int immobilizeTurns;
    private readonly Dictionary<UnitStats, int> damageBySource = new();

    public bool IsStunned => stunTurns > 0;
    public bool IsImmobilized => immobilizeTurns > 0;
    public bool IsDead => isDead;
    public event Action<UnitStats> Died;

    private void Awake()
    {
        CurrentHP = MaxHP;
    }

    public void TakeDamage(int amount, UnitStats source = null)
    {
        if (isDead || amount <= 0)
            return;

        int appliedDamage = Mathf.Max(0, amount - shieldAmount);
        CurrentHP = Mathf.Max(0, CurrentHP - appliedDamage);
        if (source != null && appliedDamage > 0)
        {
            damageBySource[source] = GetThreatFrom(source) + appliedDamage;
        }

        if (CurrentHP <= 0)
            Die();
    }

    public int GetThreatFrom(UnitStats source) => source != null && damageBySource.TryGetValue(source, out int value) ? value : 0;

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
