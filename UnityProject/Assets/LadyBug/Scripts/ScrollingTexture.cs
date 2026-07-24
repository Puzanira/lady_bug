using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    [SerializeField] private float dashPeriod = 4f;

    private Renderer _renderer;
    private Vector2 _offset;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 10f;
        _offset.y -= (speed / dashPeriod) * Time.deltaTime;
        _renderer.material.mainTextureOffset = _offset;
    }
}
