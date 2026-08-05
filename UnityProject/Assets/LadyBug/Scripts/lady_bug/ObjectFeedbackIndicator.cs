using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

// Right-corner counterpart to SpeedIndicator's own left-corner gear+speed
// hub (see SceneSetup.CreateScoreUI's "RightHubPlaceholder") — a happy/sad
// face in the center badge plus a fill animation on its own tick arc,
// reacting to each good/bad object pickup (PlayerController.OnTriggerEnter).
// Zero sits at the arc centre: good pickups light the upper half (green at
// the top), bad pickups light the lower half (red at the bottom); the other
// half stays dim.
public class ObjectFeedbackIndicator : MonoBehaviour
{
    public static ObjectFeedbackIndicator Instance { get; private set; }

    [SerializeField] private RawImage faceImage;
    [SerializeField] private Texture2D happyFaceTexture;
    [SerializeField] private Texture2D sadFaceTexture;
    [SerializeField] private Image[] ticks;
    [SerializeField] private Color dimTickColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private float fillDuration = 0.4f;

    private Color[] _tickLitColors;
    private int _splitIndex;
    private int _upperLit;
    private int _lowerLit;
    private Coroutine _routine;

    private void Awake()
    {
        Instance = this;

        if (ticks != null && ticks.Length > 0)
        {
            _splitIndex = ticks.Length / 2;
            _tickLitColors = new Color[ticks.Length];
            Color centerColor = new Color(0.85f, 0.85f, 0.75f);

            for (int i = 0; i < ticks.Length; i++)
            {
                if (i < _splitIndex)
                {
                    float t = _splitIndex > 1 ? (float)(_splitIndex - 1 - i) / (_splitIndex - 1) : 1f;
                    _tickLitColors[i] = Color.Lerp(centerColor, Color.red, t);
                }
                else
                {
                    int upperCount = ticks.Length - _splitIndex;
                    float t = upperCount > 1 ? (float)(i - _splitIndex) / (upperCount - 1) : 1f;
                    _tickLitColors[i] = Color.Lerp(centerColor, Color.green, t);
                }

                if (ticks[i] != null)
                    ticks[i].color = dimTickColor;
            }
        }

        if (faceImage != null)
            faceImage.gameObject.SetActive(false);
    }

    public void OnGoodPickup()
    {
        ShowFace(happyFaceTexture);
        StartAnimate(upperTarget: ticks.Length - _splitIndex, lowerTarget: 0);
    }

    public void OnBadPickup()
    {
        ShowFace(sadFaceTexture);
        StartAnimate(upperTarget: 0, lowerTarget: _splitIndex);
    }

    private void ShowFace(Texture2D texture)
    {
        if (faceImage == null)
            return;
        faceImage.texture = texture;
        faceImage.gameObject.SetActive(true);
    }

    private void StartAnimate(int upperTarget, int lowerTarget)
    {
        if (ticks == null || ticks.Length == 0)
            return;
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(AnimateFill(upperTarget, lowerTarget));
    }

    private IEnumerator AnimateFill(int upperTarget, int lowerTarget)
    {
        int startUpper = _upperLit;
        int startLower = _lowerLit;
        float t = 0f;
        while (t < fillDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fillDuration);
            ApplyLit(
                Mathf.RoundToInt(Mathf.Lerp(startUpper, upperTarget, p)),
                Mathf.RoundToInt(Mathf.Lerp(startLower, lowerTarget, p)));
            yield return null;
        }

        ApplyLit(upperTarget, lowerTarget);
        _upperLit = upperTarget;
        _lowerLit = lowerTarget;
        _routine = null;
    }

    private void ApplyLit(int upperLit, int lowerLit)
    {
        for (int i = 0; i < ticks.Length; i++)
        {
            if (ticks[i] == null)
                continue;

            bool lit = i >= _splitIndex
                ? i - _splitIndex < upperLit
                : _splitIndex - 1 - i < lowerLit;
            ticks[i].color = lit ? _tickLitColors[i] : dimTickColor;
        }
    }
}
}
