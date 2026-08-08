using UnityEngine;

namespace LadyBug
{

// Real frame animation for a lane object: swaps the quad's texture on a timer,
// picking the next frame at random rather than marching through them in order.
//
// This is what LaneWalker's own class comment said could not be done — "a
// literal per-species animation would need several consistent hand-picked
// frames, which independent AI generations can't reliably match each other's
// style/proportions for". That held for text-to-image, where every call
// invents the character again. Feeding the existing sprite back in as an
// image-to-image edit and changing only the pose keeps one character across
// all frames, so the objection no longer applies.
//
// Frames must share a canvas size (they are exported aligned bottom-centre on
// one, see asset_gen) — the quad is scaled once from the first frame's aspect
// and never touched again, so a differently-shaped frame would visibly stretch.
public class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Texture2D[] frames;
    [SerializeField] private float frameDuration = 0.16f;
    // Random, not sequential: these poses are not a walk cycle in order, and
    // shuffling reads as a lively animal rather than a looping tape. Spawned
    // copies also start on different frames so a row of them isn't in lockstep.
    [SerializeField] private bool randomOrder = true;

    private float _timer;
    private int _index = -1;
    private Material _material;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer == null || frames == null || frames.Length == 0)
        {
            enabled = false;
            return;
        }

        // .material, not .sharedMaterial — each spawned instance needs its own
        // so several dogs on screen don't all show the same frame.
        _material = targetRenderer.material;

        _index = Random.Range(0, frames.Length);
        Apply();
        _timer = Random.Range(0f, frameDuration);
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;

        _timer = frameDuration;
        _index = NextIndex();
        Apply();
    }

    private int NextIndex()
    {
        if (frames.Length == 1)
            return 0;

        if (!randomOrder)
            return (_index + 1) % frames.Length;

        // Never the same frame twice running — a repeat reads as the animation
        // having stalled, which is exactly what this replaced.
        int next = Random.Range(0, frames.Length - 1);
        if (next >= _index)
            next++;
        return next;
    }

    private void Apply()
    {
        Texture2D frame = frames[_index];
        if (frame != null && _material != null)
            _material.mainTexture = frame;
    }
}
}
