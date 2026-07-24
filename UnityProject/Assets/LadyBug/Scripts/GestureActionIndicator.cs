using UnityEngine;
using UnityEngine.UI;

// Bottom-most line of the gesture HUD — the single current action in one
// word, for an at-a-glance readout under the raw key squares and the
// interpreted-gesture arrows.
public class GestureActionIndicator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private Text actionText;

    private void Update()
    {
        if (actionText == null || gestureInput == null)
            return;

        actionText.text = CurrentAction();
    }

    private string CurrentAction()
    {
        if (gestureInput.JumpHeld) return "ПРЫЖОК";
        if (gestureInput.DuckHeld) return "ПРИСЕСТЬ";
        if (gestureInput.LeanLeftHeld) return "ВЛЕВО";
        if (gestureInput.LeanRightHeld) return "ВПРАВО";
        return "–";
    }
}
