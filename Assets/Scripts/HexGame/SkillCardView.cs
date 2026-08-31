using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int CardIndex { get; private set; }
    public SkillDefinition TopSkill { get; private set; }
    public SkillDefinition BottomSkill { get; private set; }
    public Vector2 BasePosition { get; private set; }

    private RectTransform rect;
    private Image background;
    private Outline outline;
    private Canvas cardCanvas;
    private Vector2 rest;
    private bool hovered;
    private bool topSelected;
    private bool bottomSelected;
    private bool locked;
    private Action<SkillCardView, bool> hoverChanged;
    private Action<SkillCardView> clicked;

    public void Initialize(int index, SkillDefinition top, SkillDefinition bottom, RectTransform cardRect,
        Image cardBackground, Outline cardOutline, Action<SkillCardView, bool> onHover, Action<SkillCardView> onClick)
    {
        CardIndex = index;
        TopSkill = top;
        BottomSkill = bottom;
        rect = cardRect;
        background = cardBackground;
        outline = cardOutline;
        cardCanvas = GetComponent<Canvas>();
        rest = rect.anchoredPosition;
        BasePosition = rest;
        hoverChanged = onHover;
        clicked = onClick;
    }

    public void SetRestPosition(Vector2 position) { rest = position; }
    public void CaptureRestPosition() { rest = rect.anchoredPosition; BasePosition = rest; }
    public void SetSelection(bool hasTop, bool hasBottom, bool isLocked)
    { topSelected = hasTop; bottomSelected = hasBottom; locked = isLocked; }
    public void OnPointerEnter(PointerEventData eventData) { if (!locked) { hovered = true; hoverChanged?.Invoke(this, true); } }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; hoverChanged?.Invoke(this, false); }
    public void OnPointerClick(PointerEventData eventData) { if (!locked && eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke(this); }

    private void Update()
    {
        if (rect == null) return;
        bool chosen = topSelected || bottomSelected;
        float lift = hovered ? 38f : chosen ? 14f : 0f;
        float scale = hovered ? 1.15f : chosen ? 1.035f : 1f;
        float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, rest + Vector2.up * lift, blend);
        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * scale, blend);
        background.color = Color.Lerp(background.color, locked ? new Color(.055f, .06f, .075f, .82f) :
            chosen ? new Color(.14f, .15f, .19f, 1f) : new Color(.085f, .095f, .12f, 1f), blend);
        outline.effectColor = chosen ? new Color(1f, .82f, .3f, 1f) :
            hovered ? new Color(.65f, .85f, 1f, .95f) : new Color(.26f, .34f, .45f, .72f);
        if (cardCanvas != null) cardCanvas.sortingOrder = hovered ? 20 : topSelected || bottomSelected ? 10 : 0;
    }
}
