using UnityEngine;
using UnityEngine.UI;

public class GameTimer : MonoBehaviour
{
    public static GameTimer Instance { get; private set; }

    [SerializeField] private Text timerText;

    private float _startTime;
    private bool _running;
    private bool _paused;
    private float _pauseStartedAt;
    private float _pausedTotal;

    public float Elapsed
    {
        get
        {
            if (!_running)
                return 0f;
            float livePause = _paused ? Time.time - _pauseStartedAt : 0f;
            return Time.time - _startTime - _pausedTotal - livePause;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Called by the start screen once a mode is confirmed.</summary>
    public void BeginTiming()
    {
        _startTime = Time.time;
        _running = true;
        _paused = false;
        _pausedTotal = 0f;
    }

    /// <summary>Called by the pause dialog — the clock stands still while it's up.</summary>
    public void Pause()
    {
        if (!_running || _paused)
            return;
        _paused = true;
        _pauseStartedAt = Time.time;
    }

    public void Resume()
    {
        if (!_running || !_paused)
            return;
        _paused = false;
        _pausedTotal += Time.time - _pauseStartedAt;
    }

    private void Update()
    {
        if (!_running || timerText == null)
            return;

        float t = Elapsed;
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
