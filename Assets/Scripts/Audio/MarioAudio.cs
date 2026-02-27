using UnityEngine;

public class MarioAudio: MonoBehaviour
{
    [SerializeField] private AudioPlayer audioPlayer;

    [Header("Cues")]
    [SerializeField] private AudioCue jumpCue;
    public void PlayJump() => audioPlayer.Play(jumpCue);
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
