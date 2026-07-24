using UnityEngine;

// Flies a "+1"/"-1" popup toward the score counter, then applies the score
// and destroys itself. Both this object and the target share the same
// (0,0)-anchored, (0,0)-pivot parent, so anchoredPosition is directly
// comparable between them.
public class ScorePopup : MonoBehaviour
{
    public int value;
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
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(value);
            Destroy(gameObject);
        }
    }
}
