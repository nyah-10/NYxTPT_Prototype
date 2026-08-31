#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SkillExampleAssets
{
    private const string FolderPath = "Assets/Skills/Examples";

    [MenuItem("Tools/Hex Roguelike/Create Example Skills")]
    public static void CreateExampleSkills()
    {
        EnsureFolder("Assets", "Skills");
        EnsureFolder("Assets/Skills", "Examples");

        CreateOrUpdate("SwordStrike", "검격", "SkillIcons/sword_strike", SkillActionSlot.Main, 1,
            "인접한 적 하나를 베어 [color=#FFB347]**3의 피해**[/color]를 줍니다.\n*기본 공격 스킬입니다.*",
            new SkillEffect { type = SkillEffectType.Damage, value = 3 });

        CreateOrUpdate("ArcaneBolt", "마력탄", "SkillIcons/arcane_bolt", SkillActionSlot.Main, 3,
            "최대 [color=#56D9FF]**3칸**[/color] 떨어진 적에게 마력탄을 발사해 **2의 피해**를 줍니다.",
            new SkillEffect { type = SkillEffectType.Damage, value = 2 });

        CreateOrUpdate("FirstAid", "응급 처치", "SkillIcons/first_aid", SkillActionSlot.Sub, 0,
            "자신의 체력을 [color=#79E879]**2 회복**[/color]합니다.\n*보조 행동을 소모합니다.*",
            new SkillEffect { type = SkillEffectType.Heal, value = 2 }, true);

        CreateOrUpdate("Leap", "도약", "SkillIcons/leap", SkillActionSlot.Sub, 3,
            "유닛과 중간 장애물을 무시하고 최대 [color=#56D9FF]**3칸**[/color] 도약합니다.",
            new SkillEffect { type = SkillEffectType.Jump, value = 3 });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created example skills in Assets/Skills/Examples.");
    }

    public static SkillDefinition GetDefaultMainSkill()
    {
        CreateExampleSkills();
        return AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            FolderPath + "/SwordStrike.asset");
    }

    public static SkillDefinition GetDefaultSubSkill()
    {
        CreateExampleSkills();
        return AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            FolderPath + "/FirstAid.asset");
    }

    private static void CreateOrUpdate(
        string fileName,
        string displayName,
        string iconResourcePath,
        SkillActionSlot actionSlot,
        int range,
        string description,
        SkillEffect effect,
        bool targetsSelf = false)
    {
        string path = FolderPath + "/" + fileName + ".asset";
        SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(skill, path);
        }

        skill.displayName = displayName;
        skill.description = description;
        skill.iconResourcePath = iconResourcePath;
        skill.actionSlot = actionSlot;
        skill.range = range;
        skill.areaRadius = 0;
        skill.targetsSelf = targetsSelf;
        skill.targetsAllies = false;
        skill.targetsEnemies = !targetsSelf;
        skill.effects = new[] { effect };
        EditorUtility.SetDirty(skill);
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
