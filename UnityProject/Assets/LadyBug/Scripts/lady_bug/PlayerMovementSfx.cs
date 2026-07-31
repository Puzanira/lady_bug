using UnityEngine;

// Per-player looping movement sound: running feet while grounded (normal or
// ducking), wing buzz while airborne (jumping/bouncing) — both sources loop
// continuously and are just volume-gated by state, so swapping between them
// never clicks/restarts mid-loop.
public class PlayerMovementSfx : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private AudioSource feetSource;
    [SerializeField] private AudioSource wingsSource;
    [SerializeField] private float volume = 0.5f;

    private void Update()
    {
        if (player == null || feetSource == null || wingsSource == null)
            return;

        if (!player.enabled)
        {
            feetSource.volume = 0f;
            wingsSource.volume = 0f;
            return;
        }

        bool airborne = player.IsAirborne;
        feetSource.volume = airborne ? 0f : volume;
        wingsSource.volume = airborne ? volume : 0f;
    }
}
