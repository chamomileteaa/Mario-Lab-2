using UnityEngine;

public class EnemyAudio : MonoBehaviour
{
    [SerializeField] private AudioPlayer audioPlayer;
    [SerializeField] private AudioCue deathCue;

    public void Awake()
    {
        audioPlayer = GetComponent<AudioPlayer>();
    }
    public void PlayDeath()
    {
        if (!audioPlayer || !deathCue) return;
        audioPlayer.Play(deathCue);
        Debug.Log("Play Death Enemy");
    }
}
