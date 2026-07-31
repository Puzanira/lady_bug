using System.Collections.Generic;
using UnityEngine;

namespace LadyBug
{

public class PlayerController : MonoBehaviour
{
    [SerializeField] private int laneCount = 3;
    [SerializeField] private float laneWidth = 3f;
    [SerializeField] private float laneChangeSpeed = 11f;
    [SerializeField] private float laneRepeatDelay = 0.35f;
    [SerializeField] private float laneRepeatInterval = 0.16f;
    [SerializeField] private int startLane = -1; // -1 = default to the middle lane

    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode downKey = KeyCode.DownArrow;

    [SerializeField] private float jumpHeightDelta = 1.4f;
    [SerializeField] private float jumpDuration = 1.3f;
    // Keyboard taps used to end the jump the instant Up was released, which
    // could cut it shorter than the time needed to also press a diagonal
    // for a mid-air lane change. An early release now only ends the jump
    // once at least this much of it has already played — gesture mode
    // never ended early to begin with (see HandleInput).
    [SerializeField] private float minJumpCommitDuration = 0.65f;

    [SerializeField] private float duckHeightDelta = 0.4f;

    [SerializeField] private float heightChangeSpeed = 8f;

    // Small lean into a lane change — up to laneTiltAngle degrees, eased
    // in/out at laneTiltSpeed (degrees/sec) based on actual per-frame
    // lateral movement, not just "a key is held" (so it eases back out on
    // its own as MoveTowards approaches the target lane, same as real
    // cornering lean rather than a hard on/off toggle).
    [SerializeField] private float laneTiltAngle = 12f;
    [SerializeField] private float laneTiltSpeed = 90f;
    private float _currentTilt;

    [SerializeField] private float tumbleDuration = 0.8f;
    [SerializeField] private float tumbleHopHeight = 1f;
    [SerializeField] private float blinkDuration = 1.5f;
    [SerializeField] private float blinkInterval = 0.12f;

    private int _currentLane;
    private float _baseScaleY;
    private float _baseGroundY;
    private float _actionTimer;
    private float _laneRepeatTimer;
    private bool _bounceRising; // Bouncing sub-phase: rising back up vs resting on the occupant
    private bool _bounceCharging; // holding Up mid-bounce for a higher hop — timed, like a ground jump
    private bool _bounceChargeConsumed; // charge timed out while still held — needs a release before charging again

    // Recent-move bookkeeping used to detect the "ring" trick — a full lane
    // swap where one player crosses over airborne while the other passes
    // under them at the same time.
    private int _lastMoveFromLane = -1;
    private int _lastMoveToLane = -1;
    private float _lastMoveTime = -999f;
    private bool _lastMoveWasAirborne;
    private static float _lastRingTrickTime = -999f;
    private const float RingTrickWindow = 0.5f;
    private const float RingTrickCooldown = 0.3f;

    // Shared move-history log, used by the 4 multi-step tricks below
    // (ЧЕХАРДА, СИНХРОН, БОЛЬШОЕ КОЛЬЦО, БЕСКОНЕЧНОСТЬ) — each is a specific
    // shape traced across a few consecutive lane changes, so instead of a
    // bespoke little state machine per trick, every MoveLane call appends
    // one entry here and each detector just pattern-matches the tail of it.
    private struct MoveEvent
    {
        public float time;
        public int fromLane;
        public int toLane;
        public bool airborne; // this player's own IsAirborne at the moment of this specific step
    }
    private readonly List<MoveEvent> _moveHistory = new List<MoveEvent>();
    private const float MoveHistoryWindow = 6f;

    // Every multi-step trick below (ЧЕХАРДА/СИНХРОН/БОЛЬШОЕ КОЛЬЦО/
    // БЕСКОНЕЧНОСТЬ) is defined relative to a 3-lane WINDOW — edge/edge/
    // middle — not the road's own total lane count (laneCount, above), so
    // each one works the same wherever on the road that window happens to
    // sit and however many total lanes the road actually has. One constant
    // (TrickWindowLanes) drives both derived spans: TrickFullSpan is an
    // edge-to-edge move across the whole window (ЧЕХАРДА/СИНХРОН/БОЛЬШОЕ
    // КОЛЬЦО's own 2-lane sweeps), TrickHalfSpan is a middle-to-edge move
    // (БЕСКОНЕЧНОСТЬ's own out-and-back legs).
    private const int TrickWindowLanes = 3;
    private const int TrickFullSpan = TrickWindowLanes - 1;
    private const int TrickHalfSpan = TrickWindowLanes / 2;

    // ЧЕХАРДА (leapfrog) bookkeeping — set on the "top" player (the one
    // Bouncing on a partner) the moment they dismount sideways by a lane,
    // so the "bottom" partner's own subsequent full-window airborne hop
    // starting from that same lane can recognize what it's completing (see
    // TryDetectLeapfrogTrick's own dismountMatches check — the actual
    // "was this really an edge of some 3-lane window" validation happens
    // there, via TrickFullSpan, not here).
    private int _stackDismountFromLane = -1;
    private float _stackDismountTime = -999f;
    private static float _lastLeapfrogTrickTime = -999f;
    private const float LeapfrogTrickCooldown = 0.3f;

    private static float _lastSyncTrickTime = -999f;
    private const float SyncTrickCooldown = 0.3f;
    private static float _lastBigRingTrickTime = -999f;
    private const float BigRingTrickCooldown = 0.3f;
    private static float _lastInfinityTrickTime = -999f;
    private const float InfinityTrickCooldown = 0.3f;

    // ЗАВИСАНИЕ (hover) — seconds since this player's base last actually
    // touched the road, independent of _verticalState's name for it (which
    // reads Normal for a brief moment while a just-ended jump is still
    // easing back down to the road — see UpdateHoverTrickTracking).
    private float _noGroundTimer;
    private const float HoverTrickDuration = 5f;
    private static bool _hoverTrickAwardedThisStreak;

    // Bouncing = landed mid-air on top of another (grounded) player sharing
    // the lane — self-contained oscillation, never touches Jumping, so it
    // can't be cancelled by jump-key release like a normal jump can.
    private enum VerticalState { Normal, Jumping, Ducking, Bouncing }
    private VerticalState _verticalState = VerticalState.Normal;

    private enum CrashState { None, Tumbling, Blinking }
    private CrashState _crashState = CrashState.None;
    private float _crashTimer;
    private bool _wasBouncingBeforeCrash; // resume resting on the partner afterward instead of dropping to the road
    private float _crashBaseY; // ground reference for the tumble hop — the height we were at when we got hit, not always the road
    private Renderer _spriteRenderer;

    // When present and enabled (controller mode = gesture simulator/sensors,
    // or — for player 2 only — a physical joystick), these stand in for the
    // usual key reads — see the wrapper methods below. Gesture takes
    // priority if somehow both were left enabled at once.
    private GestureInput _gestureInput;
    private JoystickInput _joystickInput;
    private bool GestureActive => _gestureInput != null && _gestureInput.enabled;
    private bool JoystickActive => _joystickInput != null && _joystickInput.enabled;

    public int CurrentLane => _currentLane;
    public int LaneCount => laneCount;
    public int HomeLane => startLane;
    public bool IsAirborne => _verticalState == VerticalState.Jumping || _verticalState == VerticalState.Bouncing;
    public bool IsDucking => _verticalState == VerticalState.Ducking;
    public bool IsBouncing => _verticalState == VerticalState.Bouncing;
    // The same unified jump signal HandleInput itself reads (flap gesture,
    // joystick up, or the plain up key, whichever this player is actually
    // using) — exposed for WinSequence's "flap to keep going" prompt at the
    // finish line, so it doesn't need its own separate control-scheme logic.
    public bool IsJumpInputHeld => UpKeyHeld();
    public float TopY => transform.position.y + transform.localScale.y / 2f;

    // Called by WinSequence right before it disables this component and
    // flies the player off-screen — PlayerAnimator keeps running afterward
    // (it isn't disabled) and reads IsAirborne every frame, so without this
    // it would keep whatever ground/air pose the player happened to be in
    // at that instant instead of switching to the wing-flap frames for the
    // "flying away" cutscene.
    public void ForceAirborneVisual()
    {
        _verticalState = VerticalState.Jumping;
    }

    // If the win condition landed mid-crash-blink (UpdateBlink toggles
    // _spriteRenderer.enabled on/off on a timer, see CrashState.Blinking),
    // the sprite could be off at that exact instant — disabling this whole
    // component right after (WinSequence) freezes it there for good, since
    // nothing else ever flips it back on. Same fix as the crash-tumble
    // rotation freeze (see WinSequence's own comment on that): force it
    // back to a normal, visible state before handing off.
    public void ForceVisible()
    {
        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;
    }

    // Used by the start screen to preview where a player will stand before
    // Start() has run (e.g. centering the sole player in 1-player mode).
    public void SetPreviewLane(int lane)
    {
        startLane = lane;
        Vector3 pos = transform.position;
        pos.x = (lane - (laneCount - 1) / 2f) * laneWidth;
        transform.position = pos;
    }

    private void Start()
    {
        _currentLane = startLane >= 0 ? startLane : laneCount / 2;
        _baseScaleY = transform.localScale.y;
        _baseGroundY = transform.position.y - _baseScaleY / 2f;

        Transform sprite = transform.Find("Sprite");
        if (sprite != null)
            _spriteRenderer = sprite.GetComponent<Renderer>();

        _gestureInput = GetComponent<GestureInput>();
        _joystickInput = GetComponent<JoystickInput>();
    }

    // Gesture/joystick-mode stand-ins for the usual key reads — fall
    // straight through to plain keyboard reads whenever neither is active
    // for this player (i.e. keyboard controller selected — behaves exactly
    // as before). A joystick's 4 directions are already discrete presses,
    // so they slot in directly, the same shape the keyboard reads use.
    private bool UpKeyDown() => GestureActive ? _gestureInput.JumpDown : JoystickActive ? _joystickInput.UpDown : Input.GetKeyDown(upKey);
    private bool UpKeyHeld() => GestureActive ? _gestureInput.JumpHeld : JoystickActive ? _joystickInput.UpHeld : Input.GetKey(upKey);
    private bool DownKeyHeld() => GestureActive ? _gestureInput.DuckHeld : JoystickActive ? _joystickInput.DownHeld : Input.GetKey(downKey);
    private bool LeftKeyDown() => GestureActive ? _gestureInput.LeanLeftDown : JoystickActive ? _joystickInput.LeftDown : Input.GetKeyDown(leftKey);
    private bool LeftKeyHeld() => GestureActive ? _gestureInput.LeanLeftHeld : JoystickActive ? _joystickInput.LeftHeld : Input.GetKey(leftKey);
    private bool RightKeyDown() => GestureActive ? _gestureInput.LeanRightDown : JoystickActive ? _joystickInput.RightDown : Input.GetKeyDown(rightKey);
    private bool RightKeyHeld() => GestureActive ? _gestureInput.LeanRightHeld : JoystickActive ? _joystickInput.RightHeld : Input.GetKey(rightKey);

    private void Update()
    {
        switch (_crashState)
        {
            case CrashState.Tumbling:
                UpdateTumble();
                return; // no lane/jump/duck input while spinning
            case CrashState.Blinking:
                UpdateBlink();
                break; // falls through — player can still move while invulnerable
        }

        HandleInput();
        UpdateLanePosition();
        UpdateVerticalState();
        UpdateHoverTrickTracking();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Unity still calls this on a disabled component as long as the
        // GameObject and its Collider are active — disabling this script
        // (StartScreenController on the pre-game menu, WinSequence during
        // the fly-away) does stop input/Update, but NOT collisions, without
        // this explicit check. That let the start screen's ambient good-
        // pickup scroll (EntitySpawner's ordinary drift-by decoration,
        // before the real game even begins) silently register as real
        // collects in AchievementStats, inflating the end-of-run count.
        if (!enabled)
            return;

        if (_crashState != CrashState.None)
            return; // mid-hit-reaction — ignore further collisions

        MovingEntity entity = other.GetComponentInParent<MovingEntity>();
        if (entity == null)
            return;

        DuckUnderObstacle arch = entity.GetComponent<DuckUnderObstacle>();
        if (arch != null)
        {
            if (_verticalState == VerticalState.Ducking)
            {
                // Checked from here, not the rider's own collision, because
                // this collider's height is stable and always overlaps the
                // arch — the rider's swings between resting-on-back and full
                // jump height and can pass above the arch's collider
                // entirely, so their own trigger doesn't always fire.
                PlayerController partner = FindPartnerInLane(_currentLane);
                if (partner != null && partner.IsAirborne)
                    AwardArchTrickOnce(arch, Midpoint(partner));
                return; // ducked under it successfully
            }

            if (IsAirborne)
            {
                // Jumping over it is safe on its own now, solo — the co-op
                // trick is still awarded on top of that if a partner happens
                // to be ducking under it in the same lane at the same time.
                PlayerController partner = FindPartnerInLane(_currentLane);
                if (partner != null && partner.IsDucking)
                    AwardArchTrickOnce(arch, Midpoint(partner)); // in case this fires instead of/as well as the ducker's
                return; // jumped clean over it, alone or with a ducking partner
            }
        }

        // Road-wide arch — the inverse rule: no need to duck, walking or
        // ducking through it is fine, but being airborne when it's reached
        // counts as a hit (falls through to StartCrash() below).
        if (entity.GetComponent<TallArchObstacle>() != null && !IsAirborne)
            return;

        ScoreValue scoreValue = entity.GetComponent<ScoreValue>();
        if (scoreValue != null)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.SpawnPopup(scoreValue.value, transform.position);

            if (scoreValue.value < 0)
            {
                StartCrash();
                if (SfxManager.Instance != null)
                    SfxManager.Instance.PlayBad(entity.gameObject.name);
                if (AchievementStats.Instance != null)
                    AchievementStats.Instance.RecordHit(EntityIcon(entity));
                if (ObjectFeedbackIndicator.Instance != null)
                    ObjectFeedbackIndicator.Instance.OnBadPickup();
            }
            else
            {
                if (SfxManager.Instance != null)
                    SfxManager.Instance.PlayPickup();
                if (AchievementStats.Instance != null)
                    AchievementStats.Instance.RecordCollected(EntityIcon(entity));
                if (ObjectFeedbackIndicator.Instance != null)
                    ObjectFeedbackIndicator.Instance.OnGoodPickup();
            }

            Destroy(entity.gameObject);
            return;
        }

        StartCrash();
        if (SfxManager.Instance != null)
            SfxManager.Instance.PlayBad(entity.gameObject.name);
        if (AchievementStats.Instance != null)
            AchievementStats.Instance.RecordHit(EntityIcon(entity));
    }

    // The exact texture the entity was actually shown with (every spawned
    // good/bad prefab renders its sprite on a child named exactly "Sprite"
    // via a Material, see SceneSetup.CreateEntityPrefab/CreateSnakePrefab/
    // CreateGroundDecalPrefab/CreateArchPrefab — no SpriteRenderer
    // involved) — reading it straight off the live object instead of
    // matching its name against a fixed set of known categories means the
    // post-win icon grid always shows the real picture, for any object
    // type, without needing to be told about it in advance. Looked up by
    // that exact child name rather than GetComponentInChildren<Renderer>()
    // — bad objects also carry a "Shadow" child (AddStaticGroundShadow,
    // added to the hierarchy before "Sprite") whose plain dark material has
    // no texture of its own; GetComponentInChildren returned that one
    // first, so every bad-object hit read back a null icon instead of the
    // real picture.
    private static Texture2D EntityIcon(MovingEntity entity)
    {
        Transform spriteChild = entity.transform.Find("Sprite");
        Renderer renderer = spriteChild != null ? spriteChild.GetComponent<Renderer>() : null;
        return renderer != null && renderer.sharedMaterial != null
            ? renderer.sharedMaterial.mainTexture as Texture2D
            : null;
    }

    private PlayerController FindPartnerInLane(int lane)
    {
        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other != this && other.CurrentLane == lane)
                return other;
        }
        return null;
    }

    private bool HasAirbornePartner()
    {
        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other != this && other.IsAirborne)
                return true;
        }
        return false;
    }

    private bool IsSupportingBouncingPartner()
    {
        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other != this && other.CurrentLane == _currentLane && other._verticalState == VerticalState.Bouncing)
                return true;
        }
        return false;
    }

    private Vector3 Midpoint(PlayerController other) => (transform.position + other.transform.position) / 2f;

    private static void AwardArchTrickOnce(DuckUnderObstacle arch, Vector3 worldPos)
    {
        if (arch.TrickAwarded)
            return;

        arch.TrickAwarded = true;
        if (TricksManager.Instance != null)
            TricksManager.Instance.SpawnPopup("АРКА", worldPos);
    }

    private void StartCrash()
    {
        _crashState = CrashState.Tumbling;
        _crashTimer = 0f;

        // If we were resting on a partner's back, tumble in place at that
        // height and resume Bouncing afterward instead of snapping down to
        // the road — otherwise we'd land right on top of them.
        _wasBouncingBeforeCrash = _verticalState == VerticalState.Bouncing;
        _crashBaseY = transform.position.y - transform.localScale.y / 2f;

        // Reset any jump/duck in progress so the tumble starts from a clean pose.
        _verticalState = VerticalState.Normal;
        Vector3 scale = transform.localScale;
        scale.y = _baseScaleY;
        transform.localScale = scale;
        transform.rotation = Quaternion.identity;
        _currentTilt = 0f; // matches the visual reset above — otherwise UpdateLanePosition would jump straight back to whatever lean was mid-flight when this hit

        if (SpeedController.Instance != null)
            SpeedController.Instance.HalveSpeed();
    }

    private void UpdateTumble()
    {
        _crashTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_crashTimer / tumbleDuration);

        Vector3 pos = transform.position;
        pos.y = _crashBaseY + _baseScaleY / 2f + Mathf.Sin(t * Mathf.PI) * tumbleHopHeight;
        transform.position = pos;

        // Two full turns over the course of the hop.
        transform.rotation = Quaternion.Euler(0f, 0f, t * 720f);

        if (_crashTimer >= tumbleDuration)
        {
            transform.rotation = Quaternion.identity;
            Vector3 landed = transform.position;
            landed.y = _crashBaseY + _baseScaleY / 2f;
            transform.position = landed;

            _crashState = CrashState.Blinking;
            _crashTimer = 0f;

            // Partner's still there grounded in our lane — go back to
            // resting on their back instead of standing next to (on) them.
            if (_wasBouncingBeforeCrash && FindLandingBlocker(_currentLane) != null)
            {
                _verticalState = VerticalState.Bouncing;
                _bounceRising = false;
                _bounceCharging = false;
                _bounceChargeConsumed = false;
                _actionTimer = jumpDuration * 0.4f;
            }
        }
    }

    private void UpdateBlink()
    {
        _crashTimer += Time.deltaTime;

        if (_spriteRenderer != null)
        {
            bool visible = Mathf.FloorToInt(_crashTimer / blinkInterval) % 2 == 0;
            _spriteRenderer.enabled = visible;
        }

        if (_crashTimer >= blinkDuration)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;
            _crashState = CrashState.None;
        }
    }

    private void HandleInput()
    {
        HandleLaneInput();

        if (_verticalState == VerticalState.Normal)
        {
            // Can't jump out from under a partner who's bouncing on our
            // back — they'd lose their landing blocker mid-air and drop
            // straight down into the same spot we just left.
            if (UpKeyDown() && !IsSupportingBouncingPartner())
            {
                _verticalState = VerticalState.Jumping;
                _actionTimer = jumpDuration;
            }
            else if (DownKeyHeld())
            {
                _verticalState = VerticalState.Ducking;
            }
        }
        else if (_verticalState == VerticalState.Ducking)
        {
            // Held, not timed — stays down for as long as the key is down.
            if (!DownKeyHeld())
                _verticalState = VerticalState.Normal;
        }
        else if (_verticalState == VerticalState.Jumping)
        {
            // Explicit "come down now" — applies the instant Down is
            // pressed/held, in every input mode alike, not gated behind the
            // commit-time check below. Previously only gesture mode had
            // this (see the old comment this replaced); keyboard/joystick
            // ignored Down entirely while airborne and just sat through the
            // jump timer, which read as "nothing happens" when a player
            // tried to duck out of a jump early.
            if (DownKeyHeld())
            {
                EndJump();
            }
            else if (!GestureActive)
            {
                // Held up to the jumpDuration cap (enforced in UpdateVerticalState);
                // release early and it comes down immediately — unless someone's
                // still grounded in this lane, then it bounces off them instead.
                // Jumps are normally a tap, so this fires almost every time —
                // it must go through the same occupant check as the timer expiry.
                // A release before minJumpCommitDuration has played is ignored
                // outright, so even the quickest tap still leaves enough airtime
                // to also press a diagonal for a mid-air lane change. Gesture
                // mode skips this branch on purpose — a committed "both hands
                // up" jump only ends via Down (above) or the timer, releasing
                // the gesture itself doesn't cut it short (a player needs to be
                // able to stop flapping mid-air without landing instantly).
                if (!UpKeyHeld() && jumpDuration - _actionTimer >= minJumpCommitDuration)
                    EndJump();
            }
        }
    }

    // Resolves a Jump that's ending (early release or timer expiry): lands
    // normally, or — if another player is still grounded in this lane —
    // bounces off them instead, never touching the road.
    private void EndJump()
    {
        bool blocked = FindLandingBlocker(_currentLane) != null;
        if (blocked)
        {
            _verticalState = VerticalState.Bouncing;
            _bounceRising = false; // dip down onto their back first
            _bounceCharging = false;
            _bounceChargeConsumed = false;
            _actionTimer = jumpDuration * 0.4f;
        }
        else
        {
            _verticalState = VerticalState.Normal;
        }
    }

    private void HandleLaneInput()
    {
        // First tap moves one lane immediately; holding the key keeps moving
        // further after a short delay, then repeats at a steady rate.
        if (LeftKeyDown())
        {
            MoveLane(-1);
            _laneRepeatTimer = laneRepeatDelay;
        }
        else if (RightKeyDown())
        {
            MoveLane(1);
            _laneRepeatTimer = laneRepeatDelay;
        }
        else if (LeftKeyHeld() || RightKeyHeld())
        {
            _laneRepeatTimer -= Time.deltaTime;
            if (_laneRepeatTimer <= 0f)
            {
                MoveLane(LeftKeyHeld() ? -1 : 1);
                _laneRepeatTimer = laneRepeatInterval;
            }
        }
    }

    private void MoveLane(int direction)
    {
        int target = Mathf.Clamp(_currentLane + direction, 0, laneCount - 1);
        if (target == _currentLane || IsLaneOccupiedByOther(target))
            return;

        int previousLane = _currentLane;
        _currentLane = target;

        _lastMoveFromLane = previousLane;
        _lastMoveToLane = target;
        _lastMoveTime = Time.time;
        _lastMoveWasAirborne = IsAirborne;

        _moveHistory.Add(new MoveEvent { time = Time.time, fromLane = previousLane, toLane = target, airborne = IsAirborne });
        _moveHistory.RemoveAll(e => Time.time - e.time > MoveHistoryWindow);

        if (_verticalState == VerticalState.Bouncing)
            TryRecordStackDismount(previousLane);

        TryDetectRingTrick();
        TryDetectLeapfrogTrick();
        TryDetectSyncTrick();
        TryDetectBigRingTrick();
        TryDetectInfinityTrick();
    }

    // ЧЕХАРДА (leapfrog) — top half: the Bouncing rider dismounts sideways
    // by one lane, freeing the partner underneath them to complete the
    // trick (see TryDetectLeapfrogTrick).
    private void TryRecordStackDismount(int from)
    {
        _stackDismountFromLane = from;
        _stackDismountTime = Time.time;
    }

    // ЧЕХАРДА (leapfrog) — bottom half: freed from supporting the rider
    // (see above), this player jumps and clears both remaining lanes in one
    // continuous airborne hop — two same-direction moves back to back,
    // never landing in between — from the edge lane the stack was just in,
    // straight over to the opposite edge.
    private void TryDetectLeapfrogTrick()
    {
        if (Time.time - _lastLeapfrogTrickTime < LeapfrogTrickCooldown)
            return;
        if (_moveHistory.Count < 2)
            return;

        MoveEvent last = _moveHistory[_moveHistory.Count - 1];
        MoveEvent prev = _moveHistory[_moveHistory.Count - 2];

        bool bothAirborne = last.airborne && prev.airborne;
        bool sameDirection = (last.toLane - last.fromLane) == (prev.toLane - prev.fromLane);
        bool spansBothEdges = Mathf.Abs(last.toLane - prev.fromLane) == TrickFullSpan;
        bool closeInTime = last.time - prev.time < 0.6f;
        if (!(bothAirborne && sameDirection && spansBothEdges && closeInTime))
            return;

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;

            bool dismountMatches = other._stackDismountFromLane == prev.fromLane &&
                Time.time - other._stackDismountTime < 3f;
            if (dismountMatches)
            {
                _lastLeapfrogTrickTime = Time.time;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("ЧЕХАРДА", Midpoint(other));
                return;
            }
        }
    }

    // СИНХРОН — the grounded "base" of a stack (Bouncing rider still on
    // their back) does the same clean 2-lane sweep ЧЕХАРДА's bottom half
    // does, except this time the rider stays put, riding along instead of
    // dismounting — confirmed by finding them still specifically Bouncing
    // (not just any airborne state — a coincidental solo jump landing in
    // the same lane at the same moment shouldn't count) and co-located
    // right after this move completes.
    private void TryDetectSyncTrick()
    {
        if (Time.time - _lastSyncTrickTime < SyncTrickCooldown)
            return;
        if (IsAirborne) // only the grounded base half of the stack checks this
            return;
        if (_moveHistory.Count < 2)
            return;

        MoveEvent last = _moveHistory[_moveHistory.Count - 1];
        MoveEvent prev = _moveHistory[_moveHistory.Count - 2];

        bool bothGrounded = !last.airborne && !prev.airborne;
        bool sameDirection = (last.toLane - last.fromLane) == (prev.toLane - prev.fromLane);
        bool spansBothEdges = Mathf.Abs(last.toLane - prev.fromLane) == TrickFullSpan;
        bool closeInTime = last.time - prev.time < 0.6f;
        if (!(bothGrounded && sameDirection && spansBothEdges && closeInTime))
            return;

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;

            if (other.IsBouncing && other.CurrentLane == CurrentLane)
            {
                _lastSyncTrickTime = Time.time;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("СИНХРОН", Midpoint(other));
                return;
            }
        }
    }

    // БОЛЬШОЕ КОЛЬЦО — this player's own last 4 lane moves form a there-
    // and-back loop across all 3 lanes: 2 grounded steps sweeping edge to
    // edge one way, then 2 airborne steps sweeping back the other way.
    private bool HasBigRingPattern(out float endTime)
    {
        endTime = 0f;
        if (_moveHistory.Count < 4)
            return false;

        int n = _moveHistory.Count;
        MoveEvent g1 = _moveHistory[n - 4];
        MoveEvent g2 = _moveHistory[n - 3];
        MoveEvent a1 = _moveHistory[n - 2];
        MoveEvent a2 = _moveHistory[n - 1];

        bool groundLeg = !g1.airborne && !g2.airborne;
        bool airLeg = a1.airborne && a2.airborne;
        bool chained = g1.toLane == g2.fromLane && a1.fromLane == g2.toLane && a2.fromLane == a1.toLane;
        bool groundSweep = Mathf.Abs(g2.toLane - g1.fromLane) == TrickFullSpan;
        bool airSweep = a2.toLane == g1.fromLane; // back where it started
        bool closeInTime = a2.time - g1.time < 3f;

        if (groundLeg && airLeg && chained && groundSweep && airSweep && closeInTime)
        {
            endTime = a2.time;
            return true;
        }
        return false;
    }

    private void TryDetectBigRingTrick()
    {
        if (Time.time - _lastBigRingTrickTime < BigRingTrickCooldown)
            return;
        if (!HasBigRingPattern(out float myEnd))
            return;

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;

            if (other.HasBigRingPattern(out float otherEnd) && Mathf.Abs(myEnd - otherEnd) < 2f)
            {
                _lastBigRingTrickTime = Time.time;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("БОЛЬШОЕ КОЛЬЦО", Midpoint(other));
                return;
            }
        }
    }

    // БЕСКОНЕЧНОСТЬ — from the middle lane of some 3-lane window, out to
    // one edge of it and back (grounded out, airborne back — a jump
    // bridges the two), then out to the other edge and back the same
    // way — a figure-8 traced across those three lanes. "Middle" is
    // wherever this particular figure-8 actually started (g1.fromLane),
    // not a fixed road-wide middle — same reasoning as TrickFullSpan
    // above, so this works from any 3-lane window on the road.
    private bool HasInfinityPattern(out float endTime)
    {
        endTime = 0f;
        if (_moveHistory.Count < 4)
            return false;

        int n = _moveHistory.Count;
        MoveEvent g1 = _moveHistory[n - 4];
        MoveEvent a1 = _moveHistory[n - 3];
        MoveEvent g2 = _moveHistory[n - 2];
        MoveEvent a2 = _moveHistory[n - 1];

        int mid = g1.fromLane;
        bool leg1 = !g1.airborne && Mathf.Abs(g1.toLane - mid) == TrickHalfSpan;
        bool ret1 = a1.airborne && a1.fromLane == g1.toLane && a1.toLane == mid;
        bool leg2 = !g2.airborne && g2.fromLane == mid && Mathf.Abs(g2.toLane - mid) == TrickHalfSpan && g2.toLane != g1.toLane;
        bool ret2 = a2.airborne && a2.fromLane == g2.toLane && a2.toLane == mid;
        bool closeInTime = a2.time - g1.time < 6f;

        if (leg1 && ret1 && leg2 && ret2 && closeInTime)
        {
            endTime = a2.time;
            return true;
        }
        return false;
    }

    private void TryDetectInfinityTrick()
    {
        if (Time.time - _lastInfinityTrickTime < InfinityTrickCooldown)
            return;
        if (!HasInfinityPattern(out float myEnd))
            return;

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;

            if (other.HasInfinityPattern(out float otherEnd) && Mathf.Abs(myEnd - otherEnd) < 4f)
            {
                _lastInfinityTrickTime = Time.time;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("БЕСКОНЕЧНОСТЬ", Midpoint(other));
                return;
            }
        }
    }

    // ЗАВИСАНИЕ — both players airborne at once, continuously, without
    // either one's base ever touching the road again, for
    // HoverTrickDuration straight.
    private void UpdateHoverTrickTracking()
    {
        bool baseOnGround = _crashState == CrashState.None &&
            Mathf.Abs((transform.position.y - transform.localScale.y / 2f) - _baseGroundY) < 0.02f;
        _noGroundTimer = baseOnGround ? 0f : _noGroundTimer + Time.deltaTime;

        if (_hoverTrickAwardedThisStreak)
        {
            if (baseOnGround)
                _hoverTrickAwardedThisStreak = false;
            return;
        }

        if (_noGroundTimer < HoverTrickDuration)
            return;

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;

            if (other._noGroundTimer >= HoverTrickDuration)
            {
                _hoverTrickAwardedThisStreak = true;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("ЗАВИСАНИЕ", Midpoint(other));
                return;
            }
        }
    }

    // "Ring" trick: two players cross through each other's lanes at once —
    // one airborne, one grounded — a full swap of places, like passing
    // through a ring. Works in either direction; whoever completes the
    // swap second is the one that notices and scores it.
    private void TryDetectRingTrick()
    {
        if (Time.time - _lastRingTrickTime < RingTrickCooldown)
            return; // already scored this exact swap from the other player's side

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;

            bool recentEnough = Time.time - other._lastMoveTime <= RingTrickWindow;
            bool isSwap = other._lastMoveFromLane == _lastMoveToLane && other._lastMoveToLane == _lastMoveFromLane;
            bool oneAirborneOneGrounded = _lastMoveWasAirborne != other._lastMoveWasAirborne;

            if (recentEnough && isSwap && oneAirborneOneGrounded)
            {
                _lastRingTrickTime = Time.time;
                if (TricksManager.Instance != null)
                    TricksManager.Instance.SpawnPopup("КОЛЬЦО", Midpoint(other));
                return;
            }
        }
    }

    // Blocked only if BOTH players would be grounded in that lane at once —
    // either one being airborne (jumping/bouncing) is enough to pass through,
    // whichever side is in the air.
    private bool IsLaneOccupiedByOther(int lane)
    {
        return !IsAirborne && FindGroundedOccupant(lane) != null;
    }

    private PlayerController FindGroundedOccupant(int lane)
    {
        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this)
                continue;
            if (other.CurrentLane == lane && !other.IsAirborne)
                return other;
        }
        return null;
    }

    // Used when a jump/bounce is resolving whether it can touch down. Blocks
    // if another player is grounded here — or, if both players are mid-air
    // in the same lane at once (simultaneous jumps merging into it), the tie
    // is broken by a stable priority (lower instance ID lands first) so
    // exactly one of them lands while the other keeps bouncing on top,
    // instead of both dropping onto the road together.
    private PlayerController FindLandingBlocker(int lane)
    {
        PlayerController grounded = null;
        PlayerController airbornePeer = null;

        foreach (var other in FindObjectsByType<PlayerController>())
        {
            if (other == this || other.CurrentLane != lane)
                continue;

            if (!other.IsAirborne)
                grounded = other;
            else if (GetEntityId() > other.GetEntityId())
                airbornePeer = other; // the lower ID wins the tie and lands
        }

        return grounded != null ? grounded : airbornePeer;
    }

    private void UpdateLanePosition()
    {
        float targetX = (_currentLane - (laneCount - 1) / 2f) * laneWidth;
        Vector3 pos = transform.position;
        float previousX = pos.x;
        pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * Time.deltaTime);
        transform.position = pos;

        // Lean toward whichever direction actually moved this frame — zero
        // once MoveTowards reaches the target lane and dx drops to zero, so
        // this eases back out on its own without a separate "done moving"
        // check.
        float dx = pos.x - previousX;
        float targetTilt = Mathf.Abs(dx) > 0.0001f ? -Mathf.Sign(dx) * laneTiltAngle : 0f;
        _currentTilt = Mathf.MoveTowards(_currentTilt, targetTilt, laneTiltSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, _currentTilt);
    }

    private void UpdateVerticalState()
    {
        // Jump lifts the whole cube off the road (position moves, scale stays put).
        // Duck shrinks the cube while keeping its base anchored to the road.
        float targetScaleY = _baseScaleY;
        float targetPosY = _baseGroundY + _baseScaleY / 2f;

        if (_verticalState == VerticalState.Jumping)
        {
            targetPosY = _baseGroundY + _baseScaleY / 2f + jumpHeightDelta;
        }
        else if (_verticalState == VerticalState.Ducking)
        {
            targetScaleY = Mathf.Max(0.2f, _baseScaleY - duckHeightDelta);
            targetPosY = _baseGroundY + targetScaleY / 2f;
        }
        else if (_verticalState == VerticalState.Bouncing)
        {
            // Self-contained oscillation between "resting on the other
            // player's back" and "up at full jump height" — never dips to
            // road level while they're still grounded in this lane. Holding
            // Up charges a higher hop — the same height gain as a solo jump,
            // but measured from the partner's back instead of the road (so
            // a ducked partner naturally starts the hop a bit lower). Follows
            // _bounceCharging (not the raw key) so it drops back out of the
            // charged height once the timeout consumes the charge, even if
            // the key is still held down.
            PlayerController occupant = FindLandingBlocker(_currentLane);
            if (occupant != null && _bounceCharging)
                targetPosY = occupant.TopY + _baseScaleY / 2f + jumpHeightDelta;
            else if (_bounceRising || occupant == null)
                targetPosY = _baseGroundY + _baseScaleY / 2f + jumpHeightDelta;
            else
                targetPosY = occupant.TopY + _baseScaleY / 2f;
        }

        Vector3 scale = transform.localScale;
        scale.y = Mathf.MoveTowards(scale.y, targetScaleY, heightChangeSpeed * Time.deltaTime);
        transform.localScale = scale;

        Vector3 pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, targetPosY, heightChangeSpeed * Time.deltaTime);
        transform.position = pos;

        // Only Jump/Bounce are timed — Ducking reverts as soon as the key is
        // released (handled in HandleInput), not on a countdown.
        if (_verticalState == VerticalState.Jumping)
        {
            _actionTimer -= Time.deltaTime;
            if (_actionTimer <= 0f)
            {
                // Holding Up through the timeout re-arms straight into
                // another jump instead of landing — but only once a
                // partner is ALSO already airborne, so a solo jump is
                // never affected: it always still lands right at
                // jumpDuration exactly as before. This is what lets two
                // players chain-hover together for ЗАВИСАНИЕ without ever
                // touching the road (see UpdateHoverTrickTracking); a lone
                // player holding Up gets nothing extra from it.
                if (UpKeyHeld() && HasAirbornePartner())
                    _actionTimer = jumpDuration;
                else
                    EndJump();
            }
        }
        else if (_verticalState == VerticalState.Bouncing)
        {
            PlayerController occupant = FindLandingBlocker(_currentLane);
            bool holdingUp = occupant != null && UpKeyHeld();

            if (!holdingUp)
                _bounceChargeConsumed = false; // released — armed for another charge

            if (holdingUp && !_bounceCharging && !_bounceChargeConsumed)
            {
                // Start a charge — capped at jumpDuration even if held
                // longer, same timeout a ground jump has.
                _bounceCharging = true;
                _actionTimer = jumpDuration;
            }
            else if (!holdingUp && _bounceCharging)
            {
                // Released early — falls back into the ordinary auto-cycle,
                // same as releasing a ground jump early brings it down.
                _bounceCharging = false;
                _bounceRising = false;
                _actionTimer = jumpDuration * 0.4f;
            }

            _actionTimer -= Time.deltaTime;
            if (_actionTimer <= 0f)
            {
                if (occupant == null)
                {
                    _verticalState = VerticalState.Normal; // lane's clear — fall the rest of the way down
                }
                else
                {
                    // Timed out while still held — needs a fresh release+press
                    // to charge again, so holding Up doesn't hang up forever.
                    if (_bounceCharging)
                        _bounceChargeConsumed = true;
                    _bounceCharging = false;
                    _bounceRising = !_bounceRising; // flip between resting on them and hopping back up
                    _actionTimer = jumpDuration * 0.4f;
                }
            }
        }
    }
}
}
