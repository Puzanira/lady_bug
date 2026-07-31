using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animated demo for one gesture-instruction page (CreateGestureDiagramPage):
// sensors on the bottom half, both hands starting centered in their "beam",
// then either moving to the gesture's target position and holding (duck/
// lean) or oscillating continuously (flap/jump) — with a small arrow beside
// each hand that only appears while it's moving, pointing the direction
// that specific hand is going. A ladybug on the top half performs the
// resulting action in sync, using the same real in-game poses the actual
// move does (squash for duck, wings-out sprite for flap — see
// ArchTrickAnimation's own comment on why there's no separate "crouch" art).
public class GestureDiagramAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform leftPalm;
    [SerializeField] private RectTransform rightPalm;
    [SerializeField] private RectTransform leftArrow;
    [SerializeField] private RectTransform rightArrow;

    // Target Y offset for each hand once "reacted" — unused (continuous
    // oscillation instead) when isFlap is set. Also what IsDuckGesture/
    // IsLeanLeftGesture/IsLeanRightGesture below read to decide how the bug
    // on the right should react, so this page doesn't need a second,
    // separately-set "what gesture is this" field that could drift out of
    // sync with it.
    [SerializeField] private float leftTargetOffset = -50f;
    [SerializeField] private float rightTargetOffset = -50f;
    [SerializeField] private bool isFlap;

    [SerializeField] private float holdNeutral = 1f;
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private float holdReacted = 1f;
    [SerializeField] private float flapCycleDuration = 0.5f;
    [SerializeField] private int flapCycles = 4;
    [SerializeField] private float flapAmplitude = 40f;

    // The reacting ladybug, right half of the page. Duck squashes it
    // vertically (real in-game duck, no separate crouch sprite exists);
    // lean hops it sideways; flap rises and alternates the two wings-out
    // frames the real jump animation uses (PlayerAnimator.airFrames).
    [SerializeField] private RawImage bugImage;
    [SerializeField] private Texture2D bugNormalTexture;
    [SerializeField] private Texture2D bugAirTexture1;
    [SerializeField] private Texture2D bugAirTexture2;
    [SerializeField] private float bugDuckSquash = 0.6f;
    // Almost to the page's own edge, not a small nudge — matches how far a
    // real lane change actually reads on screen (see CreateGestureDiagramPage).
    [SerializeField] private float bugLeanShift = 220f;
    // Real in-game lean angle (PlayerController.laneTiltAngle) — leaned INTO
    // the move while travelling, eased back to upright once settled in the
    // new spot, same as a real lane change does.
    [SerializeField] private float bugLeanTiltAngle = 12f;
    [SerializeField] private float bugFlyRise = 50f;
    [SerializeField] private float bugFlapFrameDuration = 0.15f;

    // StartScreenController's carousel shows each gesture page for exactly
    // this many full loops before advancing, instead of a fixed interval
    // that could cut a loop off partway through.
    public const int RepeatCount = 3;

    // One full loop's length.
    public float CycleDuration => isFlap
        ? holdNeutral + flapCycles * flapCycleDuration + holdReacted
        : holdNeutral + moveDuration + holdReacted;

    public float TotalDisplayDuration => CycleDuration * RepeatCount;

    private bool IsDuckGesture => !isFlap && leftTargetOffset < 0f && rightTargetOffset < 0f;
    private bool IsLeanRightGesture => !isFlap && leftTargetOffset > 0f && rightTargetOffset < 0f;
    private bool IsLeanLeftGesture => !isFlap && leftTargetOffset < 0f && rightTargetOffset > 0f;

    private Vector2 _leftPalmRest, _rightPalmRest;
    private Vector2 _leftArrowRest, _rightArrowRest;
    private Vector2 _bugRestPos;
    private Vector3 _bugRestScale = Vector3.one;

    private void Awake()
    {
        if (leftPalm != null)
            _leftPalmRest = leftPalm.anchoredPosition;
        if (rightPalm != null)
            _rightPalmRest = rightPalm.anchoredPosition;
        if (leftArrow != null)
            _leftArrowRest = leftArrow.anchoredPosition;
        if (rightArrow != null)
            _rightArrowRest = rightArrow.anchoredPosition;
        if (bugImage != null)
        {
            _bugRestPos = bugImage.rectTransform.anchoredPosition;
            _bugRestScale = bugImage.rectTransform.localScale;
        }
    }

    private void OnEnable()
    {
        ResetVisuals();
        StartCoroutine(isFlap ? AnimateFlap() : AnimateMove());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetVisuals();
    }

    private void ResetVisuals()
    {
        if (leftPalm != null)
            leftPalm.anchoredPosition = _leftPalmRest;
        if (rightPalm != null)
            rightPalm.anchoredPosition = _rightPalmRest;
        if (leftArrow != null)
        {
            leftArrow.anchoredPosition = _leftArrowRest;
            leftArrow.gameObject.SetActive(false);
        }
        if (rightArrow != null)
        {
            rightArrow.anchoredPosition = _rightArrowRest;
            rightArrow.gameObject.SetActive(false);
        }
        if (bugImage != null)
        {
            bugImage.rectTransform.anchoredPosition = _bugRestPos;
            bugImage.rectTransform.localScale = _bugRestScale;
            bugImage.rectTransform.localRotation = Quaternion.identity;
            if (bugNormalTexture != null)
                bugImage.texture = bugNormalTexture;
        }
    }

    // Duck/lean gestures: both hands hold neutral, then move together to
    // their target offset and hold there before resetting — a single clean
    // "this is the shape of the gesture" beat, not two hands animating
    // independently of each other. The bug on the right reacts in step.
    private IEnumerator AnimateMove()
    {
        while (true)
        {
            yield return new WaitForSeconds(holdNeutral);

            if (leftArrow != null)
                leftArrow.gameObject.SetActive(true);
            if (rightArrow != null)
                rightArrow.gameObject.SetActive(true);

            Vector2 leftTarget = _leftPalmRest + new Vector2(0f, leftTargetOffset);
            Vector2 rightTarget = _rightPalmRest + new Vector2(0f, rightTargetOffset);

            Vector3 bugTargetScale = _bugRestScale;
            Vector2 bugTargetPos = _bugRestPos;
            float bugTargetTilt = 0f;
            if (IsDuckGesture)
            {
                bugTargetScale = new Vector3(_bugRestScale.x, _bugRestScale.y * bugDuckSquash, _bugRestScale.z);
                // Shrink from the bottom, not the center — the feet stay at
                // road level and only the top comes down, same as the real
                // duck (PlayerController keeps its base anchored to the
                // ground and only shrinks upward from there).
                if (bugImage != null)
                {
                    float fullHeight = bugImage.rectTransform.sizeDelta.y * _bugRestScale.y;
                    float squashedHeight = bugImage.rectTransform.sizeDelta.y * bugTargetScale.y;
                    bugTargetPos = _bugRestPos + new Vector2(0f, -(fullHeight - squashedHeight) / 2f);
                }
            }
            else if (IsLeanRightGesture)
            {
                bugTargetPos = _bugRestPos + new Vector2(bugLeanShift, 0f);
                bugTargetTilt = -bugLeanTiltAngle;
            }
            else if (IsLeanLeftGesture)
            {
                bugTargetPos = _bugRestPos + new Vector2(-bugLeanShift, 0f);
                bugTargetTilt = bugLeanTiltAngle;
            }

            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / moveDuration);
                if (leftPalm != null)
                    leftPalm.anchoredPosition = Vector2.Lerp(_leftPalmRest, leftTarget, p);
                if (rightPalm != null)
                    rightPalm.anchoredPosition = Vector2.Lerp(_rightPalmRest, rightTarget, p);
                if (bugImage != null)
                {
                    bugImage.rectTransform.localScale = Vector3.Lerp(_bugRestScale, bugTargetScale, p);
                    bugImage.rectTransform.anchoredPosition = Vector2.Lerp(_bugRestPos, bugTargetPos, p);
                    // Leans INTO the turn while travelling — same transient
                    // tilt a real lane change has (PlayerController's own
                    // laneTiltAngle), not held once settled in the new spot.
                    float tiltPhase = Mathf.Sin(p * Mathf.PI); // 0 -> peak mid-move -> 0 by the time it arrives
                    bugImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, bugTargetTilt * tiltPhase);
                }
                yield return null;
            }

            yield return new WaitForSeconds(holdReacted);

            if (leftArrow != null)
                leftArrow.gameObject.SetActive(false);
            if (rightArrow != null)
                rightArrow.gameObject.SetActive(false);
            if (leftPalm != null)
                leftPalm.anchoredPosition = _leftPalmRest;
            if (rightPalm != null)
                rightPalm.anchoredPosition = _rightPalmRest;
            if (bugImage != null)
            {
                bugImage.rectTransform.localScale = _bugRestScale;
                bugImage.rectTransform.anchoredPosition = _bugRestPos;
                bugImage.rectTransform.localRotation = Quaternion.identity;
            }
        }
    }

    // Continuous rhythmic up/down oscillation, both hands together and in
    // phase — a real flap. The arrows bounce along with the palms instead
    // of pointing a single fixed direction, since a static "up" glyph alone
    // doesn't convey the repeated, rhythmic motion the actual gesture needs
    // — the motion itself carries that here. The bug rises and alternates
    // its two wings-out frames for the same reason.
    private IEnumerator AnimateFlap()
    {
        while (true)
        {
            yield return new WaitForSeconds(holdNeutral);

            if (leftArrow != null)
                leftArrow.gameObject.SetActive(true);
            if (rightArrow != null)
                rightArrow.gameObject.SetActive(true);

            float flapFrameTimer = 0f;
            bool flapFrameToggle = false;
            if (bugImage != null && bugAirTexture1 != null)
                bugImage.texture = bugAirTexture1;

            for (int cycle = 0; cycle < flapCycles; cycle++)
            {
                float t = 0f;
                while (t < flapCycleDuration)
                {
                    t += Time.deltaTime;
                    float phase = t / flapCycleDuration; // 0..1 over one full up-down cycle
                    float offset = Mathf.Sin(phase * Mathf.PI * 2f) * flapAmplitude;
                    if (leftPalm != null)
                        leftPalm.anchoredPosition = _leftPalmRest + new Vector2(0f, offset);
                    if (rightPalm != null)
                        rightPalm.anchoredPosition = _rightPalmRest + new Vector2(0f, offset);
                    if (leftArrow != null)
                        leftArrow.anchoredPosition = _leftArrowRest + new Vector2(0f, offset * 0.5f);
                    if (rightArrow != null)
                        rightArrow.anchoredPosition = _rightArrowRest + new Vector2(0f, offset * 0.5f);

                    if (bugImage != null)
                    {
                        bugImage.rectTransform.anchoredPosition = _bugRestPos + new Vector2(0f, bugFlyRise);
                        flapFrameTimer += Time.deltaTime;
                        if (flapFrameTimer >= bugFlapFrameDuration)
                        {
                            flapFrameTimer = 0f;
                            flapFrameToggle = !flapFrameToggle;
                            Texture2D frame = flapFrameToggle ? bugAirTexture2 : bugAirTexture1;
                            if (frame != null)
                                bugImage.texture = frame;
                        }
                    }

                    yield return null;
                }
            }

            if (leftArrow != null)
            {
                leftArrow.gameObject.SetActive(false);
                leftArrow.anchoredPosition = _leftArrowRest;
            }
            if (rightArrow != null)
            {
                rightArrow.gameObject.SetActive(false);
                rightArrow.anchoredPosition = _rightArrowRest;
            }
            if (leftPalm != null)
                leftPalm.anchoredPosition = _leftPalmRest;
            if (rightPalm != null)
                rightPalm.anchoredPosition = _rightPalmRest;
            if (bugImage != null)
            {
                bugImage.rectTransform.anchoredPosition = _bugRestPos;
                if (bugNormalTexture != null)
                    bugImage.texture = bugNormalTexture;
            }

            yield return new WaitForSeconds(holdReacted);
        }
    }
}
