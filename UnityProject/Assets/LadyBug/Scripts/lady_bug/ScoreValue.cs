using UnityEngine;

// Marks an entity as scoring: positive value = good pickup (no crash),
// negative value = bad obstacle (crashes the player too).
public class ScoreValue : MonoBehaviour
{
    public int value = 1;
}
