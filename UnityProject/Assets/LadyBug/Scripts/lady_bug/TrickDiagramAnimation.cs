using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Generic two-player trick instruction diagram — plays back a small
// scripted path of (lane, height) waypoints for two ladybug icons on a
// simple 3-lane track, looping. Used by the 5 newer trick pages (ЧЕХАРДА,
// СИНХРОН, ЗАВИСАНИЕ, БОЛЬШОЕ КОЛЬЦО, БЕСКОНЕЧНОСТЬ) instead of a bespoke
// animator per trick like ArchTrickAnimation/RingTrickAnimation — they're
// all fundamentally "two icons stepping between 3 lane slots at different
// heights over time," just with a different path.
//
// Configured entirely from SceneSetup.cs right after AddComponent (public
// fields, not [SerializeField] — this is build-time-only wiring, the paths
// are plain struct arrays that would be painful to round-trip through
// SerializedProperty, and nothing here needs Inspector visibility since
// the scene is always regenerated fresh, never hand-edited).
public class TrickDiagramAnimation : MonoBehaviour
{
    [System.Serializable]
    public struct Step
    {
        public int lane; // 0=left, 1=middle, 2=right
        public float y; // page-relative Y for this waypoint — explicit, not derived, so a "stacked on partner" pose can sit above normal ground level
        public bool airborne; // sprite = the wings-out air texture, same as a real jump/bounce
        public bool duck; // squash vertically in place, same as a real duck — mutually exclusive with airborne in practice
        public float travelDuration;
        public float holdDuration;
        // Suppresses this step's own automatic arrow (see PlayPath's
        // ArrowGlyph) — default false everywhere (every existing trick page
        // keeps its arrow on every step), used by ЗАВИСАНИЕ's down-bounce
        // steps so only the up bounce (which is what actually counts a
        // rep) shows an arrow, not both directions.
        public bool hideArrow;
    }

    public RectTransform bugA;
    public RectTransform bugB;
    public Step[] pathA = new Step[0];
    public Step[] pathB = new Step[0];
    public GameObject successText;
    public Texture2D airTextureA;
    public Texture2D airTextureB;

    // Small directional glyph that hovers just above each bug, showing
    // which way THAT specific step moves — swapped/shown per step, hidden
    // between them, same idea as GestureDiagramAnimation's own arrows.
    public RectTransform arrowA;
    public RectTransform arrowB;
    public Vector2 arrowOffset = new Vector2(0f, 115f);

    // B starts this long after A — "one lags one step behind the other"
    // instead of both moving in lockstep. 0 = simultaneous (the default,
    // used by every trick except the ones that explicitly need this).
    public float staggerDelay = 0f;

    // Optional digit counter shown between the two bugs for a specific
    // window of the cycle — ЗАВИСАНИЕ's "1 2 3 4 5" while both are
    // airborne. Inert (never shown) unless counterText is actually wired
    // and counterEnd > counterStart.
    public Text counterText;
    public float counterStart;
    public float counterEnd;
    public int counterMin = 1;
    public int counterMax = 5;

    public float laneSpacing = 210f;
    public float duckSquash = 0.6f;
    public float holdSuccess = 1.2f;
    public float holdAfterSuccess = 0.6f;
    // "ТРЮК ВЫПОЛНЕН +1" blinks a few times before settling into its
    // holdSuccess steady-on window, instead of just snapping on — reads as
    // a much stronger "you did it" cue than a flat toggle.
    public int successBlinkCount = 3;
    public float successBlinkInterval = 0.12f;

    // One full loop's length — StartScreenController's carousel reads this
    // so it doesn't cut the animation off mid-cycle, same idea as
    // ArchTrickAnimation.CycleDuration.
    public float CycleDuration => Mathf.Max(PathDuration(pathA), staggerDelay + PathDuration(pathB))
        + successBlinkCount * 2f * successBlinkInterval + holdSuccess + holdAfterSuccess;

    // StartScreenController's carousel shows every trick instruction page
    // for exactly this many full loops before advancing.
    public const int RepeatCount = 3;
    public float TotalDisplayDuration => CycleDuration * RepeatCount;

    private static float PathDuration(Step[] path)
    {
        float total = 0f;
        if (path != null)
            foreach (var step in path)
                total += step.travelDuration + step.holdDuration;
        return total;
    }

    private Vector2 _restA, _restB;
    private Vector3 _restScaleA = Vector3.one, _restScaleB = Vector3.one;
    private RawImage _imageA, _imageB;
    private Texture2D _groundTextureA, _groundTextureB;

    private void Awake()
    {
        if (bugA != null)
        {
            _restA = bugA.anchoredPosition;
            _restScaleA = bugA.localScale;
            _imageA = bugA.GetComponent<RawImage>();
            if (_imageA != null)
                _groundTextureA = _imageA.texture as Texture2D;
        }
        if (bugB != null)
        {
            _restB = bugB.anchoredPosition;
            _restScaleB = bugB.localScale;
            _imageB = bugB.GetComponent<RawImage>();
            if (_imageB != null)
                _groundTextureB = _imageB.texture as Texture2D;
        }
    }

    private void OnEnable()
    {
        ResetVisuals();
        StartCoroutine(Animate());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void ResetVisuals()
    {
        if (bugA != null)
        {
            bugA.anchoredPosition = _restA;
            bugA.localScale = _restScaleA;
            if (_imageA != null && _groundTextureA != null)
                _imageA.texture = _groundTextureA;
        }
        if (bugB != null)
        {
            bugB.anchoredPosition = _restB;
            bugB.localScale = _restScaleB;
            if (_imageB != null && _groundTextureB != null)
                _imageB.texture = _groundTextureB;
        }
        if (successText != null)
            successText.SetActive(false);
        if (arrowA != null)
            arrowA.gameObject.SetActive(false);
        if (arrowB != null)
            arrowB.gameObject.SetActive(false);
        if (counterText != null)
            counterText.gameObject.SetActive(false);
    }

    private IEnumerator Animate()
    {
        while (true)
        {
            Coroutine a = StartCoroutine(PlayPath(bugA, pathA, _restScaleA, _imageA, _groundTextureA, airTextureA, arrowA));
            if (staggerDelay > 0f)
                yield return new WaitForSeconds(staggerDelay);
            Coroutine b = StartCoroutine(PlayPath(bugB, pathB, _restScaleB, _imageB, _groundTextureB, airTextureB, arrowB));
            if (counterText != null && counterEnd > counterStart)
                StartCoroutine(RunCounter());
            yield return a;
            yield return b;

            if (successText != null)
                yield return BlinkSuccessText();

            ResetVisuals();
            yield return new WaitForSeconds(holdAfterSuccess);
        }
    }

    private IEnumerator BlinkSuccessText()
    {
        for (int i = 0; i < successBlinkCount; i++)
        {
            successText.SetActive(true);
            yield return new WaitForSeconds(successBlinkInterval);
            successText.SetActive(false);
            yield return new WaitForSeconds(successBlinkInterval);
        }
        successText.SetActive(true);
        yield return new WaitForSeconds(holdSuccess);
        successText.SetActive(false);
    }

    private IEnumerator RunCounter()
    {
        yield return new WaitForSeconds(counterStart);
        counterText.gameObject.SetActive(true);
        int count = counterMax - counterMin + 1;
        float perDigit = (counterEnd - counterStart) / count;
        for (int n = counterMin; n <= counterMax; n++)
        {
            counterText.text = n.ToString();
            yield return new WaitForSeconds(perDigit);
        }
        counterText.gameObject.SetActive(false);
    }

    private IEnumerator PlayPath(RectTransform bug, Step[] path, Vector3 restScale, RawImage image, Texture2D groundTexture, Texture2D airTexture, RectTransform arrow)
    {
        if (bug == null || path == null)
            yield break;

        bool first = true;
        foreach (var step in path)
        {
            Vector2 target = new Vector2((step.lane - 1) * laneSpacing, step.y);
            Vector3 targetScale = step.duck
                ? new Vector3(restScale.x, restScale.y * duckSquash, restScale.z)
                : restScale;

            if (image != null)
                image.texture = step.airborne && airTexture != null ? airTexture : groundTexture;

            Vector2 startPos = bug.anchoredPosition;
            Vector3 startScale = bug.localScale;

            // No arrow on the very first step — it's just settling into the
            // starting pose, not moving anywhere yet. hideArrow lets a
            // specific step opt out too (see Step.hideArrow).
            string glyph = (first || step.hideArrow) ? null : ArrowGlyph(target - startPos);
            first = false;
            if (arrow != null)
            {
                Text arrowText = arrow.GetComponent<Text>();
                if (glyph != null)
                {
                    if (arrowText != null)
                        arrowText.text = glyph;
                    arrow.anchoredPosition = startPos + arrowOffset;
                    arrow.gameObject.SetActive(true);
                }
                else
                {
                    arrow.gameObject.SetActive(false);
                }
            }

            float t = 0f;
            while (t < step.travelDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / step.travelDuration);
                bug.anchoredPosition = Vector2.Lerp(startPos, target, p);
                bug.localScale = Vector3.Lerp(startScale, targetScale, p);
                if (arrow != null && glyph != null)
                    arrow.anchoredPosition = bug.anchoredPosition + arrowOffset;
                yield return null;
            }
            bug.anchoredPosition = target;
            bug.localScale = targetScale;
            if (arrow != null)
                arrow.gameObject.SetActive(false);

            if (step.holdDuration > 0f)
                yield return new WaitForSeconds(step.holdDuration);
        }
    }

    // Picks the 4-way (or diagonal) glyph closest to the actual step
    // direction — lane changes are always a full laneSpacing apart so the
    // horizontal/vertical threshold just needs to reject noise, not tune a
    // real ambiguous case.
    private static string ArrowGlyph(Vector2 delta)
    {
        bool horiz = Mathf.Abs(delta.x) > 1f;
        bool vert = Mathf.Abs(delta.y) > 1f;
        if (horiz && vert)
        {
            if (delta.x > 0f) return delta.y > 0f ? "↗" : "↘";
            return delta.y > 0f ? "↖" : "↙";
        }
        if (horiz)
            return delta.x > 0f ? "→" : "←";
        if (vert)
            return delta.y > 0f ? "↑" : "↓";
        return null;
    }
}
