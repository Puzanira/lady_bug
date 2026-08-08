using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

// Shown after the player holds down a control on LoaderScreenController's
// attract-mode screen: flowers rain down and pile up (bottom row first, see
// SceneSetup.CreateIntroScreen for the fill order) until the whole screen
// is covered — then the buzz fades to silence, the picture goes to black, and
// the start menu (which has been sitting ready underneath the whole time)
// takes over behind the black. Doesn't start on its own (see BeginConfirmHold)
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
    // false the moment the screen is covered and the handoff beats start.
    // LoaderScreenController only needs the control held for as long as this
    // is true; once the screen is full, releasing early no longer aborts
    // anything — per feedback, that's already "committed".
    public bool CanStillAbort { get; private set; }
    // Only used to restore full opacity on abort now — the ending itself
    // darkens via darkenOverlay rather than fading this canvas out.
    [SerializeField] private CanvasGroup canvasGroup;
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

    // Once the grid is fully covered the screen hands off to the game, in two
    // equal beats: the buzz winds down to silence, then the picture goes to
    // black. There used to be a 5-4-3-2-1-СТАРТ graffiti countdown between
    // those two — removed per feedback, along with its pulse/shake animation
    // and the per-digit gear-shift sound.
    [SerializeField] private float audioFadeOutDuration = 2f;
    [SerializeField] private float darkenDuration = 2f;

    // Full-screen black square on top of everything on this canvas, kept at
    // alpha 0 until the darken beat. Fading THIS in (rather than fading the
    // canvas out, which is what the old countdown ending did) is what makes
    // the handoff read as "lights out" instead of "the intro dissolves and
    // you watch the menu appear".
    [SerializeField] private Image darkenOverlay;

    // Wired for all 7 of the loader's game slots (not just БК) — Finish()
    // always calls startScreen.OnRevealed() so the menu's own carousel
    // resets to page 0 right as it becomes visible, regardless of which
    // slot's intro just finished (see OnRevealed's own comment). Only
    // isPrimaryGame's instance also calls PlayMusic(), and it now does so in
    // Finish() — i.e. at the moment the game screen actually appears. It used
    // to fire the instant the "СТАРТ" graffiti showed, several seconds
    // earlier, so the music played under a screen that was still the intro.
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
        SetOverlayAlpha(0f);

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
    // reveals this canvas and starts the fill-then-handoff sequence.
    public void BeginConfirmHold()
    {
        IsRunning = true;
        CanStillAbort = true;
        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        StartCoroutine(RunIntro());
    }

    // Called by LoaderScreenController when the held control is released
    // before the screen finished filling — snaps everything back to its
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
        SetOverlayAlpha(0f);
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

        // Let the last handful still mid-fall actually land before the screen
        // is treated as full, instead of cutting them off mid-air.
        yield return new WaitForSeconds(fallDuration);

        // Screen is covered — from here the start is committed and letting go
        // of the control no longer aborts anything.
        CanStillAbort = false;

        yield return StartCoroutine(FadeOutBuzz());
        yield return StartCoroutine(DarkenToBlackAndFinish());
    }

    // First handoff beat: the buzz winds down to silence. Runs before the
    // darken rather than alongside it, so the screen is already quiet by the
    // time the lights start going out.
    private IEnumerator FadeOutBuzz()
    {
        float startBuzzVolume = buzzSource != null ? buzzSource.volume : 0f;
        float t = 0f;
        while (t < audioFadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / audioFadeOutDuration);
            if (buzzSource != null)
                buzzSource.volume = Mathf.Lerp(startBuzzVolume, 0f, p);
            yield return null;
        }
        if (buzzSource != null)
            buzzSource.Stop();
    }

    private void SetOverlayAlpha(float a)
    {
        if (darkenOverlay == null)
            return;

        Color c = darkenOverlay.color;
        darkenOverlay.color = new Color(c.r, c.g, c.b, a);
    }

    // Second handoff beat: the finished pile goes to black, then the game
    // screen takes over behind it.
    //
    // Deliberately NOT the old approach of fading this canvas out to
    // transparent: that revealed the menu gradually THROUGH the intro, which
    // is the opposite of what "затемнить экран и переключить" asks for. Here
    // the swap happens while the screen is fully black, so it is not seen at
    // all.
    private IEnumerator DarkenToBlackAndFinish()
    {
        float t = 0f;
        while (t < darkenDuration)
        {
            t += Time.deltaTime;
            SetOverlayAlpha(Mathf.Clamp01(t / darkenDuration));
            yield return null;
        }
        SetOverlayAlpha(1f);

        Finish();
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
        CanStillAbort = false; // normally already false by now — RunIntro clears it once the screen is full; this covers its empty-flowers fallback, which jumps straight here

        // Hide this canvas FIRST, so the frame the menu becomes visible is
        // also the frame the black overlay stops covering it — the swap
        // itself happened while the screen was fully black.
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // Reset the overlay for a future run: this canvas is reactivated by
        // BeginConfirmHold, and a second run would otherwise open on black.
        SetOverlayAlpha(0f);

        if (startScreen == null)
            return;

        startScreen.OnRevealed();

        // Music starts here — with the game screen — rather than back when the
        // "СТАРТ" graffiti used to appear.
        if (isPrimaryGame)
            startScreen.PlayMusic();
    }
}
}
