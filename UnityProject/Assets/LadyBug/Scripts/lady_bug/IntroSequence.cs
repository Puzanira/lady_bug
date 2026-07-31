using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Shown after the player holds down a control on LoaderScreenController's
// attract-mode screen: flowers rain down and pile up (bottom row first, see
// SceneSetup.CreateIntroScreen for the fill order) until the whole screen
// is covered — held for a beat, then a 5-4-3-2-1-СТАРТ countdown appears
// right on top of the finished flower pile — then this canvas fades itself
// out and hands off to the start menu, which has been sitting ready
// underneath the whole time. Doesn't start on its own (see BeginConfirmHold)
// — LoaderScreenController triggers it once a control is held, and can
// abort it partway through (AbortIfIncomplete) if released too early.
public class IntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    // True from BeginConfirmHold() until either a natural finish (Finish())
    // or an early abort (AbortIfIncomplete()) — lets LoaderScreenController
    // tell those two "release happened after we already committed" and
    // "release happened mid-sequence" cases apart.
    public bool IsRunning { get; private set; }
    // True only while the grid is still filling with falling objects — goes
    // false the instant the 5-4-3-2-1 countdown starts (see RunCountdown).
    // LoaderScreenController only needs the control held for as long as
    // this is true; once the countdown itself is showing, releasing early
    // no longer aborts anything — per feedback, that's already "committed".
    public bool CanStillAbort { get; private set; }
    // Fades the whole canvas out at the very end (see FadeOutAndFinish) —
    // a soft finish instead of canvasRoot just vanishing on the spot.
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float finishFadeOutDuration = 0.6f;
    // One per grid cell, already in fill order (bottom row to top row,
    // shuffled within each row) — built at scene-setup time.
    [SerializeField] private RectTransform[] flowers;
    [SerializeField] private float totalDuration = 4.5f;
    [SerializeField] private float fallDistance = 400f;
    [SerializeField] private float fallDuration = 0.4f;
    // Continuous buzz while the flowers fall, growing louder as the grid
    // fills in — same clip a player's own wing-flap loop uses (see
    // PlayerMovementSfx), reused here rather than a separate asset.
    [SerializeField] private AudioSource buzzSource;
    [SerializeField] private float buzzMaxVolume = 0.6f;

    // Once the grid is fully covered: the buzz fades to silence over this
    // beat (no more darken-then-reveal-a-wall step — the countdown below
    // now appears directly over the finished flower pile instead, per
    // feedback that the wall background should go), then a short further
    // pause (digitDelay) before the countdown itself starts.
    [SerializeField] private float darkenDuration = 1.2f;
    [SerializeField] private float digitDelay = 0.4f;

    // Big countdown — 5-4-3-2-1, then "СТАРТ", held a moment (with a
    // pulse/shake, see PulseStart), before revealing the start menu. Real
    // generated graffiti artwork (yandex_api/gen_asset.sh), one transparent
    // texture per step (drawn right over the flowers, no wall behind it
    // anymore), swapped on this one RawImage — countdownTextures[0..4] are
    // 5/4/3/2/1, [5] is "СТАРТ".
    [SerializeField] private RawImage countdownImage;
    [SerializeField] private Texture2D[] countdownTextures;
    [SerializeField] private float countdownStepDuration = 0.8f;

    // "СТАРТ" pulse — exactly pulseCount beats over startHoldDuration
    // (0.2s/beat at the defaults) rather than a fixed angular speed, so the
    // count stays exactly right regardless of how long the hold is tuned to.
    [SerializeField] private float startHoldDuration = 2.2f;
    [SerializeField] private int startPulseCount = 11; // however many 0.2s beats fit in startHoldDuration
    [SerializeField] private float startPulseAmount = 0.08f;
    [SerializeField] private float startShakeAmount = 8f;

    // Dead-still beat between the continuous wave above and the short
    // accents below — lets the continuous pulsing actually read as
    // "finished" before the next phase starts, instead of blurring
    // straight into it.
    [SerializeField] private float startPulseRestPause = 2.2f;

    // Third beat: a few short, sharp individual pulses with real stillness
    // between them (not another continuous wave) — a distinct "last call"
    // punctuation before handing off to the menu, not just more of the same.
    // Total time is 0.4s longer than the plain 8*0.4s baseline now (two
    // rounds of +0.2s), all of it added as extra pause per beat (0.05s
    // each) — same shortPulseCount, same shortPulseDuration, just more
    // stillness between beats.
    [SerializeField] private int shortPulseCount = 8;
    [SerializeField] private float shortPulseDuration = 0.1f;
    [SerializeField] private float shortPulsePause = 0.35f;
    [SerializeField] private float shortPulseAmount = 0.08f;

    // Fourth beat: a final still hold once the short accents finish, before
    // the countdown image hides and hands off to the menu underneath —
    // lets the last accent actually land instead of cutting the instant it ends.
    [SerializeField] private float finalHoldPause = 0.5f;

    // Gear-shift clip, once per digit (not for "СТАРТ" itself — that's not
    // a digit) — same sound SpeedController's own gear changes use.
    [SerializeField] private AudioSource shiftSource;

    // Wired for all 7 of the loader's game slots (not just БК) — Finish()
    // always calls startScreen.OnRevealed() so the menu's own carousel
    // resets to page 0 right as it becomes visible, regardless of which
    // slot's intro just finished (see OnRevealed's own comment). Only
    // isPrimaryGame's instance also calls PlayMusic(), in RunCountdown, the
    // instant "СТАРТ" appears — StartScreenController itself no longer
    // starts its music in Awake, since this whole screen sits on top of the
    // menu for the first several seconds and that music would otherwise
    // already be playing underneath a screen meant to be silent.
    [SerializeField] private StartScreenController startScreen;
    // True only for БК's own instance (index 0 in SceneSetup's
    // GameIntroThemes) — gates the PlayMusic() call specifically; games 2-7
    // still wire startScreen (for OnRevealed) but don't start the menu
    // music, per feedback that music is БК-specific content.
    [SerializeField] private bool isPrimaryGame;

    // Snapshot of each flower's resting anchoredPosition, taken once in
    // Awake (before anything has moved) — DropFlower always falls toward
    // this rather than re-reading the flower's current position, since
    // after an aborted run a flower can be left part-way through its fall,
    // and re-reading its position then would use that mid-air point as the
    // new "resting" target instead of the real one.
    private Vector2[] _flowerTargets;

    private void Awake()
    {
        if (countdownImage != null)
            countdownImage.gameObject.SetActive(false);

        if (flowers == null)
            return;

        _flowerTargets = new Vector2[flowers.Length];
        for (int i = 0; i < flowers.Length; i++)
        {
            if (flowers[i] == null)
                continue;
            _flowerTargets[i] = flowers[i].anchoredPosition;
            flowers[i].gameObject.SetActive(false);
        }
    }

    // Called by LoaderScreenController when a control is first pressed —
    // reveals this canvas and starts the flower/countdown sequence.
    public void BeginConfirmHold()
    {
        IsRunning = true;
        CanStillAbort = true;
        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        StartCoroutine(RunIntro());
    }

    // Called by LoaderScreenController when the held control is released
    // before the sequence reached "СТАРТ" — snaps everything back to its
    // pre-run state and hides this canvas again instead of letting the
    // coroutine keep playing out. A no-op if the sequence already finished
    // naturally (IsRunning is false by then), so a release right at the end
    // can't undo an already-committed start.
    public void AbortIfIncomplete()
    {
        if (!IsRunning)
            return;

        StopAllCoroutines();
        IsRunning = false;
        CanStillAbort = false;

        if (buzzSource != null)
        {
            buzzSource.Stop();
            buzzSource.volume = 0f;
        }
        if (countdownImage != null)
            countdownImage.gameObject.SetActive(false);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        if (flowers != null)
            foreach (var flower in flowers)
                if (flower != null)
                    flower.gameObject.SetActive(false);

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private IEnumerator RunIntro()
    {
        if (flowers == null || flowers.Length == 0)
        {
            Finish();
            yield break;
        }

        if (buzzSource != null)
        {
            buzzSource.volume = 0f;
            buzzSource.Play();
        }

        float perFlowerDelay = totalDuration / flowers.Length;
        for (int i = 0; i < flowers.Length; i++)
        {
            var flower = flowers[i];
            if (flower != null)
                StartCoroutine(DropFlower(flower, _flowerTargets[i]));
            if (buzzSource != null)
                buzzSource.volume = Mathf.Lerp(0f, buzzMaxVolume, (float)(i + 1) / flowers.Length);
            yield return new WaitForSeconds(perFlowerDelay);
        }

        // Let the last handful still mid-fall actually land before revealing
        // the menu underneath, instead of cutting them off mid-air.
        yield return new WaitForSeconds(fallDuration);

        yield return StartCoroutine(FadeOutBuzz());

        yield return new WaitForSeconds(digitDelay);
        yield return StartCoroutine(RunCountdown());
        yield return StartCoroutine(FadeOutAndFinish());
    }

    // Just the buzz winding down to silence over darkenDuration — used to
    // also darken the screen to black and reveal a graffiti wall behind the
    // countdown here; removed per feedback (the countdown sits directly
    // over the finished flower pile now, no separate background swap).
    private IEnumerator FadeOutBuzz()
    {
        float startBuzzVolume = buzzSource != null ? buzzSource.volume : 0f;
        float t = 0f;
        while (t < darkenDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / darkenDuration);
            if (buzzSource != null)
                buzzSource.volume = Mathf.Lerp(startBuzzVolume, 0f, p);
            yield return null;
        }
        if (buzzSource != null)
            buzzSource.Stop();
    }

    // Replaces the old instant Finish() call — the whole screen (flowers +
    // countdown, whatever's still showing) eases out to transparent instead
    // of just vanishing on the spot, then hides for good.
    private IEnumerator FadeOutAndFinish()
    {
        // Resets the carousel to page 0 before the fade starts, not just in
        // Finish() at the end — this canvas sits ABOVE the start menu and
        // fades to transparent over finishFadeOutDuration, so the menu (and
        // whatever carousel page it silently landed on while hidden) is
        // visible, blending in, for that whole fade — not just for one
        // stray frame at the very end. Finish()'s own call to this is now
        // mostly a no-op for this path, but still needed for the
        // no-flowers fallback in RunIntro(), which skips straight to
        // Finish() without ever fading.
        if (startScreen != null)
            startScreen.OnRevealed();

        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < finishFadeOutDuration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / finishFadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }
        Finish();
    }

    private IEnumerator RunCountdown()
    {
        // From here on, holding the control is no longer required — see
        // CanStillAbort's own comment.
        CanStillAbort = false;

        if (countdownImage == null || countdownTextures == null || countdownTextures.Length < 6)
            yield break;

        countdownImage.gameObject.SetActive(true);
        for (int n = 5; n >= 1; n--)
        {
            countdownImage.texture = countdownTextures[5 - n]; // [0]=5, [1]=4, ... [4]=1
            if (shiftSource != null)
                shiftSource.Play();
            yield return new WaitForSeconds(countdownStepDuration);
        }

        countdownImage.texture = countdownTextures[5]; // "СТАРТ"
        if (startScreen != null && isPrimaryGame)
            startScreen.PlayMusic();
        yield return StartCoroutine(PulseStart());
        yield return new WaitForSeconds(startPulseRestPause);
        yield return StartCoroutine(PulseStartShort());
        yield return new WaitForSeconds(finalHoldPause);
        countdownImage.gameObject.SetActive(false);
    }

    // Scale pulse + small jittery shake for the "СТАРТ" hold, exactly
    // startPulseCount beats over startHoldDuration — a plain static line
    // read as flat compared to the rest of this sequence.
    private IEnumerator PulseStart()
    {
        RectTransform rt = countdownImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        Vector2 basePos = rt.anchoredPosition;

        float t = 0f;
        while (t < startHoldDuration)
        {
            t += Time.deltaTime;
            float phase = (t / startHoldDuration) * startPulseCount * Mathf.PI * 2f;
            float pulse = Mathf.Sin(phase) * startPulseAmount;
            rt.localScale = baseScale * (1f + pulse);
            float shakeX = (Mathf.PerlinNoise(t * 25f, 0f) - 0.5f) * 2f * startShakeAmount;
            float shakeY = (Mathf.PerlinNoise(0f, t * 25f) - 0.5f) * 2f * startShakeAmount;
            rt.anchoredPosition = basePos + new Vector2(shakeX, shakeY);
            yield return null;
        }

        rt.localScale = baseScale;
        rt.anchoredPosition = basePos;
    }

    // shortPulseCount individual quick pulses, each with real stillness
    // (shortPulsePause) between them — punctuation after PulseStart's
    // continuous wave, not more of it.
    private IEnumerator PulseStartShort()
    {
        RectTransform rt = countdownImage.rectTransform;
        Vector3 baseScale = rt.localScale;

        for (int i = 0; i < shortPulseCount; i++)
        {
            float t = 0f;
            while (t < shortPulseDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / shortPulseDuration);
                float pulse = Mathf.Sin(p * Mathf.PI) * shortPulseAmount; // one bump: 0 -> peak -> 0
                rt.localScale = baseScale * (1f + pulse);
                yield return null;
            }
            rt.localScale = baseScale;
            yield return new WaitForSeconds(shortPulsePause);
        }
    }

    private IEnumerator DropFlower(RectTransform flower, Vector2 target)
    {
        Vector2 start = target + new Vector2(0f, fallDistance);
        flower.anchoredPosition = start;
        flower.gameObject.SetActive(true);

        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            flower.anchoredPosition = Vector2.Lerp(start, target, t / fallDuration);
            yield return null;
        }
        flower.anchoredPosition = target;
    }

    private void Finish()
    {
        IsRunning = false;
        CanStillAbort = false; // usually already false by the time RunCountdown gets here, but RunIntro's empty-flowers fallback skips straight to this
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        if (startScreen != null)
            startScreen.OnRevealed();
    }
}
