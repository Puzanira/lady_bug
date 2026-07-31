using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// First screen shown when the game boots — a plain "attract mode" idle
// screen mimicking a real arcade cabinet: a schematic of the physical
// control panel at the bottom (see SceneSetup.CreateLoaderScreen) with a
// prompt cycling through what to try, sliding in from the right edge,
// pausing centered, then sliding out the left — a different randomly
// chosen control on the panel is framed to match whichever prompt is
// showing. On a real cabinet each of the mega-project's 7 games gets its
// own physical button; for now (debug) the number keys 1-7 stand in for
// them — gameIntros[i] is whichever game key (i+1) starts, index-matched to
// gameStartKeys. Only index 0 (БК/lady_bug) has a real IntroSequence wired
// up so far; the other 6 are reserved (null) until those games exist.
// Holding the key down hands off to that game's own flower/countdown-style
// screen, and releasing before its countdown finishes aborts back here
// instead of letting it complete.
public class LoaderScreenController : MonoBehaviour
{
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private Text messageText;

    [SerializeField]
    private KeyCode[] gameStartKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7,
    };

    // Index-matched to gameStartKeys — see class comment.
    [SerializeField] private IntroSequence[] gameIntros;

    private static readonly string[] Messages =
    {
        "НАЖМИТЕ ЛЮБУЮ КНОПКУ",
        "НАКРОЙТЕ ЛЮБОЙ ДАТЧИК",
        "ПОШЕВЕЛИТЕ ЛЮБОЙ ДЖОЙСТИК",
        "ПОКРУТИТЕ ЛЮБУЮ РУКОЯТКУ",
    };

    // Needs to clear the reference-resolution half-width (960) plus half
    // the message box's own width (see SceneSetup.CreateLoaderScreen,
    // messageRt.sizeDelta.x = 1700 -> 850) for the text to actually finish
    // off-screen rather than just past center — 2200 leaves a safe margin.
    [SerializeField] private float slideDistance = 2200f;
    [SerializeField] private float slideInDuration = 0.5f;
    [SerializeField] private float holdDuration = 1.6f;
    [SerializeField] private float slideOutDuration = 0.5f;

    // Index-matched to Messages above — message i highlights a random
    // element from pool i. Some categories have more than one real element
    // on the panel (several buttons/knobs), some just one (joystick/sensor).
    [SerializeField] private GameObject[] buttonHighlights;
    [SerializeField] private GameObject[] sensorHighlights;
    [SerializeField] private GameObject[] joystickHighlights;
    [SerializeField] private GameObject[] knobHighlights;

    private Vector2 _messageRestPos;
    private GameObject _activeHighlight;
    // Which of gameStartKeys is currently being held, or -1 if none — has to
    // be a specific index (not just "is anything still held") so releasing
    // the one key that started the hold is what's checked, not any other
    // unrelated key the player might also be touching.
    private int _holdingIndex = -1;
    private IntroSequence _activeIntro;

    private void Awake()
    {
        if (messageText != null)
            _messageRestPos = messageText.rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        StartCoroutine(CycleMessages());
    }

    private void Update()
    {
        if (_holdingIndex < 0)
        {
            for (int i = 0; i < gameStartKeys.Length; i++)
            {
                if (!Input.GetKey(gameStartKeys[i]))
                    continue;

                IntroSequence intro = (gameIntros != null && i < gameIntros.Length) ? gameIntros[i] : null;
                if (intro == null)
                    continue; // that game's key is reserved but not built yet — no-op for now

                _holdingIndex = i;
                _activeIntro = intro;
                if (canvasRoot != null)
                    canvasRoot.SetActive(false);
                _activeIntro.BeginConfirmHold();
                break;
            }
        }
        else if (!Input.GetKey(gameStartKeys[_holdingIndex]))
        {
            // Only abort while the grid is still filling with objects
            // (CanStillAbort) — once the 5-4-3-2-1 countdown itself starts,
            // a release is just the player letting go of a button they
            // don't need anymore, not a cancel, per feedback.
            if (_activeIntro != null && _activeIntro.CanStillAbort)
            {
                _activeIntro.AbortIfIncomplete();
                if (canvasRoot != null)
                    canvasRoot.SetActive(true);
            }
            _holdingIndex = -1;
            _activeIntro = null;
        }
    }

    private IEnumerator CycleMessages()
    {
        int i = 0;
        while (true)
        {
            ShowHighlightForCategory(i);
            yield return StartCoroutine(SlideMessage(Messages[i]));
            HideActiveHighlight();
            i = (i + 1) % Messages.Length;
        }
    }

    private IEnumerator SlideMessage(string text)
    {
        if (messageText == null)
            yield break;

        messageText.text = text;
        RectTransform rt = messageText.rectTransform;

        yield return StartCoroutine(SlideX(rt, slideDistance, 0f, slideInDuration));
        yield return new WaitForSeconds(holdDuration);
        yield return StartCoroutine(SlideX(rt, 0f, -slideDistance, slideOutDuration));
    }

    private IEnumerator SlideX(RectTransform rt, float fromX, float toX, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = _messageRestPos + new Vector2(Mathf.Lerp(fromX, toX, p), 0f);
            yield return null;
        }
        rt.anchoredPosition = _messageRestPos + new Vector2(toX, 0f);
    }

    private void ShowHighlightForCategory(int messageIndex)
    {
        GameObject[] pool = null;
        if (messageIndex == 0) pool = buttonHighlights;
        else if (messageIndex == 1) pool = sensorHighlights;
        else if (messageIndex == 2) pool = joystickHighlights;
        else if (messageIndex == 3) pool = knobHighlights;

        if (pool == null || pool.Length == 0)
            return;

        _activeHighlight = pool[Random.Range(0, pool.Length)];
        if (_activeHighlight != null)
            _activeHighlight.SetActive(true);
    }

    private void HideActiveHighlight()
    {
        if (_activeHighlight != null)
            _activeHighlight.SetActive(false);
        _activeHighlight = null;
    }
}
