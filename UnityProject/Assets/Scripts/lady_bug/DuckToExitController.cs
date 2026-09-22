using UnityEngine;
using UnityEngine.UI;

// Holding duck (down key, or both-hands-down gesture) on every active player
// at once opens the quit-confirm dialog — the gesture-based replacement for
// the old "hold brake 5s" exit trigger (braking was removed from every
// control scheme). First 5s are silent; the next 5s show a visible
// countdown, so releasing early is an obvious way to cancel. Releasing at
// any point resets the whole thing back to zero. The hold is tracked from
// raw down-input (IsDuckInputHeld), not duck pose — a collision resets the
// visual duck but must not zero the silent phase while down stays held.
//
// The cabinet panel's own exit button reaches the same dialog — it is the
// SYSTEM button on that board (JoystickSerial.EscapeButtonDown, see the field
// index note there) and, unlike the duck hold above, fires the dialog
// instantly on press, with no hold or countdown. debugExitKey stays as the
// keyboard stand-in for it, for working without the cabinet plugged in.
public class DuckToExitController : MonoBehaviour
{
    [SerializeField] private Text countdownText;
    [SerializeField] private float silentPhase = 5f;
    [SerializeField] private float countdownPhase = 5f;

    // Keyboard stand-in for the cabinet's exit button, for developing without
    // the panel plugged in.
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

        // Red opens the question as well as the SYSTEM button does. That gives
        // red two jobs, and they don't collide: outside the dialog it asks
        // "quit?", inside it answers НЕТ (see PauseController). The press that
        // opens the dialog is deliberately not readable by it on the same
        // frame, or it would open and immediately cancel itself.
        //
        // SystemMenuDown already folds in Esc for the no-hardware case, so
        // this path is reachable while developing without the cabinet.
        JoystickSerial panel = JoystickSerial.Instance;
        bool exitButtonPressed = Input.GetKeyDown(debugExitKey)
            || JoystickSerial.SystemMenuDown
            || (panel != null && panel.RedButtonDown);
        if (exitButtonPressed)
        {
            _holdTimer = 0f;
            SetCountdownVisible(false);
            if (PauseController.Instance != null)
                PauseController.Instance.OpenDialog();
            return;
        }

        if (!AreAllPlayersHoldingExit())
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

    // Raw down-input held — not IsDucking. A crash resets duck pose/state but
    // the player may still be holding down for exit; counting that as "released"
    // was resetting the silent phase mid-hold.
    private bool AreAllPlayersHoldingExit()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        if (players.Length == 0)
            return false;

        foreach (var p in players)
        {
            if (!p.IsDuckInputHeld)
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
