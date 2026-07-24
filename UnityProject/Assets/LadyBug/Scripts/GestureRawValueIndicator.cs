using UnityEngine;
using UnityEngine.UI;

// Raw sensor readout for the gesture HUD — sits between the raw key squares
// (GestureKeyIndicator) and the interpreted-gesture arrows (GestureIndicator):
// shows the hand distances (mm) a real sensor would report, before they get
// thresholded into Up/Down, plus the brake button's raw pressed/not state.
public class GestureRawValueIndicator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private Text valueText;

    private void Update()
    {
        if (valueText == null || gestureInput == null)
            return;

        valueText.text = "Л:" + gestureInput.LeftHandDistanceMm + "мм"
                        + "  П:" + gestureInput.RightHandDistanceMm + "мм";
    }
}
