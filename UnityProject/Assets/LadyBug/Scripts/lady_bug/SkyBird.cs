using UnityEngine;

namespace LadyBug
{


// Small schematic bird — two wing lines meeting at the center, each with a
// slight bow, plus flap, lateral drift, and gentle vertical bob.
[RequireComponent(typeof(LineRenderer))]
public class SkyBird : MonoBehaviour
{
    [SerializeField] private float driftSpeed = 4.5f;
    [SerializeField] private float destroyZ = -20f;
    [SerializeField] private float wingSpan = 2.0f;
    [SerializeField] private float wingFlapSpeed = 10f;
    [SerializeField] private float wingDropMin = -1.1f;
    [SerializeField] private float wingDropMax = 1.15f;
    [SerializeField] private float wingBow = 0.14f; // subtle mid-wing arch upward (0 = straight)
    [SerializeField] private int wingSegments = 8; // LineRenderer has no arc primitive — more points = smoother curve
    [SerializeField] private float bobAmplitude = 0.65f;
    [SerializeField] private float bobFrequency = 1.4f;

    LineRenderer _line;
    float _spawnZ;
    float _spawnY;
    float _phase;
    float _lateralSpeed;
    float _lineWidth;

    public void Configure(float lateralSpeed, float lineWidth, float span, float drift, float flapSpeed)
    {
        _lateralSpeed = lateralSpeed;
        _lineWidth = lineWidth;
        wingSpan = span;
        driftSpeed = drift;
        wingFlapSpeed = flapSpeed;

        if (_line != null)
        {
            _line.startWidth = _lineWidth;
            _line.endWidth = _lineWidth;
        }
    }

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = false;
        _line.positionCount = WingPointCount();
        _line.numCapVertices = 2;
        _line.numCornerVertices = 4;
        if (_lineWidth <= 0f)
            _lineWidth = 0.22f;
        if (wingSpan <= 0f)
            wingSpan = 2.0f;
        _line.startWidth = _lineWidth;
        _line.endWidth = _lineWidth;
    }

    void Start()
    {
        _spawnZ = transform.position.z;
        _spawnY = transform.position.y;
        _phase = Random.Range(0f, Mathf.PI * 2f);
        if (Mathf.Abs(_lateralSpeed) < 0.05f)
            _lateralSpeed = Random.Range(0.4f, 1.2f) * (Random.value < 0.5f ? -1f : 1f);

        float facing = _lateralSpeed >= 0f ? 1f : -1f;
        transform.localScale = new Vector3(facing, 1f, 1f);
    }

    void Update()
    {
        Vector3 pos = transform.position;
        pos.z -= driftSpeed * Time.deltaTime;
        pos.x += _lateralSpeed * Time.deltaTime;
        pos.y = _spawnY + Mathf.Sin(Time.time * bobFrequency + _phase) * bobAmplitude;
        transform.position = pos;

        float flap = (Mathf.Sin(Time.time * wingFlapSpeed + _phase) + 1f) * 0.5f;
        float wingDrop = Mathf.Lerp(wingDropMin, wingDropMax, flap);
        float halfSpan = wingSpan * 0.5f;
        int segments = Mathf.Max(2, wingSegments);
        int index = 0;

        for (int i = 0; i <= segments; i++)
        {
            float t = 1f - i / (float)segments;
            _line.SetPosition(index++, WingPoint(-1f, t, halfSpan, wingDrop, wingBow));
        }

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            _line.SetPosition(index++, WingPoint(1f, t, halfSpan, wingDrop, wingBow));
        }

        if (pos.z < destroyZ)
            Destroy(gameObject);
    }

    int WingPointCount() => Mathf.Max(2, wingSegments) * 2 + 1;

    // t = 0 at the body, t = 1 at the wing tip; side is ±1 for left/right.
    static Vector3 WingPoint(float side, float t, float halfSpan, float wingDrop, float bowAmount)
    {
        float x = side * t * halfSpan;
        float y = t * wingDrop;
        // Bow always arches upward — on the downstroke wingDrop is negative
        // and used to flip the bow downward; use |wingDrop| for bow only.
        float bow = Mathf.Sin(t * Mathf.PI) * Mathf.Abs(wingDrop) * bowAmount;
        return new Vector3(x, y + bow, 0f);
    }
}
}
