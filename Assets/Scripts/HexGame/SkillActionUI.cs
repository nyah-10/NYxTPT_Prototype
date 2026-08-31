using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class SkillActionUI : MonoBehaviour
{
    private sealed class CardSelectionState
    {
        public SkillCardView HoveredCard;
        public SkillCardView FocusedCard;
        public SkillCardView SelectedTopCard;
        public SkillCardView SelectedBottomCard;
        public bool IsConfirmed;
    }

    private sealed class CardVisual
    {
        public SkillCardView View;
        public Image TopShade;
        public Image BottomShade;
        public Text TopMark;
        public Text BottomMark;
    }

    private static readonly Color TopColor = new(1f, .42f, .12f, 1f);
    private static readonly Color BottomColor = new(.08f, .76f, 1f, 1f);
    public SkillLoadout playerSkills;
    public HexGridManager gridManager;

    private readonly CardSelectionState selection = new();
    private readonly List<CardVisual> cards = new();
    private RectTransform handContent;
    private GameObject modal;
    private RectTransform modalCard;
    private Text modalTitle, modalTop, modalBottom, modalInitiative;
    private Image modalTopImage, modalBottomImage;
    private Text topSlot, bottomSlot, preview;
    private Button confirmButton;
    private Text confirmLabel;

    private void Start()
    {
        if (playerSkills == null) playerSkills = FindAnyObjectByType<SkillLoadout>();
        if (gridManager == null) gridManager = FindAnyObjectByType<HexGridManager>();
        EnsureEventSystem();
        CreateHud();
    }

    private void Update()
    {
        if (playerSkills == null) return;
        if (selection.IsConfirmed && !playerSkills.IsCurrentActionConfirmed)
        {
            selection.IsConfirmed = false;
            selection.SelectedTopCard = null;
            selection.SelectedBottomCard = null;
        }
        bool canSelect = playerSkills.GetComponent<PlayerController>()?.turnManager?.Phase == RoundPhase.CardSelection;
        if (!canSelect && !selection.IsConfirmed) CloseFocus();
        if (modal != null && modal.activeSelf)
        {
            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            modalCard.localScale = Vector3.Lerp(modalCard.localScale, Vector3.one, blend);
        }
        RefreshSelectionVisuals();
    }

    private void CreateHud()
    {
        GameObject root = new("Card Selection HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        CreateSelectionBar(root.transform);
        CreateHand(root.transform);
        CreateFocusModal(root.transform);
        RefreshSelectionVisuals();
    }

    private void CreateSelectionBar(Transform parent)
    {
        GameObject bar = Ui("Current Action", parent, typeof(Image), typeof(Outline));
        SetRect(bar, new(.5f, 1), new(.5f, 1), new(.5f, 1), new(0, -78), new(1040, 124));
        bar.GetComponent<Image>().color = new(.02f, .027f, .043f, .96f);
        bar.GetComponent<Outline>().effectColor = new(.3f, .4f, .55f, .5f);
        topSlot = Label(bar.transform, "첫 번째 선택\nTOP · 선택 필요", new(-320, 0), new(300, 88), 18, TextAnchor.MiddleLeft, Color.white);
        bottomSlot = Label(bar.transform, "두 번째 선택\nBOTTOM · 선택 필요", new(0, 0), new(300, 88), 18, TextAnchor.MiddleLeft, Color.white);
        preview = Label(bar.transform, "서로 다른 카드의 TOP과 BOTTOM을 선택하세요", new(300, 12), new(300, 48), 15, TextAnchor.MiddleCenter, new(.65f, .7f, .8f, 1f));
        confirmButton = ActionButton(bar.transform, "Confirm Action", "행동 확정", new(300, -30), new(220, 44), new(.12f, .5f, .38f, 1f), ConfirmAction);
        confirmLabel = confirmButton.GetComponentInChildren<Text>();
    }

    private void CreateHand(Transform parent)
    {
        GameObject panel = Ui("Hand Panel", parent, typeof(Image));
        SetRect(panel, new(.5f, 0), new(.5f, 0), new(.5f, 0), new(0, 20), new(1760, 390));
        panel.GetComponent<Image>().color = new(.012f, .017f, .028f, .94f);
        Label(panel.transform, "마우스 휠 또는 드래그로 손패 탐색 · 카드를 클릭해 자세히 보기", new(0, 360), new(1200, 24), 15, TextAnchor.MiddleCenter, new(.58f, .65f, .75f, 1f));

        GameObject viewport = Ui("Viewport", panel.transform, typeof(Image), typeof(Mask));
        SetRect(viewport, new(.5f, 0), new(.5f, 0), new(.5f, 0), new(0, 12), new(1660, 340));
        viewport.GetComponent<Image>().color = new(0, 0, 0, .001f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        GameObject content = Ui("Cards", viewport.transform, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        handContent = SetRect(content, new(0, .5f), new(0, .5f), new(0, .5f), new(28, 0), new(0, 320));
        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(38, 120, 4, 4);
        layout.spacing = -82;
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = false; layout.childControlHeight = false;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = panel.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = handContent;
        scroll.horizontal = true; scroll.vertical = false; scroll.scrollSensitivity = 42; scroll.inertia = true; scroll.decelerationRate = .12f;

        int count = Mathf.Max(playerSkills.mainSkills?.Length ?? 0, playerSkills.subSkills?.Length ?? 0);
        for (int i = 0; i < count; i++)
        {
            SkillDefinition top = i < (playerSkills.mainSkills?.Length ?? 0) ? playerSkills.mainSkills[i] : null;
            SkillDefinition bottom = i < (playerSkills.subSkills?.Length ?? 0) ? playerSkills.subSkills[i] : null;
            if (top != null || bottom != null) CreateHandCard(i, top, bottom);
        }
        Canvas.ForceUpdateCanvases();
        foreach (CardVisual card in cards) card.View.CaptureRestPosition();
    }

    private void CreateHandCard(int index, SkillDefinition top, SkillDefinition bottom)
    {
        GameObject go = Ui($"Hand Card {index + 1}", handContent, typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Outline), typeof(Shadow), typeof(LayoutElement), typeof(SkillCardView));
        go.GetComponent<Canvas>().overrideSorting = true;
        RectTransform rect = SetRect(go, Vector2.zero, Vector2.zero, new(.5f, 0), Vector2.zero, new(250, 318));
        go.GetComponent<LayoutElement>().preferredWidth = 250; go.GetComponent<LayoutElement>().preferredHeight = 318;
        Image background = go.GetComponent<Image>(); background.color = new(.085f, .095f, .12f, 1f);
        Outline outline = go.GetComponent<Outline>(); outline.effectDistance = new(3, -3);
        Shadow shadow = go.GetComponent<Shadow>(); shadow.effectColor = new(0, 0, 0, .72f); shadow.effectDistance = new(10, -12);
        Label(rect, $"CARD {index + 1}", new(0, 139), new(216, 24), 14, TextAnchor.MiddleCenter, new(.72f, .77f, .84f, 1f), true);
        Image topPanel = HalfPanel(rect, "TOP", top, new(0, 66), TopColor);
        Image bottomPanel = HalfPanel(rect, "BOTTOM", bottom, new(0, -76), BottomColor);
        Text topMark = Label(topPanel.transform, "", new(75, 49), new(54, 24), 13, TextAnchor.MiddleCenter, Color.white, true);
        Text bottomMark = Label(bottomPanel.transform, "", new(75, 49), new(54, 24), 13, TextAnchor.MiddleCenter, Color.white, true);
        SkillCardView view = go.GetComponent<SkillCardView>();
        view.Initialize(index, top, bottom, rect, background, outline, HandleHover, OpenFocus);
        cards.Add(new CardVisual { View = view, TopShade = topPanel, BottomShade = bottomPanel, TopMark = topMark, BottomMark = bottomMark });
    }

    private Image HalfPanel(Transform parent, string heading, SkillDefinition skill, Vector2 position, Color accent)
    {
        Image panel = Ui(heading, parent, typeof(Image), typeof(Outline)).GetComponent<Image>();
        SetRect(panel.gameObject, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), position, new(222, 126));
        panel.color = new(.11f, .12f, .15f, 1f); panel.GetComponent<Outline>().effectColor = new(accent.r, accent.g, accent.b, .65f);
        Label(panel.transform, heading, new(-68, 49), new(70, 22), 13, TextAnchor.MiddleLeft, accent, true);
        Label(panel.transform, skill == null ? "효과 없음" : skill.displayName, new(0, 19), new(194, 28), 18, TextAnchor.MiddleLeft, Color.white, true);
        Label(panel.transform, skill == null ? "-" : EffectSummary(skill), new(0, -16), new(194, 32), 15, TextAnchor.MiddleLeft, new(.78f, .81f, .87f, 1f));
        Label(panel.transform, skill == null ? "" : $"사거리 {skill.range}  ·  선제도 {skill.initiative}", new(0, -45), new(194, 22), 13, TextAnchor.MiddleLeft, new(.58f, .65f, .75f, 1f));
        return panel;
    }

    private void CreateFocusModal(Transform parent)
    {
        modal = Ui("Card Focus Modal", parent, typeof(Image), typeof(Button));
        SetRect(modal, Vector2.zero, Vector2.one, new(.5f, .5f), Vector2.zero, Vector2.zero);
        modal.GetComponent<Image>().color = new(0, 0, 0, .72f);
        modal.GetComponent<Button>().onClick.AddListener(CloseFocus);
        GameObject card = Ui("Focused Card", modal.transform, typeof(Image), typeof(Outline), typeof(Shadow));
        modalCard = SetRect(card, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), Vector2.zero, new(520, 720));
        card.GetComponent<Image>().color = new(.055f, .065f, .09f, 1f);
        card.GetComponent<Outline>().effectColor = new(.72f, .8f, .94f, .9f);
        card.GetComponent<Shadow>().effectColor = new(0, 0, 0, .9f);
        modalTitle = Label(card.transform, "", new(0, 315), new(450, 46), 29, TextAnchor.MiddleCenter, Color.white, true);
        modalTopImage = FocusHalf(card.transform, "TOP", new(0, 142), TopColor, SelectFocusedTop, out modalTop);
        modalBottomImage = FocusHalf(card.transform, "BOTTOM", new(0, -150), BottomColor, SelectFocusedBottom, out modalBottom);
        modalInitiative = Label(card.transform, "", new(0, -326), new(440, 34), 18, TextAnchor.MiddleCenter, new(1f, .82f, .35f, 1f), true);
        ActionButton(card.transform, "Close", "손패로 돌아가기", new(0, -378), new(250, 42), new(.18f, .22f, .3f, 1f), CloseFocus);
        modal.SetActive(false);
    }

    private Image FocusHalf(Transform parent, string heading, Vector2 position, Color accent, UnityEngine.Events.UnityAction action, out Text body)
    {
        GameObject go = Ui(heading, parent, typeof(Image), typeof(Button), typeof(Outline));
        SetRect(go, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), position, new(452, 252));
        Image image = go.GetComponent<Image>(); image.color = new(.09f, .1f, .135f, 1f);
        go.GetComponent<Outline>().effectColor = accent; go.GetComponent<Button>().onClick.AddListener(action);
        Label(go.transform, heading, new(0, 102), new(400, 30), 20, TextAnchor.MiddleCenter, accent, true);
        body = Label(go.transform, "", new(0, -4), new(392, 168), 19, TextAnchor.UpperLeft, Color.white);
        return image;
    }

    private void HandleHover(SkillCardView card, bool entered)
    {
        selection.HoveredCard = entered ? card : selection.HoveredCard == card ? null : selection.HoveredCard;
        foreach (CardVisual item in cards)
        {
            float push = 0;
            if (entered && item.View != card) push = item.View.CardIndex < card.CardIndex ? -24 : 24;
            Vector2 basePosition = item.View.BasePosition;
            item.View.SetRestPosition(new(basePosition.x + push, basePosition.y));
        }
    }

    private void OpenFocus(SkillCardView card)
    {
        selection.FocusedCard = card;
        modalTitle.text = $"{card.TopSkill?.displayName ?? "TOP 없음"} / {card.BottomSkill?.displayName ?? "BOTTOM 없음"}";
        modalTop.text = FocusDescription(card.TopSkill);
        modalBottom.text = FocusDescription(card.BottomSkill);
        modalInitiative.text = $"Initiative  {CombinedInitiative(card.TopSkill, card.BottomSkill)}";
        modalTopImage.GetComponent<Button>().interactable = card.TopSkill != null;
        modalBottomImage.GetComponent<Button>().interactable = card.BottomSkill != null;
        modal.SetActive(true);
        modalCard.localScale = Vector3.one * .82f;
    }

    private void CloseFocus() { selection.FocusedCard = null; if (modal != null) modal.SetActive(false); }
    private void SelectFocusedTop() { SelectHalf(true); }
    private void SelectFocusedBottom() { SelectHalf(false); }

    private void SelectHalf(bool top)
    {
        SkillCardView card = selection.FocusedCard;
        if (card == null || selection.IsConfirmed) return;
        if (top)
        {
            if (selection.SelectedTopCard == card) selection.SelectedTopCard = null;
            else { selection.SelectedTopCard = card; if (selection.SelectedBottomCard == card) selection.SelectedBottomCard = null; }
        }
        else
        {
            if (selection.SelectedBottomCard == card) selection.SelectedBottomCard = null;
            else { selection.SelectedBottomCard = card; if (selection.SelectedTopCard == card) selection.SelectedTopCard = null; }
        }
        RefreshSelectionVisuals();
    }

    private void ConfirmAction()
    {
        if (selection.IsConfirmed || selection.SelectedTopCard == null || selection.SelectedBottomCard == null ||
            selection.SelectedTopCard == selection.SelectedBottomCard) return;
        SkillDefinition top = selection.SelectedTopCard.TopSkill;
        SkillDefinition bottom = selection.SelectedBottomCard.BottomSkill;
        if (!playerSkills.ConfirmCurrentAction(top, bottom)) return;
        selection.IsConfirmed = true;
        CloseFocus();
        RefreshSelectionVisuals();
        PlayerController player = playerSkills.GetComponent<PlayerController>();
        player.turnManager.SubmitPlayerActionCard(playerSkills);
    }

    private void RefreshSelectionVisuals()
    {
        foreach (CardVisual card in cards)
        {
            bool top = selection.SelectedTopCard == card.View;
            bool bottom = selection.SelectedBottomCard == card.View;
            card.View.SetSelection(top, bottom, selection.IsConfirmed);
            card.TopMark.text = top ? "✓ 선택" : "";
            card.BottomMark.text = bottom ? "✓ 선택" : "";
            card.TopShade.color = bottom ? new(.07f, .075f, .09f, .55f) : new(.11f, .12f, .15f, 1f);
            card.BottomShade.color = top ? new(.07f, .075f, .09f, .55f) : new(.11f, .12f, .15f, 1f);
        }
        SkillDefinition topSkill = selection.SelectedTopCard?.TopSkill;
        SkillDefinition bottomSkill = selection.SelectedBottomCard?.BottomSkill;
        if (topSlot != null) topSlot.text = topSkill == null ? "첫 번째 선택\nTOP · 선택 필요" : $"첫 번째 선택\n{topSkill.displayName} · TOP\n{EffectSummary(topSkill)}";
        if (bottomSlot != null) bottomSlot.text = bottomSkill == null ? "두 번째 선택\nBOTTOM · 선택 필요" : $"두 번째 선택\n{bottomSkill.displayName} · BOTTOM\n{EffectSummary(bottomSkill)}";
        bool valid = topSkill != null && bottomSkill != null && selection.SelectedTopCard != selection.SelectedBottomCard;
        if (modal != null && modal.activeSelf && selection.FocusedCard != null)
        {
            bool focusedTop = selection.SelectedTopCard == selection.FocusedCard;
            bool focusedBottom = selection.SelectedBottomCard == selection.FocusedCard;
            modalTopImage.color = focusedBottom ? new(.045f, .05f, .065f, .58f) : focusedTop ? new(.24f, .12f, .07f, 1f) : new(.09f, .1f, .135f, 1f);
            modalBottomImage.color = focusedTop ? new(.045f, .05f, .065f, .58f) : focusedBottom ? new(.055f, .18f, .24f, 1f) : new(.09f, .1f, .135f, 1f);
        }
        if (preview != null) preview.text = valid ? $"이번 라운드 행동\nInitiative {CombinedInitiative(topSkill, bottomSkill)}" : "서로 다른 카드의 TOP과 BOTTOM을 선택하세요";
        if (confirmButton != null) { confirmButton.interactable = valid && !selection.IsConfirmed; confirmLabel.text = selection.IsConfirmed ? "확정 완료" : "행동 확정"; }
    }

    private static int CombinedInitiative(SkillDefinition top, SkillDefinition bottom)
    {
        int value = int.MaxValue;
        if (top != null) value = Mathf.Min(value, Mathf.Max(1, top.initiative));
        if (bottom != null) value = Mathf.Min(value, Mathf.Max(1, bottom.initiative));
        return value == int.MaxValue ? 99 : value;
    }

    private static string FocusDescription(SkillDefinition skill)
    {
        if (skill == null) return "효과 없음";
        return $"{skill.displayName}\n\n{EffectSummary(skill)}\n사거리 {skill.range}\n\n{PlainText(skill.description)}";
    }

    private static string EffectSummary(SkillDefinition skill)
    {
        if (skill?.effects == null || skill.effects.Length == 0) return "효과 없음";
        List<string> parts = new();
        foreach (SkillEffect effect in skill.effects)
        {
            string name = effect.type switch { SkillEffectType.Damage => "공격", SkillEffectType.Heal => "회복", SkillEffectType.Push => "밀치기", SkillEffectType.Pull => "당기기", SkillEffectType.Stun => "기절", SkillEffectType.Immobilize => "이동 불가", SkillEffectType.Move => "이동", SkillEffectType.Jump => "도약", SkillEffectType.Shield => "보호막", _ => "효과" };
            parts.Add(effect.value > 0 ? $"{name} {effect.value}" : effect.duration > 0 ? $"{name} {effect.duration}턴" : name);
        }
        return string.Join(" · ", parts);
    }

    private static string PlainText(string value) { if (string.IsNullOrWhiteSpace(value)) return "추가 설명 없음"; return Regex.Replace(value, @"\[(?:/?color(?:=[^\]]+)?)\]", "", RegexOptions.IgnoreCase).Replace("**", "").Replace("*", ""); }
    private static Button ActionButton(Transform parent, string name, string caption, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action) { GameObject go = Ui(name, parent, typeof(Image), typeof(Button), typeof(Outline)); SetRect(go, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), position, size); go.GetComponent<Image>().color = color; go.GetComponent<Outline>().effectColor = new(1, 1, 1, .2f); Button button = go.GetComponent<Button>(); button.onClick.AddListener(action); Label(go.transform, caption, Vector2.zero, size - new Vector2(8, 8), 18, TextAnchor.MiddleCenter, Color.white, true); return button; }
    private static GameObject Ui(string name, Transform parent, params System.Type[] components) { GameObject go = new(name, components); go.transform.SetParent(parent, false); return go; }
    private static RectTransform SetRect(GameObject go, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size) { RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect; }
    private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, bool bold = false) { GameObject go = Ui("Text", parent, typeof(Text)); Text text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = fontSize; text.alignment = alignment; text.color = color; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false; SetRect(go, new(.5f, .5f), new(.5f, .5f), new(.5f, .5f), position, size); return text; }
    private static void EnsureEventSystem() { if (FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); }
}
