using UnityEngine;

namespace LadyBug
{

// No more player accel/brake input at all (control scheme redesign removed
// it everywhere) — the road now always accelerates on its own, and only
// slows down when a player crashes into something (HalveSpeed).
public class SpeedController : MonoBehaviour
{
    public static SpeedController Instance { get; private set; }

    [SerializeField] private float minSpeed = 3f;
    [SerializeField] private float maxSpeed = 200f;
    [SerializeField] private float baseAccel = 6f; // accel rate at speed 0, before the falloff below
    [SerializeField] private float accelFalloffSpeed = 15f; // accel rate halves every this many km/h of speed gained
    [SerializeField] private float gearStepKmh = 10f;
    // CurrentSpeed is display km/h — literally converting that to distance
    // (dividing by 3600) treats one game-second as one real-world driving
    // second, which makes 100 km take the better part of an hour to reach.
    // This compresses that so the counter actually moves at an arcade pace
    // instead — a tunable "how many pretend km/h-hours pass per real
    // second" multiplier, not a physically meaningful unit.
    [SerializeField] private float distancePaceMultiplier = 15f;
    [SerializeField] private float winDecelRate = 40f; // km/h shed per second once the boost ends
    // Tuned so a ~3s pre-fly hold (WinSequence.entityFadeDuration) climbs
    // from minSpeed to ~85–90 km/h — v ≈ v0 + a0·t + ½·ramp·t² with defaults.
    [SerializeField] private float winBoostInitialAccel = 15f;
    [SerializeField] private float winBoostAccelRamp = 10f;

    public float CurrentSpeed { get; private set; }
    public float MinSpeed => minSpeed;
    public float MaxSpeed => maxSpeed;

    // 1-based — gear N covers from (N-1)*gearStepKmh up to N*gearStepKmh.
    public int Gear => Mathf.Max(1, Mathf.FloorToInt(CurrentSpeed / gearStepKmh) + 1);
    public float GearStepKmh => gearStepKmh;

    // Distance travelled this run, in km — CurrentSpeed integrated over time
    // (it's in km/h, so a per-frame deltaTime in seconds needs /3600).
    public float DistanceKm { get; private set; }

    // Peak speed reached this run — its own leaderboard category, tracked
    // separately from CurrentSpeed since a late crash would otherwise lower
    // it right before the run ends.
    public float MaxSpeedReached { get; private set; }

    private bool _winBoost;
    private bool _winDecelerate;
    private float _winAccel;
    private bool _gameStarted;
    private bool _paused;
    private float _speedBeforePause;

    public bool IsRunning => _gameStarted;
    public bool IsAtMinSpeed => CurrentSpeed <= minSpeed + 0.01f;
    // True only for the "players flying off toward the horizon" stretch of
    // the win cutscene — SpeedIndicator hides itself for that window since
    // the number climbing into the thousands is dramatic, not informative.
    public bool IsWinBoosting => _winBoost;

    private void Awake()
    {
        Instance = this;
        ResetForMenu();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Clears all run state — menu idle, or right before a scene reload after a win.</summary>
    public void ResetForMenu()
    {
        _gameStarted = false;
        _winBoost = false;
        _winDecelerate = false;
        _winAccel = 0f;
        _paused = false;
        _speedBeforePause = 0f;
        // Ambient idle scroll on the start screen — road, dashed lines and
        // side scenery drift by in the background instead of sitting frozen.
        CurrentSpeed = minSpeed;
        DistanceKm = 0f;
        MaxSpeedReached = 0f;
    }

    /// <summary>Called by the start screen once a mode is confirmed — releases the road.</summary>
    public void BeginGame()
    {
        ResetForMenu();
        _gameStarted = true;
        CurrentSpeed = minSpeed;
        DebugRunConfig.DisableRoadSpawners();
        DebugRunConfig.ClearRoadEntities();
    }

    /// <summary>Called by the pause dialog — freezes the road (and everything driven by
    /// CurrentSpeed: entities, dashed lines, spawners) without losing the pre-pause speed.</summary>
    public void SetPaused(bool paused)
    {
        if (paused == _paused)
            return;

        _paused = paused;
        if (paused)
        {
            _speedBeforePause = CurrentSpeed;
            CurrentSpeed = 0f;
        }
        else
        {
            CurrentSpeed = _speedBeforePause;
        }
    }

    /// <summary>Called on collision — the road slows but keeps moving, never below the floor.
    /// The only way speed ever goes down now that braking is gone.</summary>
    public void HalveSpeed()
    {
        CurrentSpeed = Mathf.Max(CurrentSpeed * 0.5f, minSpeed);
    }

    /// <summary>Victory sequence — speed climbs forever, faster and faster, ignoring the normal falloff.
    /// Meant to last only as long as the players are still visible flying away.</summary>
    public void BeginWinBoost()
    {
        _winBoost = true;
        _winDecelerate = false;
        _winAccel = winBoostInitialAccel;
        // OfferContinue's deceleration can leave CurrentSpeed at 0 — floor
        // at minSpeed so the pre-fly ramp (and PlayerAnimator) start cleanly.
        CurrentSpeed = Mathf.Max(CurrentSpeed, minSpeed);
    }

    /// <summary>Called once the players have flown off-screen — the road eases back down to a
    /// stop instead of the boost (or the number on the speed panel) running forever.</summary>
    public void EndWinBoost()
    {
        _winBoost = false;
        _winDecelerate = true;
    }

    /// <summary>Cancels an in-progress EndWinBoost decelerate — used when WinSequence's
    /// "flap to continue" prompt (which also eases the road to a stop via EndWinBoost,
    /// same effect) gets a flap: normal acceleration resumes from whatever speed it had
    /// reached, instead of continuing down to a full stop.</summary>
    public void CancelDecelerate()
    {
        _winDecelerate = false;
    }

    private void LateUpdate()
    {
        if (!_gameStarted || _paused)
            return;

        if (_winBoost)
        {
            _winAccel += winBoostAccelRamp * Time.deltaTime;
            CurrentSpeed += _winAccel * Time.deltaTime;
            return;
        }

        if (_winDecelerate)
        {
            CurrentSpeed = Mathf.Max(0f, CurrentSpeed - winDecelRate * Time.deltaTime);
            return;
        }

        // "Logarithmic" acceleration: the accel rate itself shrinks as speed
        // climbs (hyperbolic falloff, halving every accelFalloffSpeed units
        // gained) — fast gains early, slower and slower later, without ever
        // fully plateauing or needing a hard, noticeable speed cap.
        float rate = baseAccel / (1f + CurrentSpeed / accelFalloffSpeed);
        CurrentSpeed = Mathf.Min(maxSpeed, CurrentSpeed + rate * Time.deltaTime);
        MaxSpeedReached = Mathf.Max(MaxSpeedReached, CurrentSpeed);

        DistanceKm += CurrentSpeed * Time.deltaTime / 3600f * distancePaceMultiplier;
        if (WinSequence.Instance != null)
            WinSequence.Instance.TryTrigger(DistanceKm);
    }
}
}
