using UnityEngine;
[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    public AudioSource source;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    public void Play(AudioCue cue)
    {
        if (cue == null || cue.clip == null) return;

        source.pitch = cue.randomPitch
            ? Random.Range(cue.pitchRange.x, cue.pitchRange.y)
            : 1f;

        source.PlayOneShot(cue.clip, cue.volume);
    }
    
    public void PlayExclusive(AudioCue cue)
    {
        source.Stop();
        source.clip = cue.clip;
        source.loop = true;
        source.Play();
    }
}