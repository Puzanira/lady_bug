using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LadyBug
{

// Yes/No "quit game?" dialog — opened by DuckToExitController (all players
// duck-hold) or HelpController (Q on the help screen).
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

        if (left || right)
        {
            _confirmYes = !_confirmYes;
            UpdateDialogVisuals();
        }

        if (confirm)
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
