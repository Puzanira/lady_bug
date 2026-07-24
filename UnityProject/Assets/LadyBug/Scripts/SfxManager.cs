using UnityEngine;

// Central one-shot sound-effect player. A single shared AudioSource with
// PlayOneShot lets overlapping effects (e.g. both players hitting something
// on the same frame) mix instead of cutting each other off.
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip dogClip;
    [SerializeField] private AudioClip catClip;
    [SerializeField] private AudioClip crowClip;
    [SerializeField] private AudioClip snakeClip;
    [SerializeField] private AudioClip hitGenericClip;
    [SerializeField] private AudioClip trickClip;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayPickup()
    {
        if (!GameStarted())
            return; // start-screen carousel force-spawns only good objects — no pickup chime under the menu
        Play(pickupClip);
    }

    // entityName: the colliding entity's GameObject name (prefab name, with
    // Unity's "(Clone)" suffix and all) — a couple of obstacles get their
    // own distinct sound, everything else shares one generic impact.
    public void PlayBad(string entityName)
    {
        if (!GameStarted())
            return;

        AudioClip clip = hitGenericClip;
        if (!string.IsNullOrEmpty(entityName))
        {
            if (entityName.StartsWith("Dog"))
                clip = dogClip;
            else if (entityName.StartsWith("Cat"))
                clip = catClip;
            else if (entityName.StartsWith("Crow"))
                clip = crowClip;
            else if (entityName.StartsWith("Snake"))
                clip = snakeClip;
        }
        Play(clip);
    }

    public void PlayTrick()
    {
        Play(trickClip);
    }

    private void Play(AudioClip clip)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip);
    }

    private static bool GameStarted()
    {
        return SpeedController.Instance != null && SpeedController.Instance.IsRunning;
    }
}
