using UnityEngine;
using UnityEngine.UI;

// "X из Y" readout (just the numbers, no "traveled/remaining" wording) —
// distance travelled is also the win condition now (WinSequence/
// SpeedController), so this doubles as a progress bar toward the goal.
// Reads the goal distance straight from WinSequence (same pattern
// GoalDistanceLabel uses) instead of its own separate copy — a duplicated
// value drifted out of sync with WinSequence's own temporarily-lowered
// debug distance before.
public class DistanceIndicator : MonoBehaviour
{
    [SerializeField] private Text distanceText;

    private void Update()
    {
        if (distanceText == null || SpeedController.Instance == null || WinSequence.Instance == null)
            return;

        float traveled = SpeedController.Instance.DistanceKm;
        float targetKm = WinSequence.Instance.WinDistanceKm;
        distanceText.text = string.Format("{0:0.0} из {1:0}", traveled, targetKm);
    }
}
