using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioCue musicCue;

    void Start()
    {
        var source = GetComponent<AudioSource>();
        source.clip = musicCue.clip;
        source.loop = true;
        source.volume = musicCue.volume;
        source.Play();
    }
}
