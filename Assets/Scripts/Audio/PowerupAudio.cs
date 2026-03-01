using UnityEngine;

[DisallowMultipleComponent]
public class PowerupAudio : AudioPlayer
{
    [SerializeField] private AudioClip spawnClip;

    private void Awake()
    {
        Source.playOnAwake = false;
    }

    public void PlaySpawn()
    {
        PlayOneShot(spawnClip);
    }
}
