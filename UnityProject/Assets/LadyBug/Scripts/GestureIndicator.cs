using UnityEngine;
using UnityEngine.UI;

// HUD readout of a player's current gesture-sensor state (from real sensors
// once connected, or the keyboard simulator) — lights up an arrow per hand
// plus a marker for the flap/jump state, so the full reading is visible
// during play. Sits idle/dim when that player isn't in gesture mode
// (GestureInput disabled).
public class GestureIndicator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private Text indicatorText;

    private void Update()
    {
        if (indicatorText == null || gestureInput == null)
            return;

        indicatorText.text = HandGlyph(gestureInput.LeftHandUp, gestureInput.LeftHandDown)
                            + "  "
                            + FlapGlyph(gestureInput.JumpHeld)
                            + "  "
                            + HandGlyph(gestureInput.RightHandUp, gestureInput.RightHandDown);
    }

    private static string HandGlyph(bool up, bool down)
    {
        if (up) return "<color=#FFD633>↑</color>";
        if (down) return "<color=#FFD633>↓</color>";
        return "<color=#555555>–</color>";
    }

    private static string FlapGlyph(bool flapping)
    {
        return flapping ? "<color=#FFD633>✈</color>" : "<color=#555555>–</color>";
    }
}
