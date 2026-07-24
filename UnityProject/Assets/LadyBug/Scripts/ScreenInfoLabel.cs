using UnityEngine;
using UnityEngine.UI;

// Tiny always-on-top readout of the actual render resolution/fullscreen
// state — for diagnosing UI layout mismatches between the Editor Game view
// and a standalone build (CanvasScaler's matchWidthOrHeight only guarantees
// consistent layout if you know what aspect ratio you're actually running
// at).
public class ScreenInfoLabel : MonoBehaviour
{
    [SerializeField] private Text label;

    private void Update()
    {
        if (label == null)
            return;

        label.text = Screen.width + "x" + Screen.height + (Screen.fullScreen ? " FS" : " Win");
    }
}
