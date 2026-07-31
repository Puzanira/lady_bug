using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Right-corner counterpart to SpeedIndicator's own left-corner gear+speed
// hub (see SceneSetup.CreateScoreUI's "RightHubPlaceholder") — a happy/sad
// face in the center badge plus a fill/drain animation on its own tick arc,
// reacting to each good/bad object pickup (PlayerController.OnTriggerEnter)
// instead of driving off live telemetry the way the left hub's speed does.
// Tick colors run bottom (index 0) to top (index N-1) red-to-green — the
// reverse of the left hub's own green-to-red, per feedback.
public class ObjectFeedbackIndicator : MonoBehaviour
{
    public static ObjectFeedbackIndicator Instance { get; private set; }

    [SerializeField] private Text faceText;
    [SerializeField] private Image[] ticks;
    [SerializeField] private Color dimTickColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private float fillDuration = 0.4f;

    private Color[] _tickLitColors;
    private int _currentLit;
    private Coroutine _routine;

    private void Awake()
    {
        Instance = this;

        if (ticks != null)
        {
            _tickLitColors = new Color[ticks.Length];
            for (int i = 0; i < ticks.Length; i++)
            {
                float t = ticks.Length > 1 ? (float)i / (ticks.Length - 1) : 0f;
                _tickLitColors[i] = Color.Lerp(Color.red, Color.green, t);
                if (ticks[i] != null)
                    ticks[i].color = dimTickColor;
            }
        }

        if (faceText != null)
            faceText.gameObject.SetActive(false);
    }

    // Fills the whole arc (bottom to top) and leaves it filled.
    public void OnGoodPickup()
    {
        ShowFace(":)", new Color(0.4f, 1f, 0.5f));
        StartFillTo(ticks != null ? ticks.Length : 0);
    }

    // Drains the arc back down to just the bottom (red) tick and leaves it there.
    public void OnBadPickup()
    {
        ShowFace(":(", new Color(1f, 0.4f, 0.3f));
        StartFillTo(1);
    }

    private void ShowFace(string glyph, Color color)
    {
        if (faceText == null)
            return;
        faceText.text = glyph;
        faceText.color = color;
        faceText.gameObject.SetActive(true);
    }

    private void StartFillTo(int targetLit)
    {
        if (ticks == null || ticks.Length == 0)
            return;
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(AnimateFill(targetLit));
    }

    private IEnumerator AnimateFill(int targetLit)
    {
        int startLit = _currentLit;
        float t = 0f;
        while (t < fillDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fillDuration);
            ApplyLit(Mathf.RoundToInt(Mathf.Lerp(startLit, targetLit, p)));
            yield return null;
        }
        ApplyLit(targetLit);
        _currentLit = targetLit;
        _routine = null;
    }

    private void ApplyLit(int lit)
    {
        for (int i = 0; i < ticks.Length; i++)
        {
            if (ticks[i] == null)
                continue;
            ticks[i].color = i < lit ? _tickLitColors[i] : dimTickColor;
        }
    }
}
