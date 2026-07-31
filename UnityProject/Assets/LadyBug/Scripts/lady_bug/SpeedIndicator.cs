using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Combined speed/gear HUD hub (top-left fan, see SceneSetup.CreateScoreUI) —
// the gear number sits in the center as a plain digit; speed within the
// current gear (0..GearStepKmh) lights up a curved row of tick dots around
// it, green through red, like an analog gauge instead of a numeric readout.
public class SpeedIndicator : MonoBehaviour
{
    public static SpeedIndicator Instance { get; private set; }

    [SerializeField] private Text gearDigitText;
    [SerializeField] private Image[] speedTicks; // ordered low speed (green) to high (red)
    [SerializeField] private Color dimTickColor = new Color(1f, 1f, 1f, 0.15f);

    private Color[] _tickLitColors;
    private Coroutine _shiftRoutine;

    private void Awake()
    {
        Instance = this;

        if (speedTicks != null)
        {
            _tickLitColors = new Color[speedTicks.Length];
            for (int i = 0; i < speedTicks.Length; i++)
            {
                float t = speedTicks.Length > 1 ? (float)i / (speedTicks.Length - 1) : 0f;
                _tickLitColors[i] = Color.Lerp(Color.green, Color.red, t);
                if (speedTicks[i] != null)
                    speedTicks[i].color = dimTickColor;
            }
        }
    }

    private void Update()
    {
        if (SpeedController.Instance == null)
            return;

        SpeedController sc = SpeedController.Instance;

        if (gearDigitText != null)
            gearDigitText.text = sc.Gear.ToString();

        // Win-boost sends speed rocketing up without limit purely for
        // visual drama — freezing the gauge at whatever it last showed
        // reads better than it strobing through the whole rainbow dozens
        // of times a second as it blows through every gear.
        if (sc.IsWinBoosting || speedTicks == null || _tickLitColors == null)
            return;

        float gearFloor = (sc.Gear - 1) * sc.GearStepKmh;
        float t2 = Mathf.Clamp01((sc.CurrentSpeed - gearFloor) / sc.GearStepKmh);
        int lit = Mathf.RoundToInt(t2 * speedTicks.Length);

        for (int i = 0; i < speedTicks.Length; i++)
        {
            if (speedTicks[i] == null)
                continue;
            speedTicks[i].color = i < lit ? _tickLitColors[i] : dimTickColor;
        }
    }

    // Replaces the old lever-flicker animation — that whole icon is gone
    // now (see SceneSetup, just a plain digit in the hub) — a quick scale
    // punch on the gear digit itself instead, so the shift moment still
    // gets some visual feedback rather than none.
    public void PlayGearShift()
    {
        if (gearDigitText == null)
            return;

        if (_shiftRoutine != null)
            StopCoroutine(_shiftRoutine);
        _shiftRoutine = StartCoroutine(PulseGearDigit());
    }

    private IEnumerator PulseGearDigit()
    {
        RectTransform rt = gearDigitText.rectTransform;
        Vector3 baseScale = rt.localScale;
        const float duration = 0.25f;
        const float peakScale = 1.4f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float pulse = Mathf.Sin(p * Mathf.PI) * (peakScale - 1f);
            rt.localScale = baseScale * (1f + pulse);
            yield return null;
        }

        rt.localScale = baseScale;
        _shiftRoutine = null;
    }
}
