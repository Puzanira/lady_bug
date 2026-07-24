using UnityEngine;

// Reads player 2's physical arcade joystick (4 digital microswitches —
// up/down/left/right, see ArduinoFirmware/Joystick) over serial via
// JoystickSerial and turns it into the same held/just-pressed signal shape
// PlayerController normally reads from keys: up = jump, down = duck,
// left/right = lane change — the same mapping the keyboard already uses.
// Unlike GestureInput's two-hand distance sensors (which need to interpret
// a continuous reading into a gesture, e.g. flapping for jump), a
// joystick's 4 directions already ARE discrete button presses, so no
// interpretation step is needed here at all.
//
// Disabled by default (see SceneSetup.CreatePlayer) — enabled at runtime by
// StartScreenController only for player 2 (left) when "Датчики" is the
// chosen controller, where it stands in for player 2's own gesture reading
// (player 1/right still reads real hand sensors as before).
public class JoystickInput : MonoBehaviour
{
    public bool UpHeld { get; private set; }
    public bool UpDown { get; private set; }
    public bool DownHeld { get; private set; }
    public bool LeftHeld { get; private set; }
    public bool LeftDown { get; private set; }
    public bool RightHeld { get; private set; }
    public bool RightDown { get; private set; }

    private void Update()
    {
        JoystickSerial joystick = JoystickSerial.Instance;
        bool up = joystick != null && joystick.Up;
        bool down = joystick != null && joystick.Down;
        bool left = joystick != null && joystick.Left;
        bool right = joystick != null && joystick.Right;

        UpDown = up && !UpHeld;
        UpHeld = up;

        DownHeld = down;

        LeftDown = left && !LeftHeld;
        LeftHeld = left;

        RightDown = right && !RightHeld;
        RightHeld = right;
    }
}
