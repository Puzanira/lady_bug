using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SpeedIndicator : MonoBehaviour
{
    public static SpeedIndicator Instance { get; private set; }

    [SerializeField] private Text speedText;
    // Dedicated gear panel (see SceneSetup.CreateScoreUI, stacked right
    // above the speed panel) — just the current gear's digit and the
    // lever icon, no longer duplicated inside speedText itself.
    [SerializeField] private Text gearDigitText;
    // Persistent gear-lever HUD icon — always visible, not spawned/
    // destroyed per shift. PlayGearShift just flickers it through a
    // random run of leverFrames and settles back on the resting frame
    // (leverFrames[0]).
    [SerializeField] private RawImage leverImage;
    [SerializeField] private Texture2D[] leverFrames;

    private Coroutine _shiftRoutine;

    private void Awake()
    {
        Instance = this;
        if (leverImage != null && leverFrames != null && leverFrames.Length > 0)
            leverImage.texture = leverFrames[0];
    }

    private void Update()
    {
        if (speedText == null || SpeedController.Instance == null)
            return;

        SpeedController sc = SpeedController.Instance;

        // The win-boost climbs speed without limit purely for the "flying
        // off" visual — a number racing into the thousands isn't useful
        // information, so just stop updating the readout for that stretch
        // instead of hiding the panel (which read as it disappearing then
        // popping back once boosting ended) — it freezes on the last real
        // value instead.
        if (sc.IsWinBoosting)
            return;

        speedText.text = string.Format("{0:0.0} км/ч", sc.CurrentSpeed);
        if (gearDigitText != null)
            gearDigitText.text = sc.Gear.ToString();
    }

    // Replaces the old plain "ПЕРЕКЛЮЧЕНИЕ НА N ПЕРЕДАЧУ" text popup — the
    // lever icon stays on screen at all times now, this just plays its
    // shift flicker right where it already sits beside the speed readout,
    // instead of spawning/flying a separate banner across the screen.
    public void PlayGearShift()
    {
        if (leverImage == null || leverFrames == null || leverFrames.Length == 0)
            return;

        if (_shiftRoutine != null)
            StopCoroutine(_shiftRoutine);
        _shiftRoutine = StartCoroutine(PlayShiftAnimation());
    }

    private IEnumerator PlayShiftAnimation()
    {
        const float frameDuration = 0.12f;
        // A longer, livelier flicker than just stepping through the frames
        // once — random picks (never the same frame twice in a row) instead
        // of a fixed short sequence.
        int count = Random.Range(5, 8);
        int lastIndex = -1;
        for (int i = 0; i < count; i++)
        {
            int index;
            do
            {
                index = Random.Range(0, leverFrames.Length);
            } while (leverFrames.Length > 1 && index == lastIndex);
            lastIndex = index;

            if (leverFrames[index] != null)
                leverImage.texture = leverFrames[index];
            yield return new WaitForSeconds(frameDuration);
        }

        leverImage.texture = leverFrames[0];
        _shiftRoutine = null;
    }
}
