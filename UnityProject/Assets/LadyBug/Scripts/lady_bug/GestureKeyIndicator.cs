using UnityEngine;
using UnityEngine.UI;

// Raw keyboard-state debug view for the gesture simulator — one square per
// physical key, lit while held. Separate from GestureIndicator, which shows
// the INTERPRETED gesture (jump/duck/lean/flap/brake) instead of the raw
// keys — this one is just "is this exact key currently down or not".
public class GestureKeyIndicator : MonoBehaviour
{
    [SerializeField] private GestureInput gestureInput;
    [SerializeField] private Image[] squares; // order matches KeyOrder() below

    private static readonly Color PressedColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    private static readonly Color IdleColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);

    private KeyCode[] _keys;

    private void Start()
    {
        if (gestureInput != null)
            _keys = KeyOrder(gestureInput);
    }

    private void Update()
    {
        if (_keys == null || squares == null)
            return;

        for (int i = 0; i < squares.Length && i < _keys.Length; i++)
        {
            if (squares[i] != null)
                squares[i].color = Input.GetKey(_keys[i]) ? PressedColor : IdleColor;
        }
    }

    public static KeyCode[] KeyOrder(GestureInput gesture)
    {
        return new[]
        {
            gesture.LeftHandUpKey, gesture.LeftHandDownKey,
            gesture.RightHandUpKey, gesture.RightHandDownKey
        };
    }
}
