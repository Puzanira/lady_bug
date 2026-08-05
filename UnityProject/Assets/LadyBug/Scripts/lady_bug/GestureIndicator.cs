using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

// HUD readout of a player's current gesture-sensor state (from real sensors
// once connected, or the keyboard simulator) — lights up an arrow per hand
// plus a marker for the flap/jump state, so the full reading is visible
// during play. Sits idle/dim when that player isn't in gesture mode
// (GestureInput disabled).
public class GestureIndicator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private Text indicatorText;

    private readonly GestureInput.HandFlapTracker _leftFlapTracker = new GestureInput.HandFlapTracker();
    private readonly GestureInput.HandFlapTracker _rightFlapTracker = new GestureInput.HandFlapTracker();

    private void Update()
    {
        if (indicatorText == null)
            return;

        if (UsesLinkedGestureInput())
        {
            indicatorText.text = HandGlyph(gestureInput.LeftHandUp, gestureInput.LeftHandDown)
                                + "  "
                                + FlapGlyph(gestureInput.JumpHeld)
                                + "  "
                                + HandGlyph(gestureInput.RightHandUp, gestureInput.RightHandDown);
            return;
        }

        if (GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
        {
            bool flapping = GestureInput.BothHandsFlapping(
                _leftFlapTracker, _rightFlapTracker, leftMm, rightMm);
            indicatorText.text = HandGlyph(GestureInput.HandIsUp(leftMm), GestureInput.HandIsDown(leftMm))
                                + "  "
                                + FlapGlyph(flapping)
                                + "  "
                                + HandGlyph(GestureInput.HandIsUp(rightMm), GestureInput.HandIsDown(rightMm));
            return;
        }

        indicatorText.text = "<color=#555555>–</color>  <color=#555555>–</color>  <color=#555555>–</color>";
    }

    private bool UsesLinkedGestureInput()
    {
        return gestureInput != null
            && gestureInput.enabled
            && gestureInput.UseRealSensors
            && gestureInput.gameObject.activeInHierarchy;
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
}
