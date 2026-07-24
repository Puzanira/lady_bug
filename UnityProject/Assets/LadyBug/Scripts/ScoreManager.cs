using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private Text scoreText;
    [SerializeField] private RectTransform popupParent;
    [SerializeField] private RectTransform counterAnchor;

    private int _score;

    public int Score => _score;

    private void Awake()
    {
        Instance = this;
        UpdateText();
    }

    public void AddScore(int delta)
    {
        // Score is just an accumulating achievement now (its own leaderboard
        // category) — the win condition is distance travelled, checked
        // continuously in SpeedController, not score thresholds here.
        _score = Mathf.Max(0, _score + delta);
        UpdateText();
    }

    // worldPos: the position of the player who triggered this — the popup
    // spawns there instead of a fixed point on the road.
    public void SpawnPopup(int value, Vector3 worldPos)
    {
        if (popupParent == null || counterAnchor == null)
            return;

        var go = new GameObject(value >= 0 ? "PopupPlus" : "PopupMinus");
        go.transform.SetParent(popupParent, false);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 84;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = (value >= 0 ? "+" : "") + value;
        text.color = value >= 0 ? new Color(0.25f, 0.9f, 0.3f) : Color.red;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 130f);
        rt.anchoredPosition = ScreenSpaceUtil.WorldToCanvasPoint(worldPos);

        ScorePopup popup = go.AddComponent<ScorePopup>();
        popup.value = value;
        popup.target = counterAnchor;
    }

    private void UpdateText()
    {
        if (scoreText != null)
            scoreText.text = _score.ToString();
    }
}
