using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InitiativeOrderUI : MonoBehaviour
{
    private TurnManager turnManager;
    private Transform orderRow;
    private Text revealText;
    private Text feedbackText;
    private float feedbackUntil;

    private void Start()
    {
        turnManager = FindAnyObjectByType<TurnManager>();
        CreateBar();
        if (turnManager != null)
        {
            turnManager.QueueChanged += Refresh;
            turnManager.FeedbackRequested += ShowFeedback;
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (turnManager == null) return;
        turnManager.QueueChanged -= Refresh;
        turnManager.FeedbackRequested -= ShowFeedback;
    }

    private void Update()
    {
        if (turnManager == null) return;
        if (feedbackText != null && feedbackText.gameObject.activeSelf && Time.unscaledTime >= feedbackUntil)
            feedbackText.gameObject.SetActive(false);
        if (turnManager.Phase == RoundPhase.CardSelection)
        {
            System.Text.StringBuilder builder = new("공개된 몬스터 카드  ");
            foreach (EnemyController enemy in FindObjectsByType<EnemyController>())
            {
                UnitStats stats = enemy.GetComponent<UnitStats>();
                if (stats == null || stats.IsDead) continue;
                builder.Append(enemy.name).Append(": ").Append(enemy.RevealedCardSummary).Append("    ");
            }
            revealText.text = builder.ToString();
            revealText.gameObject.SetActive(true);
        }
        else revealText.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (orderRow == null || turnManager == null) return;
        for (int i = orderRow.childCount - 1; i >= 0; i--) Destroy(orderRow.GetChild(i).gameObject);
        List<TurnQueueItem> queue = turnManager.GetTurnQueueSnapshot();
        foreach (TurnQueueItem item in queue) CreateQueueBadge(item);
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);
        feedbackUntil = Time.unscaledTime + 2.2f;
    }

    private void CreateBar()
    {
        GameObject canvasObject = new("Initiative Order HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panel = new("Round Order", typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        Image image = panel.GetComponent<Image>();
        image.color = new Color(.02f, .04f, .08f, .9f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(.5f, 1f);
        panelRect.anchorMax = new Vector2(.5f, 1f);
        panelRect.pivot = new Vector2(.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -18f);
        panelRect.sizeDelta = new Vector2(1240f, 54f);

        GameObject row = new("Queue", typeof(HorizontalLayoutGroup));
        row.transform.SetParent(panel.transform, false);
        orderRow = row.transform;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10; layout.childAlignment = TextAnchor.MiddleCenter; layout.childForceExpandWidth = false;
        Stretch(row.GetComponent<RectTransform>());

        revealText = CreateText(canvasObject.transform, "Monster Cards", 18, TextAnchor.MiddleCenter);
        SetRect(revealText.rectTransform, new(.5f, 1), new(.5f, 1), new(.5f, 1), new(0, -82), new(1500, 44));
        feedbackText = CreateText(canvasObject.transform, "Combat Feedback", 24, TextAnchor.MiddleCenter);
        feedbackText.color = new Color(1f, .84f, .32f);
        SetRect(feedbackText.rectTransform, new(.5f, 1), new(.5f, 1), new(.5f, 1), new(0, -134), new(900, 48));
        feedbackText.gameObject.SetActive(false);
    }

    private void CreateQueueBadge(TurnQueueItem item)
    {
        GameObject badge = new(item.Combatant == null ? "Missing" : item.Combatant.name, typeof(Image), typeof(LayoutElement));
        badge.transform.SetParent(orderRow, false);
        badge.GetComponent<LayoutElement>().preferredWidth = 185;
        Color color = item.IsPlayer ? new(.08f, .34f, .62f, .95f) : new(.52f, .14f, .12f, .95f);
        if (item.State == QueueEntryState.Acting) color = new(.85f, .55f, .08f, 1f);
        else if (item.State == QueueEntryState.SkippedDead || item.State == QueueEntryState.SkippedDisabled) color = new(.18f, .18f, .2f, .72f);
        else if (item.State == QueueEntryState.Completed) color.a = .5f;
        badge.GetComponent<Image>().color = color;
        GameObject iconObject = new("Unit Icon", typeof(Image));
        iconObject.transform.SetParent(badge.transform, false);
        Image icon = iconObject.GetComponent<Image>();
        SpriteRenderer unitRenderer = item.Combatant == null ? null : item.Combatant.GetComponentInChildren<SpriteRenderer>();
        icon.sprite = unitRenderer == null ? null : unitRenderer.sprite;
        icon.preserveAspect = true;
        icon.color = icon.sprite == null ? new Color(1f, 1f, 1f, .2f) : Color.white;
        SetRect(icon.rectTransform, new(0, .5f), new(0, .5f), new(0, .5f), new(7, 0), new(38, 38));
        Text label = CreateText(badge.transform, "Label", 17, TextAnchor.MiddleCenter);
        string state = item.State switch { QueueEntryState.SkippedDead => " · 사망", QueueEntryState.SkippedDisabled => " · 행동 불가", _ => "" };
        string activeMarker = item.State == QueueEntryState.Acting ? "▶ " : "";
        label.text = $"{activeMarker}{(item.IsPlayer ? "◆" : "●")} {item.Combatant?.name ?? "유닛"}  {item.Initiative}{state}";
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(44, 0); label.rectTransform.offsetMax = Vector2.zero;
    }

    private static Text CreateText(Transform parent, string name, int size, TextAnchor alignment)
    {
        GameObject go = new(name, typeof(Text)); go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.alignment = alignment; text.color = Color.white; return text;
    }

    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
}
