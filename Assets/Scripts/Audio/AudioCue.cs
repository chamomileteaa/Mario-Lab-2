using UnityEngine;

[CreateAssetMenu(fileName = "AudioCue", menuName = "Audio/Cue")]
public class AudioCue : ScriptableObject
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    //This bottom part isnt really important btw 
    public bool randomPitch = true;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
}
