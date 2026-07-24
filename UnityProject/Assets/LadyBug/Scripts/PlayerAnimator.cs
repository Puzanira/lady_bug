using UnityEngine;

// Two things layered on the same sprite: a procedural tilt (slow while
// grounded — running legs, faster/smaller while airborne — flapping wings),
// and a genuine multi-frame pose cycle — one set of frames for the ground
// (leg pose only differs) and one for the air (wings open, flapping), each
// generated via image *edits* of frame1, not independent generations,
// specifically so the body/shell/colors stay pixel-consistent between
// frames instead of drifting the way independent AI generations do.
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform sprite;
    [SerializeField] private float runTiltAngle = 6f;
    [SerializeField] private float runCycleSpeed = 10f;
    [SerializeField] private float flapTiltAngle = 10f;
    [SerializeField] private float flapCycleSpeed = 22f;
    [SerializeField] private Texture2D[] groundFrames; // walk-cycle order
    [SerializeField] private Texture2D[] airFrames; // wing-flap cycle order
    [SerializeField] private float legFrameInterval = 0.12f;
    [SerializeField] private float wingFrameInterval = 0.09f; // flapping reads faster than running legs

    private Renderer _spriteRenderer;
    private int _frameIndex;
    private float _frameTimer;
    private bool _wasAirborne;

    private void Awake()
    {
        if (sprite != null)
            _spriteRenderer = sprite.GetComponent<Renderer>();
    }

    private void Update()
    {
        if (sprite == null || player == null)
            return;

        float angle = player.IsAirborne
            ? Mathf.Sin(Time.time * flapCycleSpeed) * flapTiltAngle
            : Mathf.Sin(Time.time * runCycleSpeed) * runTiltAngle;
        sprite.localRotation = Quaternion.Euler(0f, 0f, angle);

        UpdateFrame();
    }

    private void UpdateFrame()
    {
        if (_spriteRenderer == null)
            return;

        bool airborne = player.IsAirborne;
        Texture2D[] frames = airborne ? airFrames : groundFrames;
        if (frames == null || frames.Length == 0)
            return;

        // Reset to the start of whichever cycle we just switched into, so a
        // fresh jump always begins its wing-flap from the same frame rather
        // than wherever the ground cycle happened to leave off.
        if (airborne != _wasAirborne)
        {
            _frameIndex = 0;
            _frameTimer = 0f;
            _spriteRenderer.material.mainTexture = frames[0];
            _wasAirborne = airborne;
            return;
        }

        float interval = airborne ? wingFrameInterval : legFrameInterval;
        _frameTimer += Time.deltaTime;
        if (_frameTimer >= interval)
        {
            _frameTimer = 0f;
            _frameIndex = (_frameIndex + 1) % frames.Length;
            _spriteRenderer.material.mainTexture = frames[_frameIndex];
        }
    }
}
