using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Very first thing shown when the game launches: flowers rain down and pile
// up (bottom row first, see SceneSetup.CreateIntroScreen for the fill
// order) until the whole screen is covered — held for a beat, then swapped
// for a full-screen graffiti wall with a 5-4-3-2-1-СТАРТ countdown painted
// on it — then this canvas hides itself and hands off to the start menu,
// which has been sitting ready underneath the whole time.
public class IntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
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

    // Once the grid is fully covered: the buzz fades to silence while the
    // finished flower pile darkens at the same time, a brief hold at full
    // black, then the (still empty) wall reveals out of that darkness —
    // the countdown digits only start a beat after that, not together with
    // the wall itself. Replaces the old flat "hold, then instant cut to the
    // wall" beat.
    [SerializeField] private float darkenDuration = 1.2f;
    [SerializeField] private float darkOverlayMaxAlpha = 0.95f;
    [SerializeField] private float holdDark = 0.3f;
    [SerializeField] private float wallRevealDuration = 1f;
    [SerializeField] private float digitDelay = 0.4f;
    [SerializeField] private Image darkOverlay;

    // Full-screen brick wall (Assets/Sprites/GraffitiWall.png) — opaque, so
    // showing it automatically covers the flowers/backdrop underneath
    // (later sibling, see CreateIntroScreen) without needing to hide those
    // separately. Never moves, only the digit/word layer on top of it does.
    [SerializeField] private GameObject wall;

    // Big countdown, painted on the wall once it's revealed — 5-4-3-2-1,
    // then "СТАРТ", held a moment (with a pulse/shake, see PulseStart),
    // before revealing the start menu. Real generated graffiti artwork
    // (yandex_api/gen_asset.sh), one transparent texture per step, swapped
    // on this one RawImage — countdownTextures[0..4] are 5/4/3/2/1, [5] is
    // "СТАРТ".
    [SerializeField] private RawImage countdownImage;
    [SerializeField] private Texture2D[] countdownTextures;
    [SerializeField] private float countdownStepDuration = 0.8f;

    // "СТАРТ" pulse — exactly pulseCount beats over startHoldDuration
    // (0.2s/beat at the defaults) rather than a fixed angular speed, so the
    // count stays exactly right regardless of how long the hold is tuned
    // to. Only this image pulses — the wall behind it is a separate object
    // that never moves.
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

    // Told to start its music the instant "СТАРТ" appears (see
    // RunCountdown) — StartScreenController itself no longer starts it in
    // Awake, since this whole screen sits on top of the menu for the first
    // several seconds and that music would otherwise already be playing
    // underneath a screen meant to be silent.
    [SerializeField] private StartScreenController startScreen;

    private void Awake()
    {
        if (wall != null)
            wall.SetActive(false);
        if (countdownImage != null)
            countdownImage.gameObject.SetActive(false);

        if (flowers == null)
            return;

        foreach (var flower in flowers)
            if (flower != null)
                flower.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(RunIntro());
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
                StartCoroutine(DropFlower(flower));
            if (buzzSource != null)
                buzzSource.volume = Mathf.Lerp(0f, buzzMaxVolume, (float)(i + 1) / flowers.Length);
            yield return new WaitForSeconds(perFlowerDelay);
        }

        // Let the last handful still mid-fall actually land before revealing
        // the menu underneath, instead of cutting them off mid-air.
        yield return new WaitForSeconds(fallDuration);

        yield return StartCoroutine(FadeToWall());

        yield return new WaitForSeconds(digitDelay);
        yield return StartCoroutine(RunCountdown());
        Finish();
    }

    // Buzz fades to silence while the finished flower pile darkens at the
    // same time, a brief hold at full black, then the (still empty) wall
    // reveals back out of that darkness — see darkOverlay's own field comment.
    private IEnumerator FadeToWall()
    {
        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            Color c = darkOverlay.color;
            c.a = 0f;
            darkOverlay.color = c;
        }

        float startBuzzVolume = buzzSource != null ? buzzSource.volume : 0f;
        float t = 0f;
        while (t < darkenDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / darkenDuration);
            if (buzzSource != null)
                buzzSource.volume = Mathf.Lerp(startBuzzVolume, 0f, p);
            if (darkOverlay != null)
            {
                Color c = darkOverlay.color;
                c.a = Mathf.Lerp(0f, darkOverlayMaxAlpha, p);
                darkOverlay.color = c;
            }
            yield return null;
        }
        if (buzzSource != null)
            buzzSource.Stop();

        yield return new WaitForSeconds(holdDark);

        if (wall != null)
            wall.SetActive(true);

        t = 0f;
        while (t < wallRevealDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / wallRevealDuration);
            if (darkOverlay != null)
            {
                Color c = darkOverlay.color;
                c.a = Mathf.Lerp(darkOverlayMaxAlpha, 0f, p);
                darkOverlay.color = c;
            }
            yield return null;
        }
        if (darkOverlay != null)
            darkOverlay.gameObject.SetActive(false);
    }

    private IEnumerator RunCountdown()
    {
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
        if (startScreen != null)
            startScreen.PlayMusic();
        yield return StartCoroutine(PulseStart());
        yield return new WaitForSeconds(startPulseRestPause);
        yield return StartCoroutine(PulseStartShort());
        yield return new WaitForSeconds(finalHoldPause);
        countdownImage.gameObject.SetActive(false);
    }

    // Scale pulse + small jittery shake for the "СТАРТ" hold, exactly
    // startPulseCount beats over startHoldDuration — a plain static line
    // read as flat compared to the rest of this sequence, and only the
    // word itself moves (the wall behind it is a separate, unmoving object).
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

    private IEnumerator DropFlower(RectTransform flower)
    {
        Vector2 target = flower.anchoredPosition;
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
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }
}
