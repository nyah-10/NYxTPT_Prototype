using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class SkillActionUI : MonoBehaviour
{
    private static readonly Color MainColor = new(1f, .43f, .12f, 1f);
    private static readonly Color SubColor = new(.08f, .76f, 1f, 1f);
    private static readonly Vector2Int InvalidTarget = new(-999, -999);
    public SkillLoadout playerSkills;
    public HexGridManager gridManager;

    private readonly List<SkillCardView> cards = new();
    private SkillDefinition selectedSkill;
    private Vector2Int selectedTarget = InvalidTarget;
    private Text status, detail;
    private Button confirmButton;
    private GameObject movementGhost;
    private SkillHandLayout handLayout;

    private void Start()
    {
        if (playerSkills == null) playerSkills = FindAnyObjectByType<SkillLoadout>();
        if (gridManager == null) gridManager = FindAnyObjectByType<HexGridManager>();
        EnsureEventSystem();
        CreateHud();
        if (playerSkills != null) playerSkills.FeedbackRequested += ShowFeedback;
    }

    private void OnDestroy()
    {
        if (playerSkills != null) playerSkills.FeedbackRequested -= ShowFeedback;
    }

    private void ShowFeedback(string message)
    {
        if (status != null) status.text = message;
        if (detail != null && message.StartsWith("이동"))
        {
            detail.text = message.EndsWith("없음") ? "이동을 건너뜁니다." : "강조된 빈 타일을 클릭하세요.";
            return;
        }
        if (detail != null) detail.text = message == "타겟 없음" ? "공격을 건너뜁니다." : "강조된 적 유닛을 클릭하세요.";
    }

    private void Update()
    {
        RefreshStates();
        PlayerController activePlayer = playerSkills == null ? null : playerSkills.GetComponent<PlayerController>();
        if (activePlayer != null && activePlayer.turnManager != null && activePlayer.turnManager.Phase == RoundPhase.CardSelection)
            return;
        if (selectedSkill == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Vector3 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        RaycastHit2D hit = Physics2D.Raycast(point, Vector2.zero);
        HexTile tile = hit.collider == null ? null : hit.collider.GetComponent<HexTile>();
        if (tile == null || !gridManager.GetCoordinatesInRange(playerSkills.GetPlanningSource(), selectedSkill.range).Contains(tile.Coordinate)) return;
        selectedTarget = tile.Coordinate;
        gridManager.SetSelectedHighlight(selectedTarget);
        status.text = $"대상 타일 ({tile.Coordinate.x}, {tile.Coordinate.y}) 선택";
        detail.text = "사용 버튼을 눌러 이 행동을 예약하세요.";
    }

    private void CreateHud()
    {
        GameObject root = new("Card HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        GameObject prompt = UiObject("Instruction Strip", root.transform, typeof(Image), typeof(Outline));
        SetRect(prompt, new(.5f, 0), new(.5f, 0), new(.5f, 0), new(0, 324), new(760, 58));
        prompt.GetComponent<Image>().color = new(.025f, .03f, .045f, .94f);
        prompt.GetComponent<Outline>().effectColor = new(1f, .75f, .28f, .22f);
        status = Label(prompt.transform, "행동 카드를 선택하세요", new(0, 11), new(720, 24), 17, TextAnchor.MiddleCenter, Color.white, true);
        detail = Label(prompt.transform, "주 행동과 보조 행동을 한 장씩 예약할 수 있습니다.", new(0, -11), new(720, 20), 13, TextAnchor.MiddleCenter, new(.65f, .7f, .78f, 1f));

        GameObject hand = UiObject("Card Hand", root.transform, typeof(Image), typeof(Outline), typeof(SkillHandLayout));
        SetRect(hand, new(.5f, 0), new(.5f, 0), new(.5f, 0), new(0, 14), new(1280, 292));
        Image handBackground = hand.GetComponent<Image>();
        handBackground.color = new(.015f, .02f, .032f, .92f);
        handBackground.raycastTarget = false;
        hand.GetComponent<Outline>().effectColor = new(.25f, .34f, .45f, .42f);
        handLayout = hand.GetComponent<SkillHandLayout>();

        List<SkillDefinition> skills = ConfiguredSkills();
        for (int i = 0; i < skills.Count; i++) CreateCard(hand.transform, skills[i]);
        CreateButtons(hand.transform);
        RefreshStates();
    }

    private List<SkillDefinition> ConfiguredSkills()
    {
        List<SkillDefinition> result = new();
        Add(result, playerSkills == null ? null : playerSkills.mainSkills);
        AddUnique(result, Resources.Load<SkillDefinition>("Skills/Bonus/ShieldBash"));
        Add(result, playerSkills == null ? null : playerSkills.subSkills);
        AddUnique(result, Resources.Load<SkillDefinition>("Skills/Bonus/GuardStance"));
        return result;
    }

    private static void Add(List<SkillDefinition> target, SkillDefinition[] source)
    {
        if (source == null) return;
        foreach (SkillDefinition skill in source) if (skill != null) target.Add(skill);
    }

    private static void AddUnique(List<SkillDefinition> target, SkillDefinition skill)
    {
        if (skill != null && !target.Contains(skill)) target.Add(skill);
    }

    private void CreateCard(Transform parent, SkillDefinition skill)
    {
        Color accent = skill.actionSlot == SkillActionSlot.Main ? MainColor : SubColor;
        GameObject go = UiObject($"Card - {skill.displayName}", parent, typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow), typeof(SkillCardView));
        go.GetComponent<Canvas>().overrideSorting = true;
        RectTransform rect = SetRect(go, new(.5f, 0), new(.5f, 0), new(.5f, 0), Vector2.zero, new(250, 330));
        Image background = go.GetComponent<Image>(); background.color = new(.095f, .105f, .13f, 1f);
        Outline outline = go.GetComponent<Outline>(); outline.effectColor = accent; outline.effectDistance = new(3, -3);
        Shadow shadow = go.GetComponent<Shadow>(); shadow.effectColor = new(0, 0, 0, .75f); shadow.effectDistance = new(10, -12);
        Button button = go.GetComponent<Button>(); button.targetGraphic = background; button.transition = Selectable.Transition.None;

        Image header = UiObject("Header", rect, typeof(Image)).GetComponent<Image>();
        SetRect(header.gameObject, new(0, 1), new(1, 1), new(.5f, 1), Vector2.zero, new(0, 42)); header.color = accent;
        Label(header.transform, skill.actionSlot == SkillActionSlot.Main ? "MAIN ACTION" : "SUB ACTION", new(-50, 0), new(130, 30), 14, TextAnchor.MiddleLeft, new(.05f, .06f, .08f, 1f), true);
        GameObject initiative = UiObject("Initiative", header.transform, typeof(Image));
        SetRect(initiative, new(1, .5f), new(1, .5f), new(1, .5f), new(-8, 0), new(62, 30)); initiative.GetComponent<Image>().color = new(.035f, .04f, .055f, .92f);
        Label(initiative.transform, skill.initiative.ToString("0"), Vector2.zero, new(56, 26), 17, TextAnchor.MiddleCenter, Color.white, true);

        Image art = UiObject("Artwork", rect, typeof(Image)).GetComponent<Image>();
        SetRect(art.gameObject, new(.5f, 1), new(.5f, 1), new(.5f, 1), new(0, -48), new(224, 130));
        art.sprite = ResolveIcon(skill); art.preserveAspect = true; art.raycastTarget = false;
        art.color = art.sprite == null ? new(.12f, .14f, .18f, 1f) : Color.white;
        if (art.sprite == null) Label(art.transform, Initials(skill.displayName), Vector2.zero, new(180, 100), 42, TextAnchor.MiddleCenter, accent, true);
        Label(rect, skill.displayName, new(0, 111), new(220, 34), 22, TextAnchor.MiddleCenter, Color.white, true);
        Label(rect, PlainText(skill.description), new(0, 47), new(214, 78), 14, TextAnchor.UpperLeft, new(.82f, .84f, .88f, 1f));
        Image rule = UiObject("Rule", rect, typeof(Image)).GetComponent<Image>();
        SetRect(rule.gameObject, new(.5f, 0), new(.5f, 0), new(.5f, 0), new(0, 48), new(214, 2)); rule.color = new(accent.r, accent.g, accent.b, .6f);
        Label(rect, $"사거리  {skill.range}", new(-56, 22), new(100, 28), 14, TextAnchor.MiddleLeft, new(.7f, .75f, .82f, 1f));
        Label(rect, EffectSummary(skill), new(54, 22), new(112, 28), 14, TextAnchor.MiddleRight, accent, true);
        SkillCardView view = go.GetComponent<SkillCardView>();
        view.Initialize(skill, rect, background, outline, accent, () => SelectSkill(skill));
        cards.Add(view);
        handLayout.Register(view);
    }

    private void CreateButtons(Transform parent)
    {
        confirmButton = ActionButton(parent, "Confirm", "사용 / 공개", new(520, 18), new(180, 58), new(.12f, .54f, .42f, 1f), Confirm);
        Label(parent, "카드 선택 후 사용", new(520, -34), new(190, 24), 13, TextAnchor.MiddleCenter, new(.55f, .61f, .7f, 1f));
    }

    private static Button ActionButton(Transform parent, string name, string caption, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject go = UiObject(name, parent, typeof(Image), typeof(Button), typeof(Outline)); SetRect(go, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), position, size);
        Image image = go.GetComponent<Image>(); image.color = color; go.GetComponent<Outline>().effectColor = new(1, 1, 1, .25f);
        Button button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        Label(go.transform, caption, Vector2.zero, size - new Vector2(12, 12), 20, TextAnchor.MiddleCenter, Color.white, true); return button;
    }

    private void SelectSkill(SkillDefinition skill)
    {
        if (!playerSkills.CanUse(skill)) { status.text = "이미 예약했거나 사용할 수 없는 카드입니다."; return; }
        selectedSkill = skill; Vector2Int source = playerSkills.GetPlanningSource();
        bool chooseAtExecution = true;
        selectedTarget = skill.targetsSelf || chooseAtExecution ? source : InvalidTarget;
        gridManager.ClearHighlights();
        status.text = skill.displayName;
        detail.text = chooseAtExecution ? "대상은 이 유닛의 실행 차례에 선택합니다." : skill.targetsSelf ? "자신이 대상으로 선택되었습니다. 사용 버튼을 누르세요." : "강조된 타일에서 대상을 선택하세요.";
        RefreshStates();
    }

    private void Confirm()
    {
        if (selectedSkill == null && playerSkills.HasCompletePlan) { ClearGhost(); gridManager.ClearHighlights(); status.text = "카드를 공개합니다"; detail.text = "선제도가 낮은 카드부터 행동합니다."; playerSkills.GetComponent<PlayerController>().turnManager.SubmitPlayerActionCard(playerSkills); RefreshStates(); return; }
        if (selectedSkill == null || selectedTarget == InvalidTarget) { status.text = selectedSkill == null ? "먼저 행동 카드를 선택하세요." : "강조된 타일에서 대상을 선택하세요."; return; }
        SkillDefinition committed = selectedSkill; Vector2Int target = selectedTarget;
        if (!playerSkills.Plan(committed, target, gridManager)) { status.text = "이 대상에게는 카드를 사용할 수 없습니다."; return; }
        gridManager.ClearHighlights(); selectedSkill = null; selectedTarget = InvalidTarget;
        status.text = $"{committed.displayName} 예약 완료"; detail.text = playerSkills.HasCompletePlan ? "사용 / 공개 버튼을 눌러 행동을 시작하세요." : "다른 종류의 행동 카드도 선택할 수 있습니다."; RefreshStates();
    }

    private void EndTurn()
    {
        PlayerController player = playerSkills == null ? null : playerSkills.GetComponent<PlayerController>();
        if (player == null || player.turnManager == null || !player.turnManager.CanPlayerAct(player) || playerSkills.IsExecutingPlan) return;
        selectedSkill = null; selectedTarget = InvalidTarget; ClearGhost(); gridManager.ClearHighlights();
        status.text = playerSkills.HasPlannedActions ? "예약한 카드를 공개합니다" : "행동 없이 턴을 종료합니다"; detail.text = "몬스터 카드와 함께 행동 순서를 결정합니다.";
        player.turnManager.SubmitPlayerActionCard(playerSkills); RefreshStates();
    }

    private void RefreshStates()
    {
        foreach (SkillCardView card in cards) card.SetState(playerSkills != null && playerSkills.CanUse(card.Skill), card.Skill == selectedSkill);
        if (confirmButton != null) confirmButton.interactable = selectedSkill != null && selectedTarget != InvalidTarget || selectedSkill == null && playerSkills.HasCompletePlan;
    }

    private static bool HasMovement(SkillDefinition skill) { if (skill?.effects == null) return false; foreach (SkillEffect effect in skill.effects) if (effect.type == SkillEffectType.Move || effect.type == SkillEffectType.Jump) return true; return false; }
    private static bool RequiresExecutionTarget(SkillDefinition skill) => skill != null && !skill.targetsSelf && (skill.targetsEnemies || skill.targetsAllies) && !HasMovement(skill);
    private bool HasEnemyInRange(int range) { Vector2Int source = playerSkills.GetPlanningSource(); foreach (EnemyController enemy in FindObjectsByType<EnemyController>()) { UnitStats stats = enemy.GetComponent<UnitStats>(); if ((stats == null || stats.CurrentHP > 0) && HexGridManager.HexDistance(source, enemy.CurrentCoordinate) <= range) return true; } return false; }
    private void CreateGhost(Vector2Int coordinate) { ClearGhost(); if (!gridManager.TryGetTile(coordinate, out HexTile tile)) return; SpriteRenderer source = playerSkills.GetComponentInChildren<SpriteRenderer>(); if (source == null || source.sprite == null) return; movementGhost = new("Planned Movement Ghost", typeof(SpriteRenderer)); SpriteRenderer ghost = movementGhost.GetComponent<SpriteRenderer>(); ghost.sprite = source.sprite; ghost.sortingLayerID = source.sortingLayerID; ghost.sortingOrder = source.sortingOrder + 1; ghost.color = new(source.color.r, source.color.g, source.color.b, .4f); movementGhost.transform.position = tile.transform.position; movementGhost.transform.localScale = source.transform.lossyScale; }
    private void ClearGhost() { if (movementGhost != null) Destroy(movementGhost); movementGhost = null; }
    private static string PlainText(string value) { if (string.IsNullOrWhiteSpace(value)) return "설명이 없습니다."; return Regex.Replace(value, @"\[(?:/?color(?:=[^\]]+)?)\]", "", RegexOptions.IgnoreCase).Replace("**", "").Replace("*", ""); }
    private static Sprite ResolveIcon(SkillDefinition skill) { if (skill.icon != null) return skill.icon; if (string.IsNullOrWhiteSpace(skill.iconResourcePath)) return null; Texture2D texture = Resources.Load<Texture2D>(skill.iconResourcePath); return texture == null ? null : Sprite.Create(texture, new(0, 0, texture.width, texture.height), new(.5f, .5f), 100); }
    private static string EffectSummary(SkillDefinition skill) { if (skill.effects == null || skill.effects.Length == 0) return "효과 없음"; SkillEffect effect = skill.effects[0]; string name = effect.type switch { SkillEffectType.Damage => "피해", SkillEffectType.Heal => "회복", SkillEffectType.Push => "밀치기", SkillEffectType.Pull => "당기기", SkillEffectType.Stun => "기절", SkillEffectType.Immobilize => "이동 불가", SkillEffectType.Move => "이동", SkillEffectType.Jump => "도약", SkillEffectType.Shield => "보호막", _ => "효과" }; return effect.value > 0 ? $"{name} {effect.value}" : name; }
    private static string Initials(string value) { if (string.IsNullOrWhiteSpace(value)) return "?"; string[] words = value.Split(' '); return words.Length == 1 ? words[0][..1].ToUpperInvariant() : (words[0][..1] + words[^1][..1]).ToUpperInvariant(); }
    private static GameObject UiObject(string name, Transform parent, params System.Type[] components) { GameObject go = new(name, components); go.transform.SetParent(parent, false); return go; }
    private static RectTransform SetRect(GameObject go, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size) { RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect; }
    private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, bool bold = false) { GameObject go = UiObject("Text", parent, typeof(Text)); Text text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.alignment = alignment; text.color = color; text.fontSize = fontSize; text.text = value; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false; SetRect(go, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), position, size); return text; }
    private static void EnsureEventSystem() { if (FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); }
}
