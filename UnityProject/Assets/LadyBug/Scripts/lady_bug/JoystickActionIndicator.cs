using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{


// Bottom action readout for a joystick-controlled player — mirrors
// GestureActionIndicator but reads JoystickInput instead.
public class JoystickActionIndicator : MonoBehaviour
{
    [SerializeField] private JoystickInput joystickInput;
    [SerializeField] private Text actionText;

    private void Update()
    {
        if (actionText == null)
            return;

        if (!TryReadJoystickState(out bool up, out bool down, out bool left, out bool right))
        {
            actionText.text = "–";
            return;
        }

        if (up)
            actionText.text = "ПРЫЖОК";
        else if (down)
            actionText.text = "ПРИСЕСТЬ";
        else if (left)
            actionText.text = "ВЛЕВО";
        else if (right)
            actionText.text = "ВПРАВО";
        else
            actionText.text = "–";
    }

    private bool TryReadJoystickState(out bool up, out bool down, out bool left, out bool right)
    {
        up = down = left = right = false;

        JoystickSerial serial = JoystickSerial.Instance;
        if (serial != null && serial.IsConnected)
        {
            up = serial.Up;
            down = serial.Down;
            left = serial.Left;
            right = serial.Right;
            return true;
        }

        if (joystickInput != null && joystickInput.enabled)
        {
            up = joystickInput.UpHeld;
            down = joystickInput.DownHeld;
            left = joystickInput.LeftHeld;
            right = joystickInput.RightHeld;
            return true;
        }

        return false;
    }
}
}
