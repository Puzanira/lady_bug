using UnityEngine;
using UnityEngine.UI;

namespace LadyBug
{

// Holding duck (down key, or both-hands-down gesture) on every active player
// at once opens the quit-confirm dialog — the gesture-based replacement for
// the old "hold brake 5s" exit trigger (braking was removed from every
// control scheme). First 5s are silent; the next 5s show a visible
// countdown, so releasing early is an obvious way to cancel. Releasing at
// any point resets the whole thing back to zero.
//
// A dedicated physical exit button is coming to the controller too (exact
// button not chosen yet — see GestureSensorSerial.ExitButtonPressed) —
// unlike the duck hold above, that one fires the same dialog instantly on
// press, no hold/countdown. DebugExitKey stands in for it on a keyboard
// until the real button's wired.
public class DuckToExitController : MonoBehaviour
{
    [SerializeField] private Text countdownText;
    [SerializeField] private float silentPhase = 5f;
    [SerializeField] private float countdownPhase = 5f;

    // STUB — keyboard stand-in for GestureSensorSerial.ExitButtonPressed
    // until the real hardware button exists. Swap/remove once it does.
    [SerializeField] private KeyCode debugExitKey = KeyCode.Backspace;

    private float _holdTimer;

    private void Update()
    {
        if (SpeedController.Instance == null || !SpeedController.Instance.IsRunning)
            return; // start screen — nothing to exit from yet

        if (HelpController.Instance != null && HelpController.Instance.IsOpen)
            return; // Q on the help screen already covers this
        if (PauseController.Instance != null && PauseController.Instance.IsDialogOpen)
            return; // already open — don't restack

        bool exitButtonPressed = Input.GetKeyDown(debugExitKey)
            || (GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.ExitButtonPressed);
        if (exitButtonPressed)
        {
            _holdTimer = 0f;
            SetCountdownVisible(false);
            if (PauseController.Instance != null)
                PauseController.Instance.OpenDialog();
            return;
        }

        if (!AreAllPlayersDucking())
        {
            _holdTimer = 0f;
            SetCountdownVisible(false);
            return;
        }

        _holdTimer += Time.deltaTime;

        if (_holdTimer < silentPhase)
        {
            SetCountdownVisible(false);
        }
        else if (_holdTimer < silentPhase + countdownPhase)
        {
            SetCountdownVisible(true);
            int secondsLeft = Mathf.CeilToInt(silentPhase + countdownPhase - _holdTimer);
            if (countdownText != null)
                countdownText.text = "ВЫХОД ЧЕРЕЗ " + secondsLeft;
        }
        else
        {
            _holdTimer = 0f;
            SetCountdownVisible(false);
            if (PauseController.Instance != null)
                PauseController.Instance.OpenDialog();
        }
    }

    private bool AreAllPlayersDucking()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>();
        if (players.Length == 0)
            return false;

        foreach (var p in players)
        {
            if (!p.IsDucking)
                return false;
        }
        return true;
    }

    private void SetCountdownVisible(bool visible)
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(visible);
    }
}
}
