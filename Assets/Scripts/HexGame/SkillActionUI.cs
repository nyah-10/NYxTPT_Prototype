using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class SkillActionUI : MonoBehaviour
{
    private static readonly Color MainColor = new(1f, .48f, .12f, 1f);
    private static readonly Color SubColor = new(.08f, .78f, 1f, 1f);
    private static readonly Vector2Int InvalidTarget = new(-999, -999);

    public SkillLoadout playerSkills;
    public HexGridManager gridManager;

    private readonly List<SkillCard> cards = new();
    private SkillDefinition selectedSkill;
    private Vector2Int selectedTarget = InvalidTarget;
    private Text status;
    private Text detail;
    private GameObject tooltip;
    private Text tooltipTitle;
    private Text tooltipBody;
    private Button confirmButton;
    private Button endTurnButton;
    private Canvas hudCanvas;
    private GameObject movementGhost;

    private sealed class SkillCard
    {
        public SkillDefinition Skill;
        public Button Button;
        public Image Accent;
    }

    private void Start()
    {
        if (playerSkills == null) playerSkills = FindAnyObjectByType<SkillLoadout>();
        if (gridManager == null) gridManager = FindAnyObjectByType<HexGridManager>();
        EnsureEventSystem();
        CreateHud();
    }

    private void Update()
    {
        ApplyHudScale();
        RefreshCardStates();
        if (selectedSkill == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        RaycastHit2D hit = Physics2D.Raycast(point, Vector2.zero);
        HexTile tile = hit.collider == null ? null : hit.collider.GetComponent<HexTile>();
        if (tile == null || !gridManager.GetCoordinatesInRange(
                playerSkills.GetPlanningSource(), selectedSkill.range).Contains(tile.Coordinate)) return;

        selectedTarget = tile.Coordinate;
        gridManager.SetSelectedHighlight(selectedTarget);
        status.text = $"대상 위치 ({tile.Coordinate.x}, {tile.Coordinate.y})를 선택했습니다. 사용 버튼을 누르세요.";
        RefreshCardStates();
    }

    private void CreateHud()
    {
        GameObject canvasObject = new("Skill HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        ApplyHudScale();

        GameObject panel = UiObject("Panel", canvasObject.transform, typeof(Image));
        RectTransform panelRect = SetRect(panel, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0),
            new Vector2(0, 18), new Vector2(1050, 190));
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(.018f, .04f, .07f, .96f);

        status = Label(panelRect, "스킬을 선택하세요.", new Vector2(0, 66), new Vector2(720, 28), 17, TextAnchor.MiddleCenter, Color.white);
        detail = Label(panelRect, "", new Vector2(0, 43), new Vector2(720, 20), 13, TextAnchor.MiddleCenter, new Color(.62f, .72f, .8f));

        List<SkillDefinition> skills = GetConfiguredSkills();
        const float availableWidth = 650f;
        const float gap = 10f;
        float cardWidth = Mathf.Clamp((availableWidth - gap * Mathf.Max(0, skills.Count - 1)) / Mathf.Max(1, skills.Count), 68f, 86f);
        float totalWidth = cardWidth * skills.Count + gap * Mathf.Max(0, skills.Count - 1);
        float startX = -435f + cardWidth * .5f + (availableWidth - totalWidth) * .5f;
        for (int i = 0; i < skills.Count; i++)
            CreateSkillCard(panelRect, skills[i], new Vector2(startX + i * (cardWidth + gap), -23), cardWidth);

        confirmButton = CreateConfirmButton(panelRect);
        endTurnButton = CreateEndTurnButton(panelRect);
        CreateTooltip(panelRect);
        RefreshCardStates();
    }

    private List<SkillDefinition> GetConfiguredSkills()
    {
        List<SkillDefinition> result = new();
        AddSkills(result, playerSkills == null ? null : playerSkills.mainSkills);
        AddSkills(result, playerSkills == null ? null : playerSkills.subSkills);
        return result;
    }

    private static void AddSkills(List<SkillDefinition> destination, SkillDefinition[] source)
    {
        if (source == null) return;
        foreach (SkillDefinition skill in source)
            if (skill != null) destination.Add(skill);
    }

    private void CreateSkillCard(Transform parent, SkillDefinition skill, Vector2 position, float width)
    {
        GameObject card = UiObject($"Skill - {skill.displayName}", parent, typeof(Image), typeof(Button));
        RectTransform rect = SetRect(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, new Vector2(width, width));
        Image background = card.GetComponent<Image>();
        background.color = new Color(.035f, .075f, .12f, .96f);
        Button button = card.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectSkill(skill));

        Image accent = UiObject("Accent", rect, typeof(Image)).GetComponent<Image>();
        SetRect(accent.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 6));
        accent.color = skill.actionSlot == SkillActionSlot.Main ? MainColor : SubColor;

        Image icon = UiObject("Icon", rect, typeof(Image)).GetComponent<Image>();
        SetRect(icon.gameObject, new Vector2(0, 0), new Vector2(1, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(-8, -8));
        icon.sprite = ResolveIcon(skill);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = icon.sprite == null ? new Color(.08f, .13f, .19f, 1f) : Color.white;
        if (icon.sprite == null)
            Label(icon.transform, GetInitials(skill.displayName), Vector2.zero, new Vector2(72, 72), 24, TextAnchor.MiddleCenter,
                skill.actionSlot == SkillActionSlot.Main ? MainColor : SubColor);
        AddHoverEvents(card, skill);

        cards.Add(new SkillCard { Skill = skill, Button = button, Accent = accent });
    }

    private Button CreateConfirmButton(Transform parent)
    {
        GameObject go = UiObject("Confirm", parent, typeof(Image), typeof(Button));
        SetRect(go, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-92, 23), new Vector2(170, 62));
        Image image = go.GetComponent<Image>();
        image.color = new Color(.08f, .34f, .39f, .98f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Confirm);
        Label(go.transform, "사용", Vector2.zero, new Vector2(160, 30), 19, TextAnchor.MiddleCenter, Color.white).fontStyle = FontStyle.Bold;
        return button;
    }

    private Button CreateEndTurnButton(Transform parent)
    {
        GameObject go = UiObject("End Turn", parent, typeof(Image), typeof(Button));
        SetRect(go, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-92, -49), new Vector2(170, 48));
        Image image = go.GetComponent<Image>();
        image.color = new Color(.22f, .18f, .12f, .98f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(EndTurn);
        Label(go.transform, "턴 종료", Vector2.zero, new Vector2(160, 30), 17, TextAnchor.MiddleCenter, new Color(1f, .78f, .38f)).fontStyle = FontStyle.Bold;
        return button;
    }

    private void CreateTooltip(Transform parent)
    {
        tooltip = UiObject("Skill Tooltip", parent, typeof(Image));
        SetRect(tooltip, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 0), new Vector2(-75, 18), new Vector2(440, 190));
        tooltip.GetComponent<Image>().color = new Color(.018f, .035f, .06f, .98f);
        tooltipTitle = Label(tooltip.transform, "", new Vector2(0, 68), new Vector2(400, 38), 22, TextAnchor.MiddleLeft, Color.white);
        tooltipTitle.fontStyle = FontStyle.Bold;
        tooltipBody = Label(tooltip.transform, "", new Vector2(0, -5), new Vector2(400, 104), 17, TextAnchor.UpperLeft, new Color(.86f, .9f, .94f));
        tooltipBody.supportRichText = true;
        tooltip.SetActive(false);
    }

    private void AddHoverEvents(GameObject target, SkillDefinition skill)
    {
        EventTrigger trigger = target.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new() { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTooltip(skill));
        EventTrigger.Entry exit = new() { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => tooltip.SetActive(false));
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private void ShowTooltip(SkillDefinition skill)
    {
        tooltipTitle.text = $"{skill.displayName}   <color=#{ColorUtility.ToHtmlStringRGB(skill.actionSlot == SkillActionSlot.Main ? MainColor : SubColor)}>{(skill.actionSlot == SkillActionSlot.Main ? "주 행동" : "보조 행동")}</color>";
        tooltipBody.text = FormatDescription(skill.description) + $"\n\n<color=#91A7B8>사거리 {skill.range}  ·  {BuildEffectSummary(skill)}</color>";
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
    }

    private static string FormatDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "설명이 없습니다.";
        string formatted = Regex.Replace(value, @"\*\*(.+?)\*\*", "<b>$1</b>");
        formatted = Regex.Replace(formatted, @"(?<!\*)\*([^*]+?)\*(?!\*)", "<i>$1</i>");
        formatted = Regex.Replace(formatted, @"\[color=(#[0-9a-fA-F]{6,8}|[a-zA-Z]+)\]", "<color=$1>");
        formatted = Regex.Replace(formatted, @"\[(#[0-9a-fA-F]{6,8})\]", "<color=$1>");
        return formatted.Replace("[/color]", "</color>");
    }

    private static Sprite ResolveIcon(SkillDefinition skill)
    {
        if (skill.icon != null) return skill.icon;
        if (string.IsNullOrWhiteSpace(skill.iconResourcePath)) return null;
        Texture2D texture = Resources.Load<Texture2D>(skill.iconResourcePath);
        return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
    }

    private void SelectSkill(SkillDefinition skill)
    {
        if (!playerSkills.CanUse(skill))
        {
            status.text = "해당 행동 포인트가 남아 있지 않습니다.";
            return;
        }

        selectedSkill = skill;
        Vector2Int planningSource = playerSkills.GetPlanningSource();
        selectedTarget = skill.targetsSelf ? planningSource : InvalidTarget;
        Color highlight = skill.targetsSelf ? new Color(.2f, 1f, .35f, .55f) :
            skill.actionSlot == SkillActionSlot.Sub ? new Color(.15f, .8f, 1f, .8f) : new Color(1f, .35f, .12f, .65f);
        gridManager.SetHighlights(gridManager.GetCoordinatesInRange(
            planningSource, skill.range), highlight);
        if (skill.targetsEnemies && !HasMovementEffect(skill) && !HasEnemyInRange(skill.range))
        {
            status.text = "사거리 내 적이 없습니다.";
            detail.text = "다른 스킬을 선택하거나 턴을 종료하세요.";
            RefreshCardStates();
            return;
        }
        if (skill.targetsSelf) gridManager.SetSelectedHighlight(selectedTarget);
        status.text = skill.targetsSelf ? $"{skill.displayName}: 자신을 대상으로 선택했습니다. 사용 버튼을 누르세요." :
            $"{skill.displayName}: 강조된 칸에서 대상을 선택하세요.";
        detail.text = "아이콘에 마우스를 올리면 상세 설명을 볼 수 있습니다.";
        RefreshCardStates();
    }

    private bool HasEnemyInRange(int range)
    {
        Vector2Int source = playerSkills.GetPlanningSource();
        foreach (EnemyController enemy in FindObjectsByType<EnemyController>())
        {
            UnitStats stats = enemy.GetComponent<UnitStats>();
            if ((stats == null || stats.CurrentHP > 0) && HexGridManager.HexDistance(source, enemy.CurrentCoordinate) <= range)
                return true;
        }
        return false;
    }

    private void EndTurn()
    {
        PlayerController player = playerSkills == null ? null : playerSkills.GetComponent<PlayerController>();
        if (player == null || player.turnManager == null || !player.turnManager.IsPlayerTurn || playerSkills.IsExecutingPlan) return;
        selectedSkill = null;
        selectedTarget = InvalidTarget;
        ClearMovementGhost();
        gridManager.ClearHighlights();
        if (playerSkills.HasPlannedActions)
        {
            status.text = "행동 카드를 공개하고 이니셔티브 순서를 정합니다.";
            detail.text = "몬스터 카드도 함께 공개됩니다.";
            player.turnManager.SubmitPlayerActionCard();
        }
        else
        {
            FinishEndTurn(player);
        }
        RefreshCardStates();
    }

    private IEnumerator EndTurnAfterPlan(PlayerController player)
    {
        yield return new WaitUntil(() => !playerSkills.IsExecutingPlan);
        FinishEndTurn(player);
        RefreshCardStates();
    }

    private void FinishEndTurn(PlayerController player)
    {
        status.text = "행동 없이 카드를 공개합니다.";
        detail.text = "몬스터 카드와 함께 이니셔티브 순서를 정합니다.";
        player.turnManager.SubmitPlayerActionCard();
    }

    private void Confirm()
    {
        if (selectedSkill == null && playerSkills.HasCompletePlan)
        {
            ClearMovementGhost();
            gridManager.ClearHighlights();
            status.text = "행동 카드를 공개하고 이니셔티브 큐를 만듭니다.";
            detail.text = "낮은 이니셔티브부터 행동합니다.";
            PlayerController player = playerSkills.GetComponent<PlayerController>();
            player.turnManager.SubmitPlayerActionCard();
            RefreshCardStates();
            return;
        }

        if (selectedSkill == null || selectedTarget == InvalidTarget)
        {
            status.text = selectedSkill == null ? "먼저 스킬을 선택하세요." : "강조된 칸에서 대상을 선택하세요.";
            return;
        }

        SkillDefinition committedSkill = selectedSkill;
        Vector2Int committedTarget = selectedTarget;
        if (!playerSkills.Plan(committedSkill, committedTarget, gridManager))
        {
            status.text = "해당 대상에게 이 스킬을 사용할 수 없습니다.";
            return;
        }

        if (HasMovementEffect(committedSkill)) CreateMovementGhost(committedTarget);
        gridManager.ClearHighlights();
        selectedSkill = null;
        selectedTarget = InvalidTarget;
        status.text = $"{committedSkill.displayName} 행동을 예약했습니다.";
        detail.text = playerSkills.HasCompletePlan ? "사용 버튼을 눌러 두 행동을 실행하세요." : "다른 종류의 행동을 선택하세요.";
        RefreshCardStates();
    }

    private void RefreshCardStates()
    {
        foreach (SkillCard card in cards)
        {
            bool usable = playerSkills != null && playerSkills.CanUse(card.Skill);
            bool selected = card.Skill == selectedSkill;
            card.Button.interactable = usable;
            Color accent = card.Skill.actionSlot == SkillActionSlot.Main ? MainColor : SubColor;
            card.Accent.color = selected ? Color.white : usable ? accent : new Color(.25f, .29f, .32f, .65f);
            card.Accent.rectTransform.sizeDelta = new Vector2(0, selected ? 10 : 6);
        }
        if (confirmButton != null) confirmButton.interactable =
            selectedSkill != null && selectedTarget != InvalidTarget || selectedSkill == null && playerSkills.HasCompletePlan;
        if (endTurnButton != null)
        {
            PlayerController player = playerSkills == null ? null : playerSkills.GetComponent<PlayerController>();
            endTurnButton.interactable = player != null && player.turnManager != null && player.turnManager.IsPlayerTurn && !playerSkills.IsExecutingPlan;
        }
    }

    private static bool HasMovementEffect(SkillDefinition skill)
    {
        if (skill == null || skill.effects == null) return false;
        foreach (SkillEffect effect in skill.effects)
            if (effect.type == SkillEffectType.Move || effect.type == SkillEffectType.Jump) return true;
        return false;
    }

    private void CreateMovementGhost(Vector2Int coordinate)
    {
        ClearMovementGhost();
        if (!gridManager.TryGetTile(coordinate, out HexTile tile)) return;

        SpriteRenderer source = playerSkills.GetComponentInChildren<SpriteRenderer>();
        if (source == null || source.sprite == null) return;

        movementGhost = new GameObject("Planned Movement Ghost", typeof(SpriteRenderer));
        SpriteRenderer ghost = movementGhost.GetComponent<SpriteRenderer>();
        ghost.sprite = source.sprite;
        ghost.flipX = source.flipX;
        ghost.flipY = source.flipY;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder + 1;
        Color color = source.color;
        color.a = .4f;
        ghost.color = color;
        movementGhost.transform.position = tile.transform.position;
        movementGhost.transform.localScale = source.transform.lossyScale;
    }

    private void ClearMovementGhost()
    {
        if (movementGhost != null) Destroy(movementGhost);
        movementGhost = null;
    }

    private void ApplyHudScale()
    {
        if (hudCanvas == null) return;
        // A minimum supersampled UI scale keeps type legible in a small docked Game View.
        float fitScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        hudCanvas.scaleFactor = Mathf.Max(.7f, fitScale);
    }

    private static string BuildEffectSummary(SkillDefinition skill)
    {
        if (skill.effects == null || skill.effects.Length == 0) return "효과 없음";
        SkillEffect effect = skill.effects[0];
        string name = effect.type switch
        {
            SkillEffectType.Damage => "피해",
            SkillEffectType.Heal => "회복",
            SkillEffectType.Push => "밀치기",
            SkillEffectType.Pull => "당기기",
            SkillEffectType.Stun => "기절",
            SkillEffectType.Immobilize => "이동 불가",
            SkillEffectType.Move => "이동",
            SkillEffectType.Jump => "도약",
            SkillEffectType.Shield => "보호막",
            _ => "효과"
        };
        return effect.value > 0 ? $"{name} {effect.value}" : name;
    }

    private static string GetInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "?";
        string[] words = value.Split(' ');
        return words.Length == 1 ? words[0].Substring(0, 1).ToUpperInvariant() :
            (words[0].Substring(0, 1) + words[^1].Substring(0, 1)).ToUpperInvariant();
    }

    private static GameObject UiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = new(name, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject go = UiObject("Text", parent, typeof(Text));
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = alignment;
        text.color = color;
        text.fontSize = fontSize;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        SetRect(go, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
