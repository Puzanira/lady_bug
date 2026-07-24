using UnityEngine;

// Keeps a flat shadow directly beneath a target on the road surface — X/Z
// track the target, Y stays pinned to the road regardless of how high the
// target currently is (jumping, ducking, bouncing), so its lane position and
// jump timing are easy to judge at a glance. Used for the players, whose
// height changes at runtime — static obstacles just bake a child shadow in
// at build time instead, since their height never changes.
public class GroundShadow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float groundY = 0.02f;
    // Shrinks the shadow a bit as the target rises above its own resting
    // height (jump/bounce) — a small "further from the ground" cue, not
    // just a same-size patch sliding along underneath at any height.
    [SerializeField] private float heightShrinkFactor = 0.18f;
    [SerializeField] private float minScale = 0.55f;

    private float _restY;
    private Vector3 _baseScale;
    private bool _initialized;

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (!_initialized)
        {
            _restY = target.position.y;
            _baseScale = transform.localScale;
            _initialized = true;
        }

        Vector3 pos = target.position;
        pos.y = groundY;
        transform.position = pos;
        // The shadow is parented to the player, so it otherwise inherits
        // their own rotation too — including the lane-change lean
        // (PlayerController's laneTiltAngle). A flat disc tilted off the
        // horizontal plane foreshortens hard from this camera angle and can
        // read as sliced down to a half-circle; a ground shadow should stay
        // flat on the road regardless of how the target above it is leaning.
        transform.rotation = Quaternion.identity;

        float height = Mathf.Max(0f, target.position.y - _restY);
        float scale = Mathf.Max(minScale, 1f - height * heightShrinkFactor);
        transform.localScale = _baseScale * scale;
    }
}
