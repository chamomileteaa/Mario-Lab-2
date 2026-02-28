using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GoombaController))]
public class GoombaAudio : AudioPlayer
{
    public enum GoombaSfxType
    {
        Spawn = 0,
        StompDefeat = 1,
        KnockbackDefeat = 2
    }

    [SerializeField] private SerializedEnumDictionary<GoombaSfxType, AudioClip> cues = new SerializedEnumDictionary<GoombaSfxType, AudioClip>();

    private void Awake()
    {
        _ = Source;
    }

    public void Play(GoombaSfxType type)
    {
        if (!cues.TryGetValue(type, out var clip) || !clip) return;
        PlayOneShot(clip);
    }

    public void PlaySpawn() => Play(GoombaSfxType.Spawn);
    public void PlayStompDefeat() => Play(GoombaSfxType.StompDefeat);
    public void PlayKnockbackDefeat() => Play(GoombaSfxType.KnockbackDefeat);

}
