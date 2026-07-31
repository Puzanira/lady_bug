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
        // "из" and both "км" rendered smaller than the actual numbers (rich
        // text — Text.supportRichText is on by default) via inline <size>
        // tags, per feedback — only the numbers themselves are values, "из"
        // and "км" are just connective units/words.
        distanceText.text = string.Format("{0:0.0}<size=26> км</size>\n<size=26>из</size>\n{1:0}<size=26> км</size>", traveled, targetKm);
    }
}
