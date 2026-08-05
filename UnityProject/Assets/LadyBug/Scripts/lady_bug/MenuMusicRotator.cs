using UnityEngine;

namespace LadyBug
{


// Cycles cheerful menu background tracks on one AudioSource — random pick
// each time a track ends, never repeating the same clip twice in a row.
public sealed class MenuMusicRotator : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] clips;

    private int _lastIndex = -1;
    private bool _playing;

    public void Play()
    {
        if (source == null || clips == null || clips.Length == 0)
            return;

        _playing = true;
        source.loop = false;
        if (!source.isPlaying)
            PlayNext();
    }

    public void StopRotating()
    {
        _playing = false;
        if (source != null)
            source.Stop();
    }

    private void Update()
    {
        if (!_playing || source == null || clips == null || clips.Length == 0)
            return;

        if (!source.isPlaying)
            PlayNext();
    }

    private void PlayNext()
    {
        int index = PickRandomIndex();
        _lastIndex = index;
        source.clip = clips[index];
        source.Play();
    }

    private int PickRandomIndex()
    {
        if (clips.Length == 1)
            return 0;

        int index;
        do
            index = Random.Range(0, clips.Length);
        while (index == _lastIndex);
        return index;
    }
}
}
