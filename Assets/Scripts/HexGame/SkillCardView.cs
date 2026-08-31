using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public SkillDefinition Skill { get; private set; }

    private RectTransform rect;
    private Image background;
    private Outline outline;
    private Canvas cardCanvas;
    private SkillHandLayout hand;
    private Action clicked;
    private Color accent;
    private Vector2 targetPosition;
    private Vector2 dragPosition;
    private Vector2 previousDragPosition;
    private float targetRotation;
    private float dragTilt;
    private int handIndex;
    private bool hovered;
    private bool selected;
    private bool usable = true;
    private bool dragging;
    private bool movedDuringDrag;

    public void Initialize(SkillDefinition skill, RectTransform cardRect, Image cardBackground,
        Outline cardOutline, Color cardAccent, Action onClick)
    {
        Skill = skill;
        rect = cardRect;
        background = cardBackground;
        outline = cardOutline;
        accent = cardAccent;
        clicked = onClick;
        cardCanvas = GetComponent<Canvas>();
        targetPosition = rect.anchoredPosition;
    }

    public void AttachToHand(SkillHandLayout owner) => hand = owner;

    public void SetHandTarget(Vector2 position, float rotation, int index)
    {
        targetPosition = position;
        targetRotation = rotation;
        handIndex = index;
    }

    public void SetState(bool canUse, bool isSelected)
    {
        usable = canUse;
        selected = isSelected;
        GetComponent<Button>().interactable = canUse;
    }

    public void OnPointerDown(PointerEventData eventData) => movedDuringDrag = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!usable || dragging) return;
        hovered = true;
        hand?.SetHovered(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (dragging) return;
        hovered = false;
        hand?.SetHovered(this, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (usable && !movedDuringDrag && eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!usable || eventData.button != PointerEventData.InputButton.Left) return;
        dragging = true;
        movedDuringDrag = false;
        dragPosition = rect.anchoredPosition;
        previousDragPosition = dragPosition;
        hand?.BeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || hand == null) return;
        RectTransform handRect = hand.GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(handRect, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
        movedDuringDrag |= Vector2.Distance(local, dragPosition) > 6f;
        previousDragPosition = dragPosition;
        dragPosition = local;
        dragTilt = Mathf.Clamp((dragPosition.x - previousDragPosition.x) * -.65f, -12f, 12f);
        hand.DragTo(this, local.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;
        hovered = RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera);
        hand?.EndDrag(this);
        hand?.SetHovered(this, hovered);
    }

    private void Update()
    {
        if (rect == null) return;
        float lift = selected ? 42f : hovered ? 32f : 0f;
        float scale = hovered || dragging ? 1.1f : selected ? 1.055f : 1f;
        Vector2 desiredPosition = dragging ? dragPosition : targetPosition + Vector2.up * lift;
        float desiredRotation = dragging ? dragTilt : targetRotation;
        float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);

        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, desiredPosition, blend);
        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * scale, blend);
        rect.localRotation = Quaternion.Slerp(rect.localRotation, Quaternion.Euler(0, 0, desiredRotation), blend);
        dragTilt = Mathf.Lerp(dragTilt, 0f, blend);

        Color target = usable ? selected ? new(.14f, .15f, .18f, 1f) : new(.095f, .105f, .13f, 1f) : new(.055f, .06f, .07f, .72f);
        background.color = Color.Lerp(background.color, target, blend);
        outline.effectColor = usable ? new(accent.r, accent.g, accent.b, selected ? 1f : hovered ? .9f : .62f) : new(.22f, .24f, .27f, .45f);
        if (cardCanvas != null) cardCanvas.sortingOrder = dragging ? 100 : hovered || selected ? 50 : handIndex;
    }
}
