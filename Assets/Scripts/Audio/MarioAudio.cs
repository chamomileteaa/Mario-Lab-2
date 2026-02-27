using UnityEngine;

public class MarioAudio: MonoBehaviour
{
    [SerializeField] private AudioPlayer audioPlayer;

    [Header("Cues")]
    [SerializeField] private AudioCue jumpCue;

    [SerializeField] private AudioCue growCue;
    
    [SerializeField] private AudioCue shrinkPipeCue;
    [SerializeField] private AudioCue LifeUpCue;
    public void PlayJump() => audioPlayer.Play(jumpCue);
    public void PlayGrow() => audioPlayer.Play(growCue);
    public void PlayShrink() => audioPlayer.Play(shrinkPipeCue);
    
    public void PlayLifeUp() => audioPlayer.Play(LifeUpCue);
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
