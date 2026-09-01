using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[RequireComponent(typeof(RunMapGenerator))]
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private RunMapGenerator mapGenerator;

    private Button startButton;
    private Text statusText;

    private void Awake()
    {
        if (mapGenerator == null) mapGenerator = GetComponent<RunMapGenerator>();
        BuildMenu();
    }

    public void StartRun()
    {
        if (mapGenerator == null) return;
        startButton.interactable = false;
        statusText.text = Localize("전투 준비 중...", "Preparing combat...");
        mapGenerator.GenerateMap();

        if (mapGenerator.nodes.Count == 0)
        {
            statusText.text = Localize("사용 가능한 룸 템플릿이 없습니다.", "No room templates are available.");
            startButton.interactable = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(mapGenerator.combatSceneName) &&
            !Application.CanStreamedLevelBeLoaded(mapGenerator.combatSceneName))
        {
            statusText.text = Localize("Combat 씬이 빌드 목록에 없습니다.", "The Combat scene is not in the build list.");
            startButton.interactable = true;
            return;
        }

        mapGenerator.SelectNode(0);
    }

    private void BuildMenu()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        GameObject canvasObject = new("Main Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        Image backdrop = CreatePanel(canvasObject.transform, "Backdrop", new Color(.025f, .035f, .065f, 1f));
        Stretch(backdrop.rectTransform);
        Image panel = CreatePanel(backdrop.transform, "Menu Panel", new Color(.06f, .085f, .14f, .97f));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f, .5f);
        panel.rectTransform.sizeDelta = new Vector2(720f, 520f);

        CreateText(panel.transform, "Title", Localize("HEX ROGUELIKE", "HEX ROGUELIKE"), 64,
            new Vector2(0f, 125f), new Vector2(620f, 100f), FontStyle.Bold);
        CreateText(panel.transform, "Subtitle", Localize("룸 템플릿 던전", "ROOM TEMPLATE DUNGEON"), 27,
            new Vector2(0f, 55f), new Vector2(620f, 50f), FontStyle.Normal);

        GameObject buttonObject = new("Start Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
        buttonRect.anchoredPosition = new Vector2(0f, -60f);
        buttonRect.sizeDelta = new Vector2(430f, 92f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(.16f, .5f, .76f, 1f);
        startButton = buttonObject.GetComponent<Button>();
        ColorBlock colors = startButton.colors;
        colors.highlightedColor = new Color(.25f, .68f, .95f, 1f);
        colors.pressedColor = new Color(.09f, .35f, .58f, 1f);
        startButton.colors = colors;
        startButton.onClick.AddListener(StartRun);
        CreateText(buttonObject.transform, "Label", Localize("게임 시작", "START RUN"), 36,
            Vector2.zero, buttonRect.sizeDelta, FontStyle.Bold);

        statusText = CreateText(panel.transform, "Status", string.Empty, 22,
            new Vector2(0f, -145f), new Vector2(620f, 55f), FontStyle.Normal);
        statusText.color = new Color(.75f, .84f, .95f, 1f);
    }

    private static Image CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string name, string value, int size,
        Vector2 position, Vector2 dimensions, FontStyle style)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string Localize(string korean, string english) =>
        Application.systemLanguage == SystemLanguage.Korean ? korean : english;
}
