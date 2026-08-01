using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

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

    // A real drawn round face (SceneSetup.CreateSmileyTexture), not a text
    // glyph — this project's own font is missing some Unicode glyphs in an
    // actual build (see CreateSingleArrow's own comment on the same issue
    // with arrow characters), so an emoji character here would risk the
    // same blank-glyph problem.
    [SerializeField] private RawImage faceImage;
    [SerializeField] private Texture2D happyFaceTexture;
    [SerializeField] private Texture2D sadFaceTexture;
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

        if (faceImage != null)
            faceImage.gameObject.SetActive(false);
    }

    // Fills the whole arc (bottom to top) and leaves it filled.
    public void OnGoodPickup()
    {
        ShowFace(happyFaceTexture);
        StartFillTo(ticks != null ? ticks.Length : 0);
    }

    // Drains the arc back down to just the bottom (red) tick and leaves it there.
    public void OnBadPickup()
    {
        ShowFace(sadFaceTexture);
        StartFillTo(1);
    }

    private void ShowFace(Texture2D texture)
    {
        if (faceImage == null)
            return;
        faceImage.texture = texture;
        faceImage.gameObject.SetActive(true);
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
}
