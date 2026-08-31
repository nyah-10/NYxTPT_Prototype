using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillDefinition Skill { get; private set; }
    private RectTransform rect;
    private Image background;
    private Outline outline;
    private Color accent;
    private Vector2 rest;
    private int restSibling;
    private bool hovered, selected, usable = true;

    public void Initialize(SkillDefinition skill, RectTransform cardRect, Image cardBackground, Outline cardOutline, Color cardAccent)
    { Skill = skill; rect = cardRect; background = cardBackground; outline = cardOutline; accent = cardAccent; rest = rect.anchoredPosition; restSibling = transform.GetSiblingIndex(); }
    public void SetState(bool canUse, bool isSelected) { usable = canUse; selected = isSelected; GetComponent<Button>().interactable = canUse; }
    public void OnPointerEnter(PointerEventData eventData) { hovered = usable; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }

    private void Update()
    {
        if (rect == null) return;
        float lift = selected ? 42 : hovered ? 28 : 0;
        float scale = selected ? 1.06f : hovered ? 1.035f : 1;
        // Unscaled time keeps the hand responsive while gameplay is paused.
        float blend = 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, rest + Vector2.up * lift, blend);
        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * scale, blend);
        Color target = usable ? selected ? new(.14f, .15f, .18f, 1) : new(.095f, .105f, .13f, 1) : new(.055f, .06f, .07f, .72f);
        background.color = Color.Lerp(background.color, target, blend);
        outline.effectColor = usable ? new(accent.r, accent.g, accent.b, selected ? 1 : hovered ? .88f : .62f) : new(.22f, .24f, .27f, .45f);
        if (selected || hovered) transform.SetAsLastSibling(); else if (transform.GetSiblingIndex() != restSibling) transform.SetSiblingIndex(restSibling);
    }
}
