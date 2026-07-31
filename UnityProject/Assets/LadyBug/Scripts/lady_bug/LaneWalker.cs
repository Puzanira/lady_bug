using UnityEngine;

// A few living bad objects (dog/cat/crow) drift sideways between lanes at
// random — eases from the current lane's centre toward a random neighbour's
// centre, which naturally passes through both lanes' edges along the way,
// then waits before picking another lane. Purely lateral (X); MovingEntity
// still handles the forward (Z) scroll.
//
// Also doubles as this object's animation: a slow idle wiggle while sitting
// in a lane (standing in for "dog scratches an ear" / "bird pecks" — a
// literal per-species animation would need several consistent hand-picked
// frames, which independent AI generations can't reliably match each
// other's style/proportions for), and a faster, more obvious wiggle while
// actively crossing into a neighbouring lane.
public class LaneWalker : MonoBehaviour
{
    [SerializeField] private float laneWidth = 4f;
    [SerializeField] private int laneCount = 3;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float minDelay = 1.5f;
    [SerializeField] private float maxDelay = 4f;
    [SerializeField] private float idleWiggleAngle = 4f;
    [SerializeField] private float idleWiggleSpeed = 3f;
    [SerializeField] private float crossingWiggleAngle = 10f;
    [SerializeField] private float crossingWiggleSpeed = 12f;

    private int _lane;
    private float _timer;
    private bool _moving;
    private Transform _sprite;
    private float _baseSpriteScaleX;

    // Whether this creature is actively crossing into a neighbouring lane
    // right now — read by SnakePose to pick between its idle/moving sprite.
    public bool IsMoving => _moving;

    private void Start()
    {
        _lane = Mathf.RoundToInt(transform.position.x / laneWidth + (laneCount - 1) / 2f);
        ScheduleNext();

        Transform found = transform.Find("Sprite");
        _sprite = found != null ? found : transform;
        // The source art (Dog/Cat/Crow/...) faces +X by default — flipping
        // this sign to match whichever way a move actually goes (see
        // TryStartMove) keeps the head leading instead of these creatures
        // sometimes walking backward into a lane.
        _baseSpriteScaleX = Mathf.Abs(_sprite.localScale.x);
    }

    private void Update()
    {
        float wiggleAngle = _moving
            ? Mathf.Sin(Time.time * crossingWiggleSpeed) * crossingWiggleAngle
            : Mathf.Sin(Time.time * idleWiggleSpeed) * idleWiggleAngle;
        _sprite.localRotation = Quaternion.Euler(0f, 0f, wiggleAngle);

        if (!_moving)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                TryStartMove();
            return;
        }

        float targetX = (_lane - (laneCount - 1) / 2f) * laneWidth;
        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX, moveSpeed * Time.deltaTime);
        transform.position = pos;

        if (Mathf.Approximately(pos.x, targetX))
        {
            _moving = false;
            ScheduleNext();
        }
    }

    private void TryStartMove()
    {
        int direction = Random.value < 0.5f ? -1 : 1;
        int targetLane = _lane + direction;
        if (targetLane < 0 || targetLane >= laneCount)
        {
            ScheduleNext(); // already at that edge lane — try again later
            return;
        }

        _lane = targetLane;
        _moving = true;
        _sprite.localScale = new Vector3(_baseSpriteScaleX * direction, _sprite.localScale.y, _sprite.localScale.z);
    }

    private void ScheduleNext()
    {
        _timer = Random.Range(minDelay, maxDelay);
    }
}
