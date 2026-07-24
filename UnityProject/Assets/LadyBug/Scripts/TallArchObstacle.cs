using UnityEngine;

// Marker: obstacles carrying this component span the full road (all lanes)
// and are safe to pass under while grounded or ducking — only being airborne
// (jumping/bouncing) when reaching it counts as a hit. Inverse of
// DuckUnderObstacle, which is safe only while ducking.
public class TallArchObstacle : MonoBehaviour
{
}
