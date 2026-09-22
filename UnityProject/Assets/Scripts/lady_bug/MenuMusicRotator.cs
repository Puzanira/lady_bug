using UnityEngine;

// Cycles cheerful menu background tracks on one AudioSource — random pick
// each time a track ends, never repeating the same clip twice in a row.
public sealed class MenuMusicRotator : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] clips;

    // Optional: the track that always opens the playlist. Only the FIRST slot
    // is pinned — afterwards this clip is back in the random pool like any
    // other, so it can come round again. Leave empty (the start screen does)
    // and the very first pick is random too.
    //
    // "First" means once per scene load, not once per time the screen appears:
    // coming back from an aborted game intro carries on at random, while the
    // post-game scene reload starts a fresh attract cycle and so opens with
    // this track again.
    [SerializeField] private AudioClip firstClip;

    private int _lastIndex = -1;
    private bool _playing;
    private bool _playedFirstClip;

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
        int index = TakeFirstClipIndex();
        if (index < 0)
            index = PickRandomIndex();

        // Set either way, so the opener is also what the no-repeat rule
        // compares against — the second track can be anything but it.
        _lastIndex = index;
        source.clip = clips[index];
        source.Play();
    }

    // The pinned opener's index, or -1 if there is none, it already had its
    // turn, or it isn't in clips at all. Marks itself as spent even when the
    // clip is missing from the list, so a misconfigured reference costs one
    // lookup rather than one per track for the rest of the session.
    private int TakeFirstClipIndex()
    {
        if (_playedFirstClip || firstClip == null)
            return -1;

        _playedFirstClip = true;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == firstClip)
                return i;
        }

        return -1;
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
