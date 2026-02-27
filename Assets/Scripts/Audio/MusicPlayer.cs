using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioCue musicCue;
    [SerializeField] private AudioCue invincibilityCue;
    [SerializeField] private AudioCue deathCue;
    
    [SerializeField] private AudioPlayer audioPlayer;
    
    public void PlayGroundTheme() => audioPlayer.PlayExclusive(musicCue);
    
    public void PlayInvincibilityCue() => audioPlayer.PlayExclusive(invincibilityCue);
    
    public void PlayDeathCue() => audioPlayer.PlayExclusive(deathCue);
}
