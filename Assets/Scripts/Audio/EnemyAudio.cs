using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAudio : AudioPlayer
{
    [SerializeField] private AudioClip deathClip;

    public void PlayDeath()
    {
        PlayOneShot(deathClip);
    }
}
