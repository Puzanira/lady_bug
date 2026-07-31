using UnityEngine;

// Gear-shift moment gets a one-shot clunk; a looping engine hum's
// volume/pitch rises through each gear ("нарастание внутри передачи") and
// drops straight back down the instant it shifts, then starts rising again —
// same shape as SpeedController.Gear itself.
public class GearSfx : MonoBehaviour
{
    [SerializeField] private AudioSource shiftSource; // one-shot
    [SerializeField] private AudioSource humSource; // looping
    [SerializeField] private float minVolume = 0.15f;
    [SerializeField] private float maxVolume = 0.6f;
    [SerializeField] private float minPitch = 0.85f;
    [SerializeField] private float maxPitch = 1.3f;

    private int _lastGear = -1;

    private void Update()
    {
        if (SpeedController.Instance == null || !SpeedController.Instance.IsRunning)
            return;

        // Win-boost sends speed rocketing up without limit purely for
        // visual drama (SpeedController.BeginWinBoost) — reacting to every
        // gear it blows through on the way would spam shift sounds/popups
        // dozens of times a second. Stop reacting the instant the win
        // sequence starts and freeze whatever hum was playing; nothing
        // needs updating once the "ВЫ ПОБЕДИЛИ" screen is up.
        if (WinSequence.Instance != null && WinSequence.Instance.Triggered)
        {
            if (humSource != null && humSource.isPlaying)
                humSource.Stop();
            return;
        }

        SpeedController sc = SpeedController.Instance;
        int gear = sc.Gear;

        if (_lastGear != -1 && gear != _lastGear)
        {
            if (shiftSource != null)
                shiftSource.Play();
            // Only celebrate speeding up — a crash-induced drop in gear
            // isn't something to flash the lever icon for.
            if (gear > _lastGear && SpeedIndicator.Instance != null)
                SpeedIndicator.Instance.PlayGearShift();
        }
        _lastGear = gear;

        if (humSource == null)
            return;

        // How far through the current gear's speed range we are (0 right
        // after a shift, 1 right before the next one) — feeds both volume
        // and pitch.
        float gearFloor = (gear - 1) * sc.GearStepKmh;
        float t = Mathf.Clamp01((sc.CurrentSpeed - gearFloor) / sc.GearStepKmh);
        humSource.volume = Mathf.Lerp(minVolume, maxVolume, t);
        humSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);

        if (!humSource.isPlaying)
            humSource.Play();
    }
}
