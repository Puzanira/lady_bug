using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{


// Debug HUD for a joystick-controlled player — four directional arrows with a
// centre dot, lit while that direction is held (see the menu-page sketch).
public class JoystickIndicator : MonoBehaviour
{
    [SerializeField] private JoystickInput joystickInput;
    [SerializeField] private Text upArrow;
    [SerializeField] private Text downArrow;
    [SerializeField] private Text leftArrow;
    [SerializeField] private Text rightArrow;
    [SerializeField] private Text centerDot;

    private static readonly Color ActiveColor = new Color(1f, 0.84f, 0.2f);
    private static readonly Color IdleColor = new Color(0.55f, 0.55f, 0.55f);

    private void Awake()
    {
        EnsureGestureCanvasScale();
        EnsureArrowRefs();
        ApplyArrowPresentation();
    }

    private void Start()
    {
        EnsureGestureCanvasScale();
        EnsureArrowRefs();
        ApplyArrowPresentation();
    }

    private void Update()
    {
        if (!TryReadJoystickState(out bool up, out bool down, out bool left, out bool right))
            return;

        SetArrow(upArrow, up);
        SetArrow(downArrow, down);
        SetArrow(leftArrow, left);
        SetArrow(rightArrow, right);

        if (centerDot != null)
        {
            centerDot.gameObject.SetActive(true);
            centerDot.color = up || down || left || right ? ActiveColor : IdleColor;
        }
    }

    private void EnsureArrowRefs()
    {
        if (upArrow != null && downArrow != null && leftArrow != null && rightArrow != null)
            return;

        Transform cross = FindJoystickCross();
        if (cross == null)
            return;

        upArrow ??= FindArrowText(cross, "Up");
        downArrow ??= FindArrowText(cross, "Down");
        leftArrow ??= FindArrowText(cross, "Left");
        rightArrow ??= FindArrowText(cross, "Right");
        centerDot ??= FindArrowText(cross, "Center");
    }

    private static void EnsureGestureCanvasScale()
    {
        GameObject canvas = FindGestureCanvas();
        if (canvas == null)
            return;

        RectTransform rt = canvas.GetComponent<RectTransform>();
        if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
            rt.localScale = Vector3.one;
    }

    private static GameObject FindGestureCanvas()
    {
        GameObject canvas = GameObject.Find("PlayerRightGestureCanvas");
        if (canvas != null)
            return canvas;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject candidate = all[i];
            if (candidate.name != "PlayerRightGestureCanvas" || candidate.hideFlags != HideFlags.None)
                continue;
            if (!candidate.scene.IsValid())
                continue;
            return candidate;
        }

        return null;
    }

    private static Transform FindJoystickCross()
    {
        GameObject canvas = FindGestureCanvas();
        if (canvas == null)
            return null;

        Transform cross = canvas.transform.Find("JoystickCross");
        return cross != null ? cross : canvas.transform;
    }

    private static Text FindArrowText(Transform cross, string name)
    {
        Transform child = cross.Find(name);
        return child != null ? child.GetComponent<Text>() : null;
    }

    // ComicCAT (the game UI font) has no Unicode arrow glyphs — only the
    // centre bullet rendered. Use built-in ASCII labels instead.
    private void ApplyArrowPresentation()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        StyleArrow(upArrow, font, "^", 64);
        StyleArrow(downArrow, font, "v", 64);
        StyleArrow(leftArrow, font, "<", 64);
        StyleArrow(rightArrow, font, ">", 64);
        if (centerDot != null)
        {
            if (font != null)
                centerDot.font = font;
            centerDot.text = "+";
            centerDot.fontSize = 52;
            centerDot.horizontalOverflow = HorizontalWrapMode.Overflow;
            centerDot.verticalOverflow = VerticalWrapMode.Overflow; // 52pt in the same 56-tall box — see StyleArrow
            centerDot.gameObject.SetActive(true);
            centerDot.color = IdleColor;
        }
    }

    private static void StyleArrow(Text arrow, Font font, string glyph, int fontSize)
    {
        if (arrow == null)
            return;

        if (font != null)
            arrow.font = font;
        arrow.text = glyph;
        arrow.fontSize = fontSize;
        arrow.fontStyle = FontStyle.Bold;
        // The glyph box is 56x56 while this font is 64pt — under Unity's
        // default Truncate mode a line taller than its box is not cropped,
        // it is thrown away, and the arrow renders nothing at all. Set here
        // as well as in SceneSetup so an already-saved scene repairs itself
        // without needing Rebuild Scene.
        arrow.horizontalOverflow = HorizontalWrapMode.Overflow;
        arrow.verticalOverflow = VerticalWrapMode.Overflow;
        arrow.gameObject.SetActive(true);
        arrow.color = IdleColor;
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

    private static void SetArrow(Text arrow, bool active)
    {
        if (arrow == null)
            return;

        arrow.gameObject.SetActive(true);
        arrow.color = active ? ActiveColor : IdleColor;
    }
}
}
