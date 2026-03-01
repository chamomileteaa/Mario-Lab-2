using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MusicPlayer : MonoBehaviour
{ 
    public enum MusicTheme
    {
        Overworld = 0,
        OverworldHurried = 1,
        Underground = 2,
        UndergroundHurried = 3,
        Underwater = 4,
        UnderwaterHurried = 5,
        Castle = 6,
        CastleHurried = 7,
        Starman = 8,
        StarmanHurried = 9,
        Death = 10,
        WorldClear = 11,
        StageClear = 12,
        NameEntry = 13,
        SavedPrincess = 14,
        GameOver = 15
    }

    [SerializeField] private SerializedEnumDictionary<MusicTheme, AudioClip> themes = new SerializedEnumDictionary<MusicTheme, AudioClip>();
    [SerializeField] private MusicTheme activeLevelTheme = MusicTheme.Overworld;
    [SerializeField] private AudioClip hurryUpSfx;
    [SerializeField, Min(1f)] private float hurryTimeThreshold = 100f;
    [SerializeField, Min(0.01f)] private float scheduleLeadTime = 0.05f;
    [SerializeField] private bool preloadThemeAudioData = true;
    [SerializeField] private AudioSource sourceA;
    [SerializeField] private AudioSource sourceB;

    private MarioController mario;
    private GameData gameData;
    private CameraController cameraController;
    private AudioSource[] sources;
    private AudioClip currentClip;
    private MusicTheme currentTheme;
    private int activeSourceIndex;
    private bool marioSubscribed;
    private bool hurryTriggered;
    private bool cameraSubscribed;
    private bool hasLastEnvironment;
    private CameraEnvironmentType lastEnvironment;

    private MarioController Mario => mario ? mario : mario = FindFirstObjectByType<MarioController>(FindObjectsInactive.Include);
    private GameData Data => gameData ? gameData : gameData = GameData.GetOrCreate();
    private CameraController CameraController => cameraController ? cameraController : cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
    private AudioSource[] Sources => sources ??= ResolveSources();

    private void OnEnable()
    {
        PrewarmAudioData();
        TrySubscribeMario();
        TrySubscribeCamera();
        SceneManager.sceneLoaded += OnSceneLoaded;

        var data = Data;
        if (data && data.runActive)
            OnGameplayRunStarted();
    }

    private void Update()
    {
        TrySubscribeCamera();
        PollEnvironmentChanges();

        var data = Data;
        if (data && data.runActive && currentTheme == MusicTheme.NameEntry)
            OnGameplayRunStarted();

        TryTriggerHurryState();
    }

    private void OnDisable()
    {
        UnsubscribeMario();
        UnsubscribeCamera();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void PlayOverworldTheme() => SetLevelTheme(MusicTheme.Overworld);
    public void PlayUnderwaterTheme() => SetLevelTheme(MusicTheme.Underwater);
    public void PlayCastleTheme() => SetLevelTheme(MusicTheme.Castle);
    public void PlayUndergroundTheme() => SetLevelTheme(MusicTheme.Underground);

    public void PlayStarmanTheme() => PlayTheme(GetStarmanVariant(), true);
    public void PlayDeathTheme() => PlayTheme(MusicTheme.Death, true);
    public void PlayGameOverTheme() => PlayTheme(MusicTheme.GameOver, true);
    public void PlayWorldClearTheme() => PlayTheme(MusicTheme.WorldClear, true);
    public void PlayStageClearTheme() => PlayTheme(MusicTheme.StageClear, true);
    public void PlayNameEntryTheme() => PlayTheme(MusicTheme.NameEntry, true);
    public void PlaySavedPrincessTheme() => PlayTheme(MusicTheme.SavedPrincess, true);
    
    public void OnGameplayRunStarted()
    {
        hurryTriggered = false;
        TrySubscribeCamera();
        var controller = CameraController;
        var environment = controller ? controller.ActiveEnvironment : CameraEnvironmentType.Overworld;
        ApplyEnvironmentTheme(environment, true);
    }

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
        var localSources = Sources;
        for (var i = 0; i < localSources.Length; i++)
        {
            var source = localSources[i];
            if (!source) continue;
            source.Stop();
            source.loop = false;
            source.clip = null;
            source.pitch = 1f;
            source.volume = 1f;
        }

        currentClip = null;
    }

    private void OnMarioSpawned()
    {
        hurryTriggered = false;
        if (Mario && Mario.IsStarPowered)
            PlayStarmanTheme();
        else
            PlayCurrentLevelTheme(true);
    }

    private void OnStarPowerChanged(bool active)
    {
        if (active) PlayStarmanTheme();
        else PlayCurrentLevelTheme(true);
    }

    private void TrySubscribeMario()
    {
        if (marioSubscribed) return;
        if (!Mario) return;

        Mario.Spawned += OnMarioSpawned;
        Mario.StarPowerChanged += OnStarPowerChanged;
        marioSubscribed = true;
    }

    private void UnsubscribeMario()
    {
        if (!marioSubscribed) return;
        if (!Mario) return;

        Mario.Spawned -= OnMarioSpawned;
        Mario.StarPowerChanged -= OnStarPowerChanged;
        marioSubscribed = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cameraController = null;
        cameraSubscribed = false;
        hasLastEnvironment = false;
        TrySubscribeCamera();

        var data = Data;
        if (data && data.runActive)
            OnGameplayRunStarted();
    }

    private void TrySubscribeCamera()
    {
        if (cameraSubscribed) return;
        var controller = CameraController;
        if (!controller) return;
        controller.ActiveEnvironmentChanged += OnActiveEnvironmentChanged;
        cameraSubscribed = true;
        ApplyEnvironmentTheme(controller.ActiveEnvironment, false);
    }

    private void UnsubscribeCamera()
    {
        var controller = CameraController;
        if (!controller) return;
        controller.ActiveEnvironmentChanged -= OnActiveEnvironmentChanged;
        cameraSubscribed = false;
        hasLastEnvironment = false;
    }

    private void OnActiveEnvironmentChanged(CameraEnvironmentType environment)
    {
        ApplyEnvironmentTheme(environment, true);
    }

    private void PollEnvironmentChanges()
    {
        var controller = CameraController;
        if (!controller) return;

        var environment = controller.ActiveEnvironment;
        if (hasLastEnvironment && environment == lastEnvironment) return;
        ApplyEnvironmentTheme(environment, true);
    }

    private void ScheduleCue(AudioClip clip, double startDspTime, bool loop)
    {
        var nextSourceIndex = 1 - activeSourceIndex;
        var localSources = Sources;
        var nextSource = localSources[nextSourceIndex];
        var previousSource = localSources[activeSourceIndex];

        if (!nextSource) return;

        nextSource.clip = clip;
        nextSource.volume = 1f;
        nextSource.pitch = 1f;
        nextSource.loop = loop;
        nextSource.PlayScheduled(startDspTime);

        if (previousSource && previousSource.isPlaying)
            previousSource.SetScheduledEndTime(startDspTime);

        activeSourceIndex = nextSourceIndex;
    }

    private bool IsAnySourcePlaying()
    {
        foreach (var source in Sources)
            if (source && source.isPlaying) return true;
        return false;
    }

    private static bool IsLoopingTheme(MusicTheme theme)
    {
        return theme is MusicTheme.Overworld or
            MusicTheme.OverworldHurried or
            MusicTheme.Underground or
            MusicTheme.UndergroundHurried or
            MusicTheme.Underwater or
            MusicTheme.UnderwaterHurried or
            MusicTheme.Castle or
            MusicTheme.CastleHurried or
            MusicTheme.Starman or
            MusicTheme.StarmanHurried or
            MusicTheme.NameEntry;
    }

    private AudioSource[] ResolveSources()
    {
        if (sourceA && sourceB)
            return new[] { ConfigureSource(sourceA), ConfigureSource(sourceB) };

        var childSources = GetComponentsInChildren<AudioSource>(true);
        if (!sourceA && childSources.Length > 0) sourceA = childSources[0];
        if (!sourceB && childSources.Length > 1) sourceB = childSources[1];

        if (!sourceA)
        {
            var child = new GameObject("MusicSourceA");
            child.transform.SetParent(transform, false);
            sourceA = child.AddComponent<AudioSource>();
        }

        if (!sourceB)
        {
            var child = new GameObject("MusicSourceB");
            child.transform.SetParent(transform, false);
            sourceB = child.AddComponent<AudioSource>();
        }

        return new[] { ConfigureSource(sourceA), ConfigureSource(sourceB) };
    }

    private static AudioSource ConfigureSource(AudioSource source)
    {
        if (!source) return null;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void PlayCurrentLevelTheme(bool restartIfPlaying)
    {
        var themeToPlay = hurryTriggered ? GetHurriedVariant(activeLevelTheme) : NormalizeLevelTheme(activeLevelTheme);
        PlayTheme(themeToPlay, restartIfPlaying);
    }

    private void SetLevelTheme(MusicTheme theme)
    {
        activeLevelTheme = theme;
        PlayCurrentLevelTheme(true);
    }

    private void TryTriggerHurryState()
    {
        if (hurryTriggered) return;
        var data = Data;
        if (!data) return;
        if (data.timer > hurryTimeThreshold) return;
        if (data.timer <= 0f) return;
        if (!IsHurryCapable(activeLevelTheme)) return;

        hurryTriggered = true;
        PlayHurryUpSfx();

        if (Mario && Mario.IsStarPowered)
        {
            PlayStarmanTheme();
            return;
        }

        PlayCurrentLevelTheme(true);
    }

    private void PlayHurryUpSfx()
    {
        if (!hurryUpSfx) return;
        var localSources = Sources;
        var source = localSources.Length > 0 ? localSources[0] : null;
        if (!source) return;
        source.PlayOneShot(hurryUpSfx);
    }

    private MusicTheme GetStarmanVariant()
    {
        return hurryTriggered ? MusicTheme.StarmanHurried : MusicTheme.Starman;
    }

    private static bool IsHurryCapable(MusicTheme theme)
    {
        var normalized = NormalizeLevelTheme(theme);
        return normalized is MusicTheme.Overworld or MusicTheme.Underground or MusicTheme.Underwater or MusicTheme.Castle;
    }

    private static MusicTheme GetHurriedVariant(MusicTheme theme)
    {
        return NormalizeLevelTheme(theme) switch
        {
            MusicTheme.Overworld => MusicTheme.OverworldHurried,
            MusicTheme.Underground => MusicTheme.UndergroundHurried,
            MusicTheme.Underwater => MusicTheme.UnderwaterHurried,
            MusicTheme.Castle => MusicTheme.CastleHurried,
            _ => NormalizeLevelTheme(theme)
        };
    }

    private static MusicTheme NormalizeLevelTheme(MusicTheme theme)
    {
        return theme switch
        {
            MusicTheme.OverworldHurried => MusicTheme.Overworld,
            MusicTheme.UndergroundHurried => MusicTheme.Underground,
            MusicTheme.UnderwaterHurried => MusicTheme.Underwater,
            MusicTheme.CastleHurried => MusicTheme.Castle,
            _ => theme
        };
    }

    private void ApplyEnvironmentTheme(CameraEnvironmentType environment, bool restartIfPlaying)
    {
        lastEnvironment = environment;
        hasLastEnvironment = true;

        activeLevelTheme = environment switch
        {
            CameraEnvironmentType.Overworld => MusicTheme.Overworld,
            CameraEnvironmentType.Underground => MusicTheme.Underground,
            CameraEnvironmentType.Underwater => MusicTheme.Underwater,
            CameraEnvironmentType.Castle => MusicTheme.Castle,
            _ => MusicTheme.Overworld
        };

        if (Mario && Mario.IsStarPowered) return;
        PlayCurrentLevelTheme(restartIfPlaying);
    }

    private void PrewarmAudioData()
    {
        _ = Sources;
        if (!preloadThemeAudioData) return;

        PreloadThemeAudioData(GetStarmanVariant());
        PreloadThemeAudioData(MusicTheme.Starman);
        PreloadThemeAudioData(MusicTheme.StarmanHurried);
        PreloadThemeAudioData(NormalizeLevelTheme(activeLevelTheme));
        PreloadThemeAudioData(GetHurriedVariant(activeLevelTheme));
    }

    private void PreloadThemeAudioData(MusicTheme theme)
    {
        if (!themes.TryGetValue(theme, out var clip) || !clip) return;
        clip.LoadAudioData();
    }
}
