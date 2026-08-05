using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

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
        if (valueText == null)
            return;

        if (GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
        {
            valueText.text = FormatHand("Л", leftMm) + "  " + FormatHand("П", rightMm);
            return;
        }

        valueText.text = "Л:–мм  П:–мм";
    }

    private static string FormatHand(string label, int mm)
    {
        return mm < 0 ? label + ":–мм" : label + ":" + mm + "мм";
    }
}
}
