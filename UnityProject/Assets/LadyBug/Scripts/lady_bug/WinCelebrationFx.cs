using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{


// Full-screen confetti and firework bursts on the final win hold — see
// WinSequence.RunSequence.
//
// Used to be two 420px squares flanking «ПОЗДРАВЛЯЕМ!!!», both playing at once
// on the score canvas. Now they take the whole screen and alternate — confetti,
// then firework, then round again — on their own canvas above everything else.
// One image, not two: only one effect is on screen at a time, so a second
// RawImage would just sit transparent.
public class WinCelebrationFx : MonoBehaviour
{
    const string ConfettiResourcePath = "Celebration/Confetti";
    const string FireworkResourcePath = "Celebration/Firework";

    // Above PhotoCaptureCanvas (210) and the new-record announce (215) — the
    // brief says in front of the whole screen, and those are the only things
    // that could still be up at this point.
    const int SortingOrder = 220;

    // How many times the pair repeats. One round is a full confetti burst
    // followed by a full firework burst.
    const int CycleCount = 3;

    [SerializeField] private RawImage fxImage;
    // Faster than the old flanking versions (18/14): a burst that fills the
    // screen reads as sluggish at the pace that looked fine on a small square.
    [SerializeField] private float confettiFps = 24f;
    [SerializeField] private float fireworkFps = 20f;

    // Both bursts sit high in their own frame, so centred on screen they read
    // as floating above the text. Nudged down — the confetti further, since its
    // sheet has more empty headroom than the firework's. Each got a further
    // 108px (a tenth of the 1080 reference height) on top of the first pass.
    // Firework is the yellow one (single hue, RGB ~255/227/50); confetti is the
    // multicoloured one — checked against the frames, not guessed from the names.
    [SerializeField] private float fireworkOffsetY = -228f;
    [SerializeField] private float confettiOffsetY = -346f;

    Texture2D[] _confettiFrames;
    Texture2D[] _fireworkFrames;
    float _timer;
    int _frameIndex;
    bool _showingFirework;   // false = confetti half of the round
    int _roundsCompleted;
    bool _playing;

    public bool CyclesComplete => _roundsCompleted >= CycleCount || !HasAnyFrames;

    bool HasAnyFrames =>
        (_confettiFrames != null && _confettiFrames.Length > 0) ||
        (_fireworkFrames != null && _fireworkFrames.Length > 0);

    public static WinCelebrationFx Ensure(Canvas canvas)
    {
        var existing = canvas.GetComponentInChildren<WinCelebrationFx>(true);
        // An instance saved by an older scene build has no fxImage (it had a
        // left/right pair instead) — throw it away and build the new shape.
        if (existing != null && existing.fxImage == null)
        {
            Object.Destroy(existing.gameObject);
            existing = null;
        }

        if (existing != null)
        {
            existing.ApplyLayout();
            return existing;
        }

        var rootGo = new GameObject("WinCelebrationFx");
        rootGo.transform.SetParent(canvas.transform, false);
        var rootRt = rootGo.AddComponent<RectTransform>();
        Stretch(rootRt);

        // Its own canvas so it draws above the recap panels and the photo
        // screen regardless of sibling order under the score canvas.
        var ownCanvas = rootGo.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder = SortingOrder;

        var fx = rootGo.AddComponent<WinCelebrationFx>();
        fx.fxImage = CreateFullScreenImage(rootGo.transform, "Fx");
        rootGo.SetActive(false);
        return fx;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void ApplyLayout()
    {
        if (fxImage != null)
            LayoutFullScreen(fxImage.rectTransform, CurrentOffsetY);
    }

    float CurrentOffsetY => _showingFirework ? fireworkOffsetY : confettiOffsetY;

    // The frames are square (512 confetti, 256 firework). Stretching them to
    // 16:9 would visibly oval the bursts, so the square is sized to the canvas
    // WIDTH and allowed to overflow top and bottom instead.
    static void LayoutFullScreen(RectTransform rt, float offsetY = 0f)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, offsetY);

        float side = 1920f; // canvas reference width — see CanvasScaler on the score canvas
        var parent = rt.parent as RectTransform;
        if (parent != null && parent.rect.width > 1f)
            side = Mathf.Max(parent.rect.width, parent.rect.height);
        rt.sizeDelta = new Vector2(side, side);
    }

    static RawImage CreateFullScreenImage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<RawImage>();
        img.raycastTarget = false;
        img.color = Color.white;
        LayoutFullScreen(rt);
        return img;
    }

    void Awake()
    {
        _confettiFrames = LoadFrames(ConfettiResourcePath);
        _fireworkFrames = LoadFrames(FireworkResourcePath);
        ApplyLayout();
        SetPlaying(false);
    }

    static Texture2D[] LoadFrames(string resourceFolder)
    {
        var frames = Resources.LoadAll<Texture2D>(resourceFolder);
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        return frames;
    }

    Texture2D[] CurrentFrames => _showingFirework ? _fireworkFrames : _confettiFrames;
    float CurrentStep => 1f / Mathf.Max(1f, _showingFirework ? fireworkFps : confettiFps);

    public void SetPlaying(bool playing)
    {
        _playing = playing;
        gameObject.SetActive(playing);

        if (!playing)
            return;

        _timer = 0f;
        _frameIndex = 0;
        _roundsCompleted = 0;
        // Always open on confetti — unless there are no confetti frames at all,
        // in which case the firework half carries the whole show.
        _showingFirework = _confettiFrames == null || _confettiFrames.Length == 0;
        ApplyLayout();
        ApplyFrame();
    }

    public IEnumerator WaitForCyclesComplete()
    {
        while (_playing && !CyclesComplete)
            yield return null;
    }

    void Update()
    {
        if (!_playing || CyclesComplete)
            return;

        Texture2D[] frames = CurrentFrames;
        if (frames == null || frames.Length == 0)
        {
            AdvanceHalf();
            return;
        }

        // unscaledDeltaTime: the win cutscene runs with the game paused.
        _timer += Time.unscaledDeltaTime;
        float step = CurrentStep;
        while (_timer >= step)
        {
            _timer -= step;
            if (_frameIndex >= frames.Length - 1)
            {
                AdvanceHalf();
                return;
            }
            _frameIndex++;
            ApplyFrame();
        }
    }

    // Hand over to the other effect; a round is done once the firework half
    // finishes, since a round is confetti-then-firework.
    void AdvanceHalf()
    {
        if (_showingFirework)
            _roundsCompleted++;

        _showingFirework = !_showingFirework;
        _frameIndex = 0;
        _timer = 0f;

        if (CyclesComplete)
            return;

        ApplyLayout(); // у эффектов разное смещение по вертикали
        ApplyFrame();
    }

    void ApplyFrame()
    {
        Texture2D[] frames = CurrentFrames;
        if (fxImage == null || frames == null || frames.Length == 0)
            return;
        fxImage.texture = frames[Mathf.Clamp(_frameIndex, 0, frames.Length - 1)];
    }
}
}
