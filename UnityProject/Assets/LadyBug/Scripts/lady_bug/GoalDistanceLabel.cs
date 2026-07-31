using UnityEngine;
using UnityEngine.UI;

// Keeps the start screen's "проехать N км" goal line in sync with
// WinSequence's actual win distance, which is temporarily lowered for
// faster debug/test runs — a hardcoded "100 км" in the instructions would
// lie about the real target while testing.
public class GoalDistanceLabel : MonoBehaviour
{
    [SerializeField] private Text label;

    private void OnEnable()
    {
        if (label == null || WinSequence.Instance == null)
            return;

        label.text = "проехать " + WinSequence.Instance.WinDistanceKm.ToString("0") + " км";
    }
}
