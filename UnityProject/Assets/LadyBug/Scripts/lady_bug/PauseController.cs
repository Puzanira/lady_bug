using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LadyBug
{

// Yes/No "quit game?" dialog — opened by DuckToExitController (all players
// duck-hold, or the cabinet's exit button) or HelpController (Q on the help
// screen). Answered either by picking a side and confirming (lean + jump, or
// the arrow keys), or outright with the cabinet panel's green/red buttons.
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private Text yesText;
    [SerializeField] private Text noText;

    private static readonly Color Highlighted = new Color(1f, 0.85f, 0.2f);

    private bool _dialogOpen;
    private bool _confirmYes;
    private PlayerController[] _players;
    private int _openedFrame = -1;

    public bool IsDialogOpen => _dialogOpen;

    private void Awake()
    {
        Instance = this;
        if (dialogRoot != null)
            dialogRoot.SetActive(false);
    }

    private void Update()
    {
        if (_dialogOpen)
            HandleDialogInput();
    }

    public void OpenDialog()
    {
        _dialogOpen = true;
        _confirmYes = false;
        _openedFrame = Time.frameCount;
        // Repack: FindObjectsByType, not the obsolete FindObjectsOfType — the hub
        // builds the whole cabinet with warnings-as-noise budget of zero.
        _players = FindObjectsByType<PlayerController>();

        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(true);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Pause();

        foreach (var p in _players)
            p.enabled = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(true);
        UpdateDialogVisuals();
    }

    private void HandleDialogInput()
    {
        bool left = false;
        bool right = false;
        bool confirm = false;

        if (_players != null)
        {
            foreach (PlayerController p in _players)
            {
                if (p == null || !p.gameObject.activeInHierarchy)
                    continue;
                left |= p.ReadLeanLeftDown();
                right |= p.ReadLeanRightDown();
                confirm |= p.ReadJumpDown();
            }
        }

        // The cabinet panel answers the question outright, no navigating to an
        // option first: green is ДА, red is НЕТ — the same NO/YES the panel's
        // own firmware assigns those two buttons. The lean-and-jump path above
        // stays exactly as it was, for the keyboard and the gesture sensors.
        // Not on the frame the dialog opened: red is also what OPENS it from
        // inside the run (DuckToExitController), and that same one-frame edge
        // would otherwise be read here as НЕТ and shut the dialog again
        // instantly. Update order between the two components isn't fixed, so
        // the guard is on the frame number rather than on who runs first.
        JoystickSerial panel = Time.frameCount != _openedFrame ? JoystickSerial.Instance : null;
        bool panelYes = panel != null && panel.GreenButtonDown;
        bool panelNo = panel != null && panel.RedButtonDown;

        if (panelYes || panelNo)
        {
            // Move the highlight to the answer being given before acting on
            // it, so the screen shows what was chosen rather than the run
            // vanishing with НЕТ still lit.
            _confirmYes = panelYes;
            UpdateDialogVisuals();
        }
        else if (left || right)
        {
            _confirmYes = !_confirmYes;
            UpdateDialogVisuals();
        }

        if (confirm || panelYes || panelNo)
        {
            if (_confirmYes)
            {
                if (SpeedController.Instance != null)
                    SpeedController.Instance.ResetForMenu();
                // Reload by buildIndex, not name: in the arcade-hub build lady_bug's
                // entry scene and Sisyphus's are BOTH named "Main", so LoadScene(name)
                // resolves to the first "Main" in Build Settings (Sisyphus) and
                // launches the wrong game. buildIndex is collision-proof.
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            else
                CloseDialog();
        }
    }

    private void CloseDialog()
    {
        _dialogOpen = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(false);
        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(false);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Resume();

        foreach (var p in FindObjectsByType<PlayerController>())
            p.enabled = true;
    }

    private void UpdateDialogVisuals()
    {
        if (yesText != null)
            yesText.color = _confirmYes ? Highlighted : Color.white;
        if (noText != null)
            noText.color = _confirmYes ? Color.white : Highlighted;
    }
}
}
