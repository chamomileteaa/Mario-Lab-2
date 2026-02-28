using UnityEngine;

[DisallowMultipleComponent]
public class KoopaTroopaAudio : AudioPlayer
{
    public enum KoopaTroopaSfxType
    {
        Spawn = 0,
        Stomp = 1,
        ShellKick = 2,
        ShellBump = 3,
        Defeat = 4
    }

    [SerializeField] private SerializedEnumDictionary<KoopaTroopaSfxType, AudioClip> cues = new SerializedEnumDictionary<KoopaTroopaSfxType, AudioClip>();

    private void Awake()
    {
        _ = Source;
    }

    public void Play(KoopaTroopaSfxType type)
    {
        if (!cues.TryGetValue(type, out var clip) || !clip) return;
        PlayOneShot(clip);
    }

    public void PlaySpawn() => Play(KoopaTroopaSfxType.Spawn);
    public void PlayStomp() => Play(KoopaTroopaSfxType.Stomp);
    public void PlayShellKick() => Play(KoopaTroopaSfxType.ShellKick);
    public void PlayShellBump() => Play(KoopaTroopaSfxType.ShellBump);
    public void PlayDefeat() => Play(KoopaTroopaSfxType.Defeat);

}
