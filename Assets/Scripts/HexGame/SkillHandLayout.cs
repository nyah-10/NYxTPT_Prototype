using System.Collections.Generic;
using UnityEngine;

public sealed class SkillHandLayout : MonoBehaviour
{
    [SerializeField] private float handCenterX = -130f;
    [SerializeField] private float availableWidth = 1070f;
    [SerializeField] private float cardWidth = 250f;
    [SerializeField] private float preferredGap = 20f;
    [SerializeField] private float minimumStep = 72f;
    [SerializeField] private float arcHeight = 24f;
    [SerializeField] private float maxRotation = 5f;

    private readonly List<SkillCardView> cards = new();
    private SkillCardView hoveredCard;
    private SkillCardView draggedCard;

    public void Register(SkillCardView card)
    {
        if (card == null || cards.Contains(card)) return;
        cards.Add(card);
        card.AttachToHand(this);
        RefreshTargets();
    }

    public void SetHovered(SkillCardView card, bool hovered)
    {
        hoveredCard = hovered ? card : hoveredCard == card ? null : hoveredCard;
        RefreshTargets();
    }

    public void BeginDrag(SkillCardView card)
    {
        draggedCard = card;
        hoveredCard = card;
        RefreshTargets();
    }

    public void DragTo(SkillCardView card, float localX)
    {
        if (draggedCard != card || cards.Count < 2) return;
        int oldIndex = cards.IndexOf(card);
        int newIndex = FindClosestSlot(localX);
        if (oldIndex == newIndex) return;
        cards.RemoveAt(oldIndex);
        cards.Insert(newIndex, card);
        RefreshTargets();
    }

    public void EndDrag(SkillCardView card)
    {
        if (draggedCard != card) return;
        draggedCard = null;
        RefreshTargets();
    }

    private int FindClosestSlot(float x)
    {
        GetSpacing(out float startX, out float step);
        return Mathf.Clamp(Mathf.RoundToInt((x - startX) / Mathf.Max(1f, step)), 0, cards.Count - 1);
    }

    private void RefreshTargets()
    {
        if (cards.Count == 0) return;
        GetSpacing(out float startX, out float step);
        int hoveredIndex = hoveredCard == null ? -1 : cards.IndexOf(hoveredCard);

        for (int i = 0; i < cards.Count; i++)
        {
            float normalized = cards.Count == 1 ? 0f : i / (cards.Count - 1f) * 2f - 1f;
            float x = startX + i * step;
            float y = 18f + (1f - Mathf.Abs(normalized)) * arcHeight;
            float rotation = -normalized * maxRotation;

            if (hoveredIndex >= 0 && i != hoveredIndex)
            {
                float distance = Mathf.Abs(i - hoveredIndex);
                float push = Mathf.Lerp(38f, 8f, Mathf.Clamp01((distance - 1f) / 3f));
                x += i < hoveredIndex ? -push : push;
            }

            cards[i].SetHandTarget(new Vector2(x, y), rotation, i);
        }
    }

    private void GetSpacing(out float startX, out float step)
    {
        if (cards.Count <= 1)
        {
            step = 0f;
            startX = handCenterX;
            return;
        }

        // Cards retain their readable size; only their overlap grows as the hand fills.
        step = Mathf.Clamp((availableWidth - cardWidth) / (cards.Count - 1f), minimumStep, cardWidth + preferredGap);
        float totalWidth = cardWidth + step * (cards.Count - 1);
        startX = handCenterX - totalWidth * .5f + cardWidth * .5f;
    }
}
