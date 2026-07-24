using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LadyBug
{

// Yes/No "quit game?" dialog — opened by HelpController when Q is pressed
// while the F1 help screen is up (braking was removed from every control
// scheme, so the old "hold brake for 5s" trigger no longer has a key to
// hang off of; routing it through the help screen keeps a quit path
// discoverable without adding a dedicated always-on key).
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private Text yesText;
    [SerializeField] private Text noText;

    private static readonly Color Highlighted = new Color(1f, 0.85f, 0.2f);

    private bool _dialogOpen;
    private bool _confirmYes;

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

        if (SpeedController.Instance != null)
            SpeedController.Instance.SetPaused(true);
        if (GameTimer.Instance != null)
            GameTimer.Instance.Pause();

        foreach (var p in FindObjectsByType<PlayerController>())
            p.enabled = false;

        if (dialogRoot != null)
            dialogRoot.SetActive(true);
        UpdateDialogVisuals();
    }

    private void HandleDialogInput()
    {
        bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);

        if (left || right)
        {
            _confirmYes = !_confirmYes;
            UpdateDialogVisuals();
        }

        if (confirm)
        {
            if (_confirmYes)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
