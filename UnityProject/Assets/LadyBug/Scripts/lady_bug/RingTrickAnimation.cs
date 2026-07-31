using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animated demo for the "ТРЮК: КОЛЬЦО" instruction page — three sequential
// beats instead of one continuous simultaneous cross (see
// PlayerController.TryDetectRingTrick for the real mechanic): airBug rises
// straight up alone first (groundBug stays put), then the two cross —
// airBug sliding over the top, groundBug sliding under the bottom, opposite
// directions — and finally airBug alone comes back down. Each beat gets its
// own single directional arrow, not one static pair shown for the whole
// thing.
public class RingTrickAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform airBug; // rises, crosses over the top, then comes back down
    [SerializeField] private RectTransform groundBug; // stays put, then crosses underneath at ground level

    // Single reusable arrow per bug, glyph/position swapped per beat
    // instead of a fixed pair shown for the whole animation — see
    // TrickDiagramAnimation's own per-step arrow for the same idea.
    [SerializeField] private RectTransform airArrow;
    [SerializeField] private RectTransform groundArrow;
    [SerializeField] private Vector2 arrowOffset = new Vector2(0f, 115f);

    [SerializeField] private GameObject successText;

    // Wings-out sprite while airBug is actually airborne — same real
    // in-game pose swap PlayerAnimator uses (see ArchTrickAnimation's own
    // topBugAirTexture comment).
    [SerializeField] private Texture2D airBugAirTexture;
    private RawImage _airBugImage;
    private Texture2D _airBugNormalTexture;

    [SerializeField] private float holdNeutral = 1.2f;
    [SerializeField] private float riseDuration = 0.5f;
    [SerializeField] private float holdBetweenBeats = 0.6f;
    [SerializeField] private float crossDuration = 0.9f;
    [SerializeField] private float arcHeight = 70f;
    [SerializeField] private float holdCrossed = 0.8f;
    [SerializeField] private float holdSuccess = 1.6f;
    [SerializeField] private float holdAfterSuccess = 0.8f;
    // "ТРЮК ВЫПОЛНЕН +1" blinks a few times before settling into its
    // holdSuccess steady-on window, instead of just snapping on — reads as
    // a much stronger "you did it" cue than a flat toggle.
    [SerializeField] private int successBlinkCount = 3;
    [SerializeField] private float successBlinkInterval = 0.12f;

    // One full loop's length — StartScreenController's carousel reads this
    // so it doesn't cut the animation off mid-cycle.
    public float CycleDuration =>
        holdNeutral + riseDuration + holdBetweenBeats + crossDuration + holdBetweenBeats + riseDuration + holdCrossed
        + successBlinkCount * 2f * successBlinkInterval + holdSuccess + holdAfterSuccess;

    // StartScreenController's carousel shows every trick instruction page
    // for exactly this many full loops before advancing.
    public const int RepeatCount = 3;
    public float TotalDisplayDuration => CycleDuration * RepeatCount;

    private Vector2 _airBugStart, _airBugEnd;
    private Vector2 _groundBugStart, _groundBugEnd;

    private void Awake()
    {
        if (airBug != null)
        {
            _airBugStart = airBug.anchoredPosition;
            _airBugEnd = new Vector2(-_airBugStart.x, _airBugStart.y);
            _airBugImage = airBug.GetComponent<RawImage>();
            if (_airBugImage != null)
                _airBugNormalTexture = _airBugImage.texture as Texture2D;
        }
        if (groundBug != null)
        {
            _groundBugStart = groundBug.anchoredPosition;
            _groundBugEnd = new Vector2(-_groundBugStart.x, _groundBugStart.y);
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
        if (airBug != null)
        {
            airBug.anchoredPosition = _airBugStart;
            if (_airBugImage != null && _airBugNormalTexture != null)
                _airBugImage.texture = _airBugNormalTexture;
        }
        if (groundBug != null)
            groundBug.anchoredPosition = _groundBugStart;
        if (airArrow != null)
            airArrow.gameObject.SetActive(false);
        if (groundArrow != null)
            groundArrow.gameObject.SetActive(false);
        if (successText != null)
            successText.SetActive(false);
    }

    private IEnumerator Animate()
    {
        while (true)
        {
            yield return new WaitForSeconds(holdNeutral);

            yield return RiseUp();
            yield return new WaitForSeconds(holdBetweenBeats);
            yield return CrossSideways();
            yield return new WaitForSeconds(holdBetweenBeats);
            yield return ComeDown();
            yield return new WaitForSeconds(holdCrossed);

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

    private void ShowArrow(RectTransform arrow, string glyph, Vector2 bugPos)
    {
        if (arrow == null)
            return;
        Text text = arrow.GetComponent<Text>();
        if (text != null)
            text.text = glyph;
        arrow.anchoredPosition = bugPos + arrowOffset;
        arrow.gameObject.SetActive(true);
    }

    // Beat 1: airBug alone rises straight up — groundBug doesn't move yet.
    // Only airArrow shows, pointing up.
    private IEnumerator RiseUp()
    {
        if (_airBugImage != null && airBugAirTexture != null)
            _airBugImage.texture = airBugAirTexture;

        Vector2 start = _airBugStart;
        Vector2 target = _airBugStart + new Vector2(0f, arcHeight);
        ShowArrow(airArrow, "↑", start);

        float t = 0f;
        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / riseDuration);
            if (airBug != null)
            {
                airBug.anchoredPosition = Vector2.Lerp(start, target, p);
                if (airArrow != null)
                    airArrow.anchoredPosition = airBug.anchoredPosition + arrowOffset;
            }
            yield return null;
        }
        if (airArrow != null)
            airArrow.gameObject.SetActive(false);
    }

    // Beat 2: airBug slides across at the top, groundBug slides across at
    // the bottom, opposite directions at the same time — the actual
    // crossing. Both arrows show, each pointing that bug's own direction.
    private IEnumerator CrossSideways()
    {
        Vector2 airStart = airBug != null ? airBug.anchoredPosition : Vector2.zero;
        Vector2 airTarget = new Vector2(_airBugEnd.x, airStart.y);
        Vector2 groundStart = _groundBugStart;
        Vector2 groundTarget = _groundBugEnd;

        string airGlyph = airTarget.x > airStart.x ? "→" : "←";
        string groundGlyph = groundTarget.x > groundStart.x ? "→" : "←";
        ShowArrow(airArrow, airGlyph, airStart);
        ShowArrow(groundArrow, groundGlyph, groundStart);

        float t = 0f;
        while (t < crossDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / crossDuration);
            if (airBug != null)
            {
                airBug.anchoredPosition = Vector2.Lerp(airStart, airTarget, p);
                if (airArrow != null)
                    airArrow.anchoredPosition = airBug.anchoredPosition + arrowOffset;
            }
            if (groundBug != null)
            {
                groundBug.anchoredPosition = Vector2.Lerp(groundStart, groundTarget, p);
                if (groundArrow != null)
                    groundArrow.anchoredPosition = groundBug.anchoredPosition + arrowOffset;
            }
            yield return null;
        }
        if (airArrow != null)
            airArrow.gameObject.SetActive(false);
        if (groundArrow != null)
            groundArrow.gameObject.SetActive(false);
    }

    // Beat 3: airBug alone comes back down at its new spot — groundBug
    // stays put, already done. Only airArrow shows, pointing down.
    private IEnumerator ComeDown()
    {
        Vector2 start = airBug != null ? airBug.anchoredPosition : Vector2.zero;
        Vector2 target = new Vector2(start.x, _airBugStart.y);
        ShowArrow(airArrow, "↓", start);

        float t = 0f;
        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / riseDuration);
            if (airBug != null)
            {
                airBug.anchoredPosition = Vector2.Lerp(start, target, p);
                if (airArrow != null)
                    airArrow.anchoredPosition = airBug.anchoredPosition + arrowOffset;
            }
            yield return null;
        }
        if (_airBugImage != null && _airBugNormalTexture != null)
            _airBugImage.texture = _airBugNormalTexture;
        if (airArrow != null)
            airArrow.gameObject.SetActive(false);
    }
}
