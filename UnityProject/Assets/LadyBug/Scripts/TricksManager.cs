using UnityEngine;
using UnityEngine.UI;

// Freestyle-tricks counter for the current session (resets on scene reload).
public class TricksManager : MonoBehaviour
{
    public static TricksManager Instance { get; private set; }

    [SerializeField] private Text tricksText;
    [SerializeField] private RectTransform popupParent;
    [SerializeField] private RectTransform counterAnchor;

    private int _count;

    public int Count => _count;

    private void Awake()
    {
        Instance = this;
        UpdateText();
    }

    public void AddTrick(int amount = 1)
    {
        _count += amount;
        UpdateText();
    }

    // Spawns a "+1 ТРЮК: <name>" popup that flies to the counter and only
    // adds the point once it arrives — mirrors ScoreManager.SpawnPopup.
    // worldPos: where it spawns — the midpoint between the two players for a
    // co-op trick, instead of a fixed point on the road.
    public void SpawnPopup(string trickName, Vector3 worldPos)
    {
        if (popupParent == null || counterAnchor == null)
            return;

        if (SfxManager.Instance != null)
            SfxManager.Instance.PlayTrick();
        if (AchievementStats.Instance != null)
            AchievementStats.Instance.RecordTrick(trickName);

        var go = new GameObject("TrickPopup");
        go.transform.SetParent(popupParent, false);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 56;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "+1 ТРЮК\n" + trickName;
        text.color = new Color(0.7f, 0.4f, 1f); // purple — distinct from the green/red score popups

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 150f);
        rt.anchoredPosition = ScreenSpaceUtil.WorldToCanvasPoint(worldPos);

        TrickPopup popup = go.AddComponent<TrickPopup>();
        popup.target = counterAnchor;
    }

    private void UpdateText()
    {
        if (tricksText != null)
            tricksText.text = _count.ToString();
    }
}
