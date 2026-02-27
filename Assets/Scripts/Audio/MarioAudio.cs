using UnityEngine;

public class MarioAudio: MonoBehaviour
{
    [SerializeField] private AudioPlayer audioPlayer;

    [Header("Cues")]
    [SerializeField] private AudioCue jumpCue;
    [SerializeField] private AudioCue landCue;
    [SerializeField] private AudioCue attackCue;
    [SerializeField] private AudioCue deathCue;

    public void PlayJump() => audioPlayer.Play(jumpCue);
    public void PlayLand() => audioPlayer.Play(landCue);
    public void PlayAttack() => audioPlayer.Play(attackCue);
    public void PlayDeath() => audioPlayer.Play(deathCue);
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    //listens to the event. Once event is listened to, play the audio.
}
