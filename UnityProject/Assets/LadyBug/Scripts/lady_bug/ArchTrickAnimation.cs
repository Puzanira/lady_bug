using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animated demo for the "ТРЮК: АРКА" instruction page — replaces a static
// diagram + caption with a looping mini-sequence: both ladybugs hold their
// spot, direction arrows appear telling each which way to move, they react
// (duck/jump), an arch approaches from the distance (small, growing) and
// passes between them, then a "trick complete" line flashes before it
// loops back to the start.
public class ArchTrickAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform bottomBug; // ducks — down arrows either side
    [SerializeField] private RectTransform topBug; // jumps — up arrows either side
    [SerializeField] private GameObject downArrows;
    [SerializeField] private GameObject upArrows;
    [SerializeField] private RectTransform arch;
    [SerializeField] private GameObject successText;

    [SerializeField] private float holdNeutral = 1.3f;
    [SerializeField] private float holdArrows = 1.2f;
    [SerializeField] private float reactDuration = 0.35f;
    [SerializeField] private float holdBetweenReacts = 0.7f;
    [SerializeField] private float holdReacted = 0.8f;
    [SerializeField] private float archDuration = 2.3f;
    [SerializeField] private float holdSuccess = 1.6f;
    [SerializeField] private float holdAfterSuccess = 0.8f;
    // "ТРЮК ВЫПОЛНЕН +1" blinks a few times before settling into its
    // holdSuccess steady-on window, instead of just snapping on — reads as
    // a much stronger "you did it" cue than a flat toggle.
    [SerializeField] private int successBlinkCount = 3;
    [SerializeField] private float successBlinkInterval = 0.12f;
    [SerializeField] private float duckOffset = 40f;
    [SerializeField] private float jumpOffset = 40f;
    [SerializeField] private float archMinHeight = 30f;
    // The source art's crossbar isn't at the exact vertical center of its
    // own bounding box (an arch shape has more open leg below it than
    // structure above) — growing the box from a fixed center anchor drags
    // the crossbar higher and higher as it scales up. Shifting the whole
    // image down by this fraction of its OWN current height keeps the
    // crossbar sitting at y=0 (exactly between the two bugs) at any size,
    // not just the original small one.
    [SerializeField] private float archCrossbarFraction = 0.28f;
    // Bugs' reacted (ducked/jumped) edges sit at ±95 (bug centers at
    // ±170 minus/plus their own half-height of 75 — see CreateArchTrickPage's
    // bugHeight/bugY and this class's duckOffset/jumpOffset). The arch stays
    // under that ±95 band (well under archMidHeight, see AnimateArch) for
    // the first half of its approach, so it still reads as "passing cleanly
    // between them" before it keeps growing — up near the edge of the
    // frame by the end, like it's arrived close to the camera, instead of
    // cutting off at a modest, still-clearly-distant size. Pulled back
    // from an even bigger peak (and CreateArchTrickPage's page now clips
    // to its own bounds with a RectMask2D besides) — it read as "too
    // close"/spilling past the frame at the very largest size.
    [SerializeField] private float archMidHeight = 170f;
    [SerializeField] private float archMidWidth = 640f;
    [SerializeField] private float archMaxHeight = 760f;
    [SerializeField] private float archMaxWidth = 1350f;

    // Real in-game poses, not separate diagram-only art: ducking is a Y
    // scale squash on the same running sprite (see PlayerController's own
    // duckHeightDelta handling, no distinct "crouch" texture exists), and
    // flying swaps to the wings-out sprite the actual jump animation uses
    // (PlayerAnimator's airFrames) — so this demo matches what the move
    // really looks like in play instead of just sliding the same static
    // pose up/down.
    [SerializeField] private float bottomBugDuckSquash = 0.6f;
    [SerializeField] private Texture2D topBugAirTexture;

    // One full loop's length — StartScreenController's carousel reads this
    // so it doesn't cut the animation off mid-cycle (was a fixed 4s
    // interval regardless of what was actually playing).
    public float CycleDuration =>
        holdNeutral + holdArrows + reactDuration + holdBetweenReacts + holdArrows + reactDuration + holdReacted + archDuration
        + successBlinkCount * 2f * successBlinkInterval + holdSuccess + holdAfterSuccess;

    // StartScreenController's carousel shows every trick instruction page
    // for exactly this many full loops before advancing — same idea as
    // GestureDiagramAnimation.RepeatCount, now applied to all of them.
    public const int RepeatCount = 3;
    public float TotalDisplayDuration => CycleDuration * RepeatCount;

    private Vector2 _bottomBugRest;
    private Vector2 _topBugRest;
    private Vector3 _bottomBugRestScale = Vector3.one;
    private float _archAspect = 1f;
    private RawImage _bottomBugImage;
    private RawImage _topBugImage;
    private Texture2D _topBugNormalTexture;

    private void Awake()
    {
        if (bottomBug != null)
        {
            _bottomBugRest = bottomBug.anchoredPosition;
            _bottomBugRestScale = bottomBug.localScale;
            _bottomBugImage = bottomBug.GetComponent<RawImage>();
        }
        if (topBug != null)
        {
            _topBugRest = topBug.anchoredPosition;
            _topBugImage = topBug.GetComponent<RawImage>();
            if (_topBugImage != null)
                _topBugNormalTexture = _topBugImage.texture as Texture2D;
        }
        if (arch != null)
        {
            RawImage img = arch.GetComponent<RawImage>();
            if (img != null && img.texture != null)
                _archAspect = (float)img.texture.width / img.texture.height;
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
        if (bottomBug != null)
        {
            bottomBug.anchoredPosition = _bottomBugRest;
            bottomBug.localScale = _bottomBugRestScale;
        }
        if (topBug != null)
        {
            topBug.anchoredPosition = _topBugRest;
            if (_topBugImage != null && _topBugNormalTexture != null)
                _topBugImage.texture = _topBugNormalTexture;
        }
        if (downArrows != null)
            downArrows.SetActive(false);
        if (upArrows != null)
            upArrows.SetActive(false);
        if (arch != null)
            arch.gameObject.SetActive(false);
        if (successText != null)
            successText.SetActive(false);
    }

    private IEnumerator Animate()
    {
        while (true)
        {
            yield return new WaitForSeconds(holdNeutral);

            // Each direction is its own step — bottom bug's cue and reaction
            // play out fully before the top bug's starts, not both at once,
            // so a slide-by-slide reading of the instruction actually shows
            // one thing happening at a time.
            if (downArrows != null)
                downArrows.SetActive(true);
            yield return new WaitForSeconds(holdArrows);
            yield return ReactBottom();
            yield return new WaitForSeconds(holdBetweenReacts);

            if (upArrows != null)
                upArrows.SetActive(true);
            yield return new WaitForSeconds(holdArrows);
            yield return ReactTop();
            yield return new WaitForSeconds(holdReacted);

            yield return AnimateArch();

            if (downArrows != null)
                downArrows.SetActive(false);
            if (upArrows != null)
                upArrows.SetActive(false);
            if (bottomBug != null)
            {
                bottomBug.anchoredPosition = _bottomBugRest;
                bottomBug.localScale = _bottomBugRestScale;
            }
            if (topBug != null)
            {
                topBug.anchoredPosition = _topBugRest;
                if (_topBugImage != null && _topBugNormalTexture != null)
                    _topBugImage.texture = _topBugNormalTexture;
            }

            if (successText != null)
                yield return BlinkSuccessText();

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

    // Bottom bug ducks (settles lower, squashes vertically) — the down
    // arrows' cue. Same squash-not-swap the real duck uses, see the class
    // comment on bottomBugDuckSquash.
    private IEnumerator ReactBottom()
    {
        if (bottomBug == null)
            yield break;

        Vector2 target = _bottomBugRest + new Vector2(0f, -duckOffset);
        Vector3 targetScale = new Vector3(_bottomBugRestScale.x, _bottomBugRestScale.y * bottomBugDuckSquash, _bottomBugRestScale.z);
        float t = 0f;
        while (t < reactDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / reactDuration);
            bottomBug.anchoredPosition = Vector2.Lerp(_bottomBugRest, target, p);
            bottomBug.localScale = Vector3.Lerp(_bottomBugRestScale, targetScale, p);
            yield return null;
        }
    }

    // Top bug jumps (rises), wings-out sprite — the up arrows' cue. Texture
    // swaps the instant the rise starts rather than easing in, same as the
    // real airborne/ground swap (PlayerAnimator) is an instant cut, not a
    // cross-fade.
    private IEnumerator ReactTop()
    {
        if (topBug == null)
            yield break;

        if (_topBugImage != null && topBugAirTexture != null)
            _topBugImage.texture = topBugAirTexture;

        Vector2 target = _topBugRest + new Vector2(0f, jumpOffset);
        float t = 0f;
        while (t < reactDuration)
        {
            t += Time.deltaTime;
            topBug.anchoredPosition = Vector2.Lerp(_topBugRest, target, Mathf.Clamp01(t / reactDuration));
            yield return null;
        }
    }

    // Small and distant, growing as it "approaches" and passes right
    // between the two bugs, then gone.
    private IEnumerator AnimateArch()
    {
        if (arch == null)
            yield break;

        arch.gameObject.SetActive(true);
        arch.anchoredPosition = Vector2.zero;

        float minWidth = archMinHeight * _archAspect;

        // Two legs: small-and-distant up to archMid* (still passing cleanly
        // between the two bugs), then archMid* on up to archMax* (arriving
        // right at the camera) — one continuous growth, not a hard cut once
        // it "passes", so waiting for it to actually reach the viewer reads
        // as one unbroken approach instead of two different animations.
        const float midFraction = 0.55f;
        float t = 0f;
        while (t < archDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / archDuration);
            float height, width;
            if (p < midFraction)
            {
                float ease = (p / midFraction) * (p / midFraction); // eases in — slow approach
                height = Mathf.Lerp(archMinHeight, archMidHeight, ease);
                width = Mathf.Lerp(minWidth, archMidWidth, ease);
            }
            else
            {
                float local = (p - midFraction) / (1f - midFraction);
                float ease = local * local; // eases in again — accelerates the rest of the way in
                height = Mathf.Lerp(archMidHeight, archMaxHeight, ease);
                width = Mathf.Lerp(archMidWidth, archMaxWidth, ease);
            }
            arch.sizeDelta = new Vector2(width, height);
            arch.anchoredPosition = new Vector2(0f, -height * archCrossbarFraction);
            yield return null;
        }

        arch.gameObject.SetActive(false);
    }
}
