using UnityEngine;

[DisallowMultipleComponent]
public class MusicPlayer : MonoBehaviour
{ 
    public enum MusicTheme
    {
        Overworld = 0,
        Underground = 1,
        Starman = 2,
        Death = 3
    }

    [SerializeField] private SerializedEnumDictionary<MusicTheme, AudioClip> themes = new SerializedEnumDictionary<MusicTheme, AudioClip>();
    [SerializeField] private MusicTheme activeLevelTheme = MusicTheme.Overworld;
    [SerializeField, Min(0.01f)] private float scheduleLeadTime = 0.05f;

    private const string SourceAName = "MusicSourceA";
    private const string SourceBName = "MusicSourceB";

    private MarioController mario;
    private AudioPlayer[] players;
    private AudioClip currentClip;
    private MusicTheme currentTheme;
    private int activeSourceIndex;
    private bool marioSubscribed;

    private MarioController Mario => mario ? mario : mario = FindFirstObjectByType<MarioController>(FindObjectsInactive.Include);
    private AudioPlayer[] Players => players ??= InitializePlayers();

    private void OnEnable()
    {
        TrySubscribeMario();
    }

    private void Start()
    {
        TrySubscribeMario();
    }

    private void OnDisable()
    {
        UnsubscribeMario();
    }

    public void PlayOverworldTheme()
    {
        activeLevelTheme = MusicTheme.Overworld;
        PlayTheme(activeLevelTheme);
    }

    public void PlayUndergroundTheme()
    {
        activeLevelTheme = MusicTheme.Underground;
        PlayTheme(activeLevelTheme);
    }

    public void PlayStarmanTheme() => PlayTheme(MusicTheme.Starman, true);
    public void PlayDeathTheme() => PlayTheme(MusicTheme.Death, true);

    public bool PlayTheme(MusicTheme theme, bool restartIfPlaying = false)
    {
        if (!themes.TryGetValue(theme, out var clip) || !clip)
            return false;

        PlayCue(theme, clip, restartIfPlaying);
        return true;
    }

    public void PlayCue(MusicTheme theme, AudioClip clip, bool restartIfPlaying = false)
    {
        if (!clip) return;
        if (!restartIfPlaying && currentClip == clip && currentTheme == theme && IsAnySourcePlaying()) return;

        currentClip = clip;
        currentTheme = theme;

        var startTime = AudioSettings.dspTime + scheduleLeadTime;
        ScheduleCue(clip, startTime, IsLoopingTheme(theme));
    }

    public void StopMusic()
    {
        var localPlayers = Players;
        for (var i = 0; i < localPlayers.Length; i++)
        {
            localPlayers[i].Stop();
            localPlayers[i].Source.pitch = 1f;
            localPlayers[i].Source.volume = 1f;
        }

        currentClip = null;
    }

    private void OnMarioSpawned()
    {
        if (Mario && Mario.IsStarPowered)
            PlayStarmanTheme();
        else
            PlayTheme(activeLevelTheme);
    }

    private void OnStarPowerChanged(bool active)
    {
        if (active) PlayStarmanTheme();
        else PlayTheme(activeLevelTheme);
    }

    private void OnMarioDied()
    {
        PlayDeathTheme();
    }

    private void TrySubscribeMario()
    {
        if (marioSubscribed) return;
        if (!Mario) return;

        Mario.Spawned += OnMarioSpawned;
        Mario.StarPowerChanged += OnStarPowerChanged;
        Mario.Died += OnMarioDied;
        marioSubscribed = true;
    }

    private void UnsubscribeMario()
    {
        if (!marioSubscribed) return;
        if (!Mario) return;

        Mario.Spawned -= OnMarioSpawned;
        Mario.StarPowerChanged -= OnStarPowerChanged;
        Mario.Died -= OnMarioDied;
        marioSubscribed = false;
    }

    private void ScheduleCue(AudioClip clip, double startDspTime, bool loop)
    {
        var nextSourceIndex = 1 - activeSourceIndex;
        var nextPlayer = Players[nextSourceIndex];
        var previousPlayer = Players[activeSourceIndex];

        nextPlayer.PlayScheduled(clip, startDspTime, 1f, loop, 1f);
        previousPlayer.SetScheduledEndTime(startDspTime);
        activeSourceIndex = nextSourceIndex;
    }

    private bool IsAnySourcePlaying()
    {
        var localPlayers = Players;
        for (var i = 0; i < localPlayers.Length; i++)
        {
            if (localPlayers[i].Source.isPlaying) return true;
        }

        return false;
    }

    private static bool IsLoopingTheme(MusicTheme theme)
    {
        return theme != MusicTheme.Death;
    }

    private AudioPlayer[] InitializePlayers()
    {
        var first = GetOrCreatePlayer(SourceAName);
        var second = GetOrCreatePlayer(SourceBName);
        return new[] { first, second };
    }

    private AudioPlayer GetOrCreatePlayer(string sourceName)
    {
        var child = transform.Find(sourceName);
        if (!child)
        {
            var childObject = new GameObject(sourceName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        var player = child.GetComponent<AudioPlayer>();
        if (!player) player = child.gameObject.AddComponent<AudioPlayer>();

        var source = player.Source;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return player;
    }
}
