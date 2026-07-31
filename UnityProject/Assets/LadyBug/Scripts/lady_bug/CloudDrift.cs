using UnityEngine;

// Ambient sky element — drifts toward the camera at its own fixed slow pace,
// independent of SpeedController, so clouds keep sailing by even before the
// game starts (unlike MovingEntity, which stops when road speed is zero).
// Also grows and lifts a little as it approaches — at these distances plain
// perspective alone barely registers, so the world-space scale/height ramp
// up too, for a much more noticeable "drifting closer" effect.
public class CloudDrift : MonoBehaviour
{
    [SerializeField] private float speed = 3f;
    [SerializeField] private float destroyZ = -20f;
    [SerializeField] private float growthFactor = 4f; // scale multiplier by the time it reaches destroyZ
    [SerializeField] private float riseHeight = 4f; // extra height gained by the time it reaches destroyZ

    private float _spawnZ;
    private float _spawnY;
    private Vector3 _baseScale;

    private void Start()
    {
        _spawnZ = transform.position.z;
        _spawnY = transform.position.y;
        _baseScale = transform.localScale;
    }

    private void Update()
    {
        Vector3 pos = transform.position;
        pos.z -= speed * Time.deltaTime;

        float t = Mathf.InverseLerp(_spawnZ, destroyZ, pos.z);
        pos.y = _spawnY + Mathf.Lerp(0f, riseHeight, t);
        transform.position = pos;

        transform.localScale = _baseScale * Mathf.Lerp(1f, growthFactor, t);

        if (pos.z < destroyZ)
            Destroy(gameObject);
    }
}
