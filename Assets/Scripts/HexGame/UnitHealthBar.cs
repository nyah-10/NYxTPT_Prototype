using UnityEngine;

[RequireComponent(typeof(UnitStats))]
public class UnitHealthBar : MonoBehaviour
{
    private UnitStats stats;
    private Transform fill;
    private SpriteRenderer fillRenderer;
    private TextMesh hpText;

    private void Start()
    {
        stats = GetComponent<UnitStats>();
        GameObject root = new GameObject("Health Bar"); root.transform.SetParent(transform, false); root.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        SpriteRenderer background = root.AddComponent<SpriteRenderer>(); background.sprite = CreateSprite(); background.color = new Color(.04f, .04f, .04f, .95f); background.sortingOrder = 30; root.transform.localScale = new Vector3(.82f, .12f, 1f);
        GameObject fillObject = new GameObject("Fill"); fillObject.transform.SetParent(root.transform, false); fill = fillObject.transform; fill.localPosition = new Vector3(-.5f, 0f, -.1f);
        fillRenderer = fillObject.AddComponent<SpriteRenderer>(); fillRenderer.sprite = background.sprite; fillRenderer.color = Color.green; fillRenderer.sortingOrder = 31;

        GameObject textObject = new GameObject("Health Value"); textObject.transform.SetParent(transform, false); textObject.transform.localPosition = new Vector3(0f, .87f, -.2f);
        hpText = textObject.AddComponent<TextMesh>();
        hpText.anchor = TextAnchor.MiddleCenter;
        hpText.alignment = TextAlignment.Center;
        hpText.fontSize = 48;
        hpText.characterSize = .075f;
        hpText.fontStyle = FontStyle.Bold;
        hpText.color = Color.white;
        MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
        textRenderer.sortingOrder = 32;
    }

    private void Update()
    {
        if (fill == null || hpText == null) return;
        float ratio = Mathf.Clamp01((float)stats.CurrentHP / stats.MaxHP);
        fill.localScale = new Vector3(ratio, .8f, 1f);
        fill.localPosition = new Vector3((ratio - 1f) * .5f, 0f, -.1f);
        fillRenderer.color = ratio > .5f ? new Color(.2f, .9f, .3f) : ratio > .25f ? new Color(1f, .75f, .08f) : new Color(1f, .18f, .12f);
        hpText.text = $"{stats.CurrentHP} / {stats.MaxHP}";
    }

    private static Sprite CreateSprite()
    {
        Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
    }
}
