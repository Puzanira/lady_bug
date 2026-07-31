using UnityEngine;

// F1 toggles a help overlay showing the full controls — freezes the road
// and input while it's up, the same way PauseController's dialog does.
public class HelpController : MonoBehaviour
{
    public static HelpController Instance { get; private set; }

    [SerializeField] private GameObject helpRoot;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (helpRoot != null)
            helpRoot.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Q))
        {
            // Hand off straight to the quit-confirm dialog without resuming
            // the game in between — braking was removed from every control
            // scheme, so this help-screen key is the only way left to reach
            // the quit prompt.
            IsOpen = false;
            if (helpRoot != null)
                helpRoot.SetActive(false);
            if (PauseController.Instance != null)
                PauseController.Instance.OpenDialog();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.F1))
            return;

        if (SpeedController.Instance == null || !SpeedController.Instance.IsRunning)
            return; // still on the start screen

        if (!IsOpen && PauseController.Instance != null && PauseController.Instance.IsDialogOpen)
            return; // don't stack over the quit-confirm dialog

        if (IsOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        IsOpen = true;
        if (helpRoot != null)
            helpRoot.SetActive(true);

        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(true);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Pause();

        foreach (var p in FindObjectsByType<PlayerController>())
            p.enabled = false;
    }

    private void Close()
    {
        IsOpen = false;
        if (helpRoot != null)
            helpRoot.SetActive(false);

        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(false);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Resume();

        foreach (var p in FindObjectsByType<PlayerController>())
            p.enabled = true;
    }
}
