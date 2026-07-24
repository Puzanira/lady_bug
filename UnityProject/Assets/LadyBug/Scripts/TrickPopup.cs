using UnityEngine;

// Flies a "+1 ТРЮК: <name>" popup toward the tricks counter, then applies the
// trick and destroys itself. Mirrors ScorePopup's fly-then-apply pattern —
// both this object and the target share the same (0,0)-anchored,
// (0,0)-pivot parent, so anchoredPosition is directly comparable.
public class TrickPopup : MonoBehaviour
{
    public RectTransform target;

    [SerializeField] private float duration = 1.1f;

    private RectTransform _rect;
    private Vector2 _startPos;
    private float _timer;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _startPos = _rect.anchoredPosition;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / duration);
        float eased = t * t;

        Vector2 targetPos = target != null ? target.anchoredPosition : _startPos;
        _rect.anchoredPosition = Vector2.Lerp(_startPos, targetPos, eased);
        _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);

        if (t >= 1f)
        {
            if (TricksManager.Instance != null)
                TricksManager.Instance.AddTrick();
            Destroy(gameObject);
        }
    }
}
