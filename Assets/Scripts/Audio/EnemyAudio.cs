using UnityEngine;

public class EnemyAudio : MonoBehaviour
{
    [SerializeField] private AudioPlayer audioPlayer;
    [SerializeField] private AudioCue deathCue;

    public void PlayDeath()
    {
        audioPlayer.Play(deathCue);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
