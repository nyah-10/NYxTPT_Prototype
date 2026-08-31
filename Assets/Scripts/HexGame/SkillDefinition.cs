using UnityEngine;

public enum SkillActionSlot
{
    Main,
    Sub
}

public enum SkillEffectType
{
    Damage,
    Heal,
    Push,
    Pull,
    Stun,
    Immobilize,
    Move,
    Jump,
    Shield
}

[System.Serializable]
public class SkillEffect
{
    public SkillEffectType type;

    [Tooltip("Damage, healing, movement distance, push/pull distance, or shield amount.")]
    [Min(0)] public int value;

    [Tooltip("Number of turns for a status effect. Ignored by instant effects.")]
    [Min(0)] public int duration;
}

[CreateAssetMenu(
    fileName = "Skill_",
    menuName = "Hex Roguelike/Skill Definition"
)]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    [TextArea(3, 8), Tooltip("Supports **bold**, *italic*, [color=#RRGGBB]text[/color], and Unity Rich Text tags.")]
    public string description;
    [Tooltip("Optional high-resolution icon shown by the skill HUD.")]
    public Sprite icon;
    [Tooltip("Resources-relative icon path used when no Sprite is assigned, for example SkillIcons/sword_strike.")]
    public string iconResourcePath;
    public SkillActionSlot actionSlot;

    [Header("Targeting")]
    [Min(0)] public int range = 1;
    [Min(0)] public int areaRadius;
    public bool targetsSelf;
    public bool targetsAllies;
    public bool targetsEnemies = true;

    [Header("Effects")]
    public SkillEffect[] effects;
}
