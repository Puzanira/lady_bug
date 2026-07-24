using UnityEngine;

// Marker: obstacles carrying this component don't count as a hit while the
// player is ducking (e.g. a low arch/beam meant to be passed under).
public class DuckUnderObstacle : MonoBehaviour
{
    // Set once a co-op pass (one ducking, one riding over) has scored its
    // freestyle trick point, so it can't be awarded twice for the same arch.
    public bool TrickAwarded;
}
