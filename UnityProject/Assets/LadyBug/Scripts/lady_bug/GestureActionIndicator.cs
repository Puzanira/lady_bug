using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

// Bottom-most line of the gesture HUD — the single current action in one
// word, for an at-a-glance readout under the raw key squares and the
// interpreted-gesture arrows.
public class GestureActionIndicator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private Text actionText;

    private readonly GestureInput.HandFlapTracker _leftFlapTracker = new GestureInput.HandFlapTracker();
    private readonly GestureInput.HandFlapTracker _rightFlapTracker = new GestureInput.HandFlapTracker();

    private void Update()
    {
        if (actionText == null)
            return;

        actionText.text = CurrentAction();
    }

    private string CurrentAction()
    {
        if (UsesLinkedGestureInput())
            return ActionFromGestureInput(gestureInput);

        if (GestureInput.TryGetLiveHandDistances(gestureInput, out int leftMm, out int rightMm))
            return ActionFromDistances(leftMm, rightMm);

        return "–";
    }

    private bool UsesLinkedGestureInput()
    {
        return gestureInput != null
            && gestureInput.enabled
            && gestureInput.UseRealSensors
            && gestureInput.gameObject.activeInHierarchy;
    }

    private static string ActionFromGestureInput(GestureInput gesture)
    {
        if (gesture.JumpHeld) return "ПРЫЖОК";
        if (gesture.DuckHeld) return "ПРИСЕСТЬ";
        if (gesture.LeanLeftHeld) return "ВЛЕВО";
        if (gesture.LeanRightHeld) return "ВПРАВО";
        return "–";
    }

    private string ActionFromDistances(int leftMm, int rightMm)
    {
        if (GestureInput.BothHandsFlapping(_leftFlapTracker, _rightFlapTracker, leftMm, rightMm))
            return "ПРЫЖОК";
        if (GestureInput.DuckHeldFromDistances(leftMm, rightMm))
            return "ПРИСЕСТЬ";
        if (GestureInput.LeanLeftHeldFromDistances(leftMm, rightMm))
            return "ВЛЕВО";
        if (GestureInput.LeanRightHeldFromDistances(leftMm, rightMm))
            return "ВПРАВО";
        return "–";
    }
}
}
