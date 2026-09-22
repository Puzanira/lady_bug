using UnityEngine;

namespace LadyBug
{

// Reads player 2's physical joystick (2-axis analog stick, thresholded into
// 4 directions by the firmware itself — see ArduinoFirmware/Joystick) over
// serial via JoystickSerial and turns it into the same held/just-pressed
// signal shape PlayerController normally reads from keys: up = jump,
// down = duck, left/right = lane change — the same mapping the keyboard
// already uses. Unlike GestureInput's two-hand distance sensors (which need
// to interpret a continuous reading into a gesture, e.g. flapping for
// jump), the firmware already reduces the joystick to discrete button-style
// directions, so no interpretation step is needed here at all.
//
// Disabled by default (see SceneSetup.CreatePlayer) — enabled at runtime by
// StartScreenController only for player 2 (right) when "Датчики" is the
// chosen controller, where it stands in for player 2's own gesture reading
// (player 1/left still reads real hand sensors, see joystickRight's own
// comment in StartScreenController for the left/right swap this reflects).
public class JoystickInput : MonoBehaviour
{
    public bool UpHeld { get; private set; }
    public bool UpDown { get; private set; }
    public bool DownHeld { get; private set; }
    public bool DownDown { get; private set; }
    public bool LeftHeld { get; private set; }
    public bool LeftDown { get; private set; }
    public bool RightHeld { get; private set; }
    public bool RightDown { get; private set; }

    // Nothing ticks a switched-off component, so without this every flag above keeps
    // the value it happened to hold at the moment it was switched off — and a wrapper
    // switched off mid-deflection goes on reporting a direction held for the rest of
    // the session. The menu deactivates PlayerRight the instant 1 ИГРОК is chosen, and
    // that choice is made by pushing the stick sideways, so this was not a corner case:
    // it is the ordinary way through the menu. GestureInput already clears itself the
    // same way in its own OnDisable; this is the matching half for the stick.
    private void OnDisable()
    {
        UpHeld = false;
        UpDown = false;
        DownHeld = false;
        DownDown = false;
        LeftHeld = false;
        LeftDown = false;
        RightHeld = false;
        RightDown = false;
    }

    private void Update()
    {
        JoystickSerial joystick = JoystickSerial.Instance;
        bool up = joystick != null && joystick.Up;
        bool down = joystick != null && joystick.Down;
        bool left = joystick != null && joystick.Left;
        bool right = joystick != null && joystick.Right;

        UpDown = up && !UpHeld;
        UpHeld = up;

        DownDown = down && !DownHeld;
        DownHeld = down;

        LeftDown = left && !LeftHeld;
        LeftHeld = left;

        RightDown = right && !RightHeld;
        RightHeld = right;
    }
}
}
