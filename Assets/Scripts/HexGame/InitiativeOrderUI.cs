using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class InitiativeOrderUI : MonoBehaviour
{
    private TurnManager turnManager;
    private Text orderText;
    private string lastValue;

    private void Start()
    {
        turnManager = FindFirstObjectByType<TurnManager>();
        CreateBar();
    }

    private void Update()
    {
        if (turnManager == null || orderText == null) return;
        StringBuilder builder = new("ROUND ORDER   ");
        foreach (string entry in turnManager.InitiativeOrder)
        {
            bool active = turnManager.CurrentCombatant != null && entry.StartsWith(turnManager.CurrentCombatant.name + " ");
            builder.Append(active ? "[ " : " ");
            builder.Append(entry);
            builder.Append(active ? " ]" : "  |");
        }
        if (turnManager.Phase == RoundPhase.CardSelection) builder.Append("  Select action cards");
        string value = builder.ToString();
        if (value == lastValue) return;
        lastValue = value;
        orderText.text = value;
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

        GameObject labelObject = new("Order Text", typeof(Text));
        labelObject.transform.SetParent(panel.transform, false);
        orderText = labelObject.GetComponent<Text>();
        orderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        orderText.fontSize = 20;
        orderText.alignment = TextAnchor.MiddleCenter;
        orderText.color = Color.white;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
    }
}
