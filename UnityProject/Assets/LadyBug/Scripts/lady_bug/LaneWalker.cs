using UnityEngine;

namespace LadyBug
{

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
    [SerializeField] private int laneCount = 1;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float minDelay = 1.5f;
    [SerializeField] private float maxDelay = 4f;
    [SerializeField] private float idleWiggleAngle = 4f;
    [SerializeField] private float idleWiggleSpeed = 3f;
    [SerializeField] private float crossingWiggleAngle = 10f;
    [SerializeField] private float crossingWiggleSpeed = 12f;
    // Chance to turn around instead of standing still when a move is blocked
    // by the edge of the road — see TryStartMove.
    [SerializeField] [Range(0f, 1f)] private float idleTurnChance = 0.35f;

    private int _lane;
    private float _timer;
    private bool _moving;
    private Transform _sprite;
    private float _baseSpriteScaleX;
    private bool _hasFrameAnimation;

    // Whether this creature is actively crossing into a neighbouring lane
    // right now. Used to pick the faster of the two placeholder wiggles; the
    // snake also used to read it to swap between a reared and a slithering
    // sprite, before it moved onto the shared frame animation like everyone else.
    public bool IsMoving => _moving;

    public void ConfigureLanes(int count, int? snapLane = null)
    {
        laneCount = Mathf.Max(1, count);
        laneWidth = RoadLayout.LaneWidthFor(laneCount);
        if (snapLane.HasValue)
            _lane = Mathf.Clamp(snapLane.Value, 0, laneCount - 1);
        else
            _lane = LaneFromWorldX(transform.position.x);
        Vector3 pos = transform.position;
        pos.x = RoadLayout.LaneCenterX(_lane, laneCount);
        transform.position = pos;
    }

    private int LaneFromWorldX(float worldX) =>
        Mathf.Clamp(Mathf.RoundToInt(worldX / laneWidth + (laneCount - 1) / 2f), 0, laneCount - 1);

    private void Awake()
    {
        laneWidth = RoadLayout.LaneWidthFor(laneCount);
    }

    private void Start()
    {
        _lane = LaneFromWorldX(transform.position.x);
        ScheduleNext();

        Transform found = transform.Find("Sprite");
        _sprite = found != null ? found : transform;
        // The source art (Dog/Cat/Crow/...) faces +X by default — flipping
        // this sign to match whichever way a move actually goes (see
        // TryStartMove) keeps the head leading instead of these creatures
        // sometimes walking backward into a lane.
        _baseSpriteScaleX = Mathf.Abs(_sprite.localScale.x);
        _hasFrameAnimation = GetComponentInChildren<SpriteFrameAnimator>() != null;

        // Facing was only ever set when a move began, so anything that had not
        // moved yet faced whichever way its source art happened to point —
        // every dog on the road looking the same way until it first crossed a
        // lane. Start each one facing at random instead.
        SetFacing(Random.value < 0.5f ? -1 : 1);
    }

    private void SetFacing(int direction)
    {
        _sprite.localScale = new Vector3(
            _baseSpriteScaleX * direction, _sprite.localScale.y, _sprite.localScale.z);
    }

    private void Update()
    {
        // The wiggle is a stand-in for animation. Anything with real frames
        // (SpriteFrameAnimator) animates itself, and rocking it as well just
        // makes it look like it is sliding on ice.
        if (!_hasFrameAnimation)
        {
            float wiggleAngle = _moving
                ? Mathf.Sin(Time.time * crossingWiggleSpeed) * crossingWiggleAngle
                : Mathf.Sin(Time.time * idleWiggleSpeed) * idleWiggleAngle;
            _sprite.localRotation = Quaternion.Euler(0f, 0f, wiggleAngle);
        }

        if (!_moving)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                TryStartMove();
            return;
        }

        float targetX = RoadLayout.LaneCenterX(_lane, laneCount);
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
            // Nowhere to go that way — it is already in the edge lane. Rather
            // than stand frozen facing the same way for another whole wait,
            // sometimes just turn on the spot, as if it looked that way and
            // thought better of it.
            if (Random.value < idleTurnChance)
                SetFacing(direction);
            ScheduleNext();
            return;
        }

        _lane = targetLane;
        _moving = true;
        SetFacing(direction);
    }

    private void ScheduleNext()
    {
        _timer = Random.Range(minDelay, maxDelay);
    }
}
}
