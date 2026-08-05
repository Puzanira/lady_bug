using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{


// Confetti (left) + firework (right) flanking «ПОЗДРАВЛЯЕМ!!!» on the
// final win hold only — see WinSequence.RunSequence.
public class WinCelebrationFx : MonoBehaviour
{
    const string ConfettiResourcePath = "Celebration/Confetti";
    const string FireworkResourcePath = "Celebration/Firework";

    [SerializeField] private RawImage leftConfetti;
    [SerializeField] private RawImage rightFirework;
    [SerializeField] private float confettiFps = 18f;
    [SerializeField] private float fireworkFps = 14f;
    const int CycleCount = 3;

    Texture2D[] _confettiFrames;
    Texture2D[] _fireworkFrames;
    float _confettiTimer;
    float _fireworkTimer;
    int _confettiIndex;
    int _fireworkIndex;
    bool _playing;
    int _confettiCyclesCompleted;
    int _fireworkCyclesCompleted;

    public bool CyclesComplete =>
        (_confettiFrames == null || _confettiFrames.Length == 0 || _confettiCyclesCompleted >= CycleCount)
        && (_fireworkFrames == null || _fireworkFrames.Length == 0 || _fireworkCyclesCompleted >= CycleCount);

    public static WinCelebrationFx Ensure(Canvas canvas)
    {
        var existing = canvas.GetComponentInChildren<WinCelebrationFx>(true);
        if (existing != null && (existing.leftConfetti == null || existing.rightFirework == null))
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
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        var fx = rootGo.AddComponent<WinCelebrationFx>();
        fx.leftConfetti = CreateSide(rootGo.transform, "LeftConfetti", new Vector2(-520f, -140f), 420f);
        fx.rightFirework = CreateSide(rootGo.transform, "RightFirework", new Vector2(520f, -140f), 420f);
        rootGo.SetActive(false);
        return fx;
    }

    void ApplyLayout()
    {
        LayoutSide(leftConfetti, new Vector2(-520f, -140f), 420f);
        LayoutSide(rightFirework, new Vector2(520f, -140f), 420f);
    }

    static void LayoutSide(RawImage img, Vector2 anchoredPos, float size)
    {
        if (img == null)
            return;
        var rt = img.rectTransform;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
    }

    static RawImage CreateSide(Transform parent, string name, Vector2 anchoredPos, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<RawImage>();
        img.raycastTarget = false;
        img.color = Color.white;
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

    public void SetPlaying(bool playing)
    {
        _playing = playing;
        gameObject.SetActive(playing);

        if (!playing)
            return;

        _confettiTimer = 0f;
        _fireworkTimer = 0f;
        _confettiIndex = 0;
        _fireworkIndex = 0;
        _confettiCyclesCompleted = 0;
        _fireworkCyclesCompleted = 0;
        ApplyFrame(leftConfetti, _confettiFrames, _confettiIndex);
        ApplyFrame(rightFirework, _fireworkFrames, _fireworkIndex);
    }

    public IEnumerator WaitForCyclesComplete()
    {
        while (_playing && !CyclesComplete)
            yield return null;
    }

    void Update()
    {
        if (!_playing)
            return;

        if (_confettiFrames != null && _confettiFrames.Length > 0 && leftConfetti != null
            && _confettiCyclesCompleted < CycleCount)
        {
            float step = 1f / confettiFps;
            AdvanceSideCycles(leftConfetti, _confettiFrames, ref _confettiTimer, ref _confettiIndex, step,
                ref _confettiCyclesCompleted, CycleCount);
        }

        if (_fireworkFrames != null && _fireworkFrames.Length > 0 && rightFirework != null
            && _fireworkCyclesCompleted < CycleCount)
        {
            float step = 1f / fireworkFps;
            AdvanceSideCycles(rightFirework, _fireworkFrames, ref _fireworkTimer, ref _fireworkIndex, step,
                ref _fireworkCyclesCompleted, CycleCount);
        }
    }

    static void AdvanceSideCycles(RawImage target, Texture2D[] frames, ref float timer, ref int index, float step,
        ref int cyclesCompleted, int cycleCount)
    {
        timer += Time.unscaledDeltaTime;
        while (timer >= step)
        {
            timer -= step;
            if (index >= frames.Length - 1)
            {
                cyclesCompleted++;
                if (cyclesCompleted >= cycleCount)
                    return;
                index = 0;
                ApplyFrame(target, frames, index);
                continue;
            }
            index++;
            ApplyFrame(target, frames, index);
        }
    }

    static void ApplyFrame(RawImage target, Texture2D[] frames, int index)
    {
        if (target == null || frames == null || frames.Length == 0)
            return;
        target.texture = frames[index];
    }
}
}
