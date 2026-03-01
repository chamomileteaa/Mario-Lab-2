using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioPlayer))]
public class GameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private HudController ui;
    [SerializeField] private MainMenuController mainMenu;
    [SerializeField] private IntroOverlayController introOverlay;
    [SerializeField] private PauseOverlayController pauseOverlay;
    [SerializeField] private GameOverOverlayController gameOverOverlay;
    [SerializeField] private CameraController cameraController;

    [Header("Input")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("Audio")]
    [SerializeField] private AudioClip pauseToggleSfx;

    [Header("Scene Reload")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField, Min(0f)] private float mainMenuStartInputDelay = 0.35f;

    private const PauseType GameplayPauseTypes = PauseType.Physics | PauseType.Animation | PauseType.Input;
    private static bool forceMainMenuOnNextLoad;

    private GameData gameData;
    private bool userPaused;
    private float mainMenuInputUnlockTime;
    private AudioPlayer audioPlayer;
    private MusicPlayer musicPlayer;

    private AudioPlayer AudioPlayer => audioPlayer ? audioPlayer : audioPlayer = GetComponent<AudioPlayer>();
    private MusicPlayer Music => musicPlayer ? musicPlayer : musicPlayer = FindFirstObjectByType<MusicPlayer>(FindObjectsInactive.Include);

    public static GameManager Instance { get; private set; }

    private void OnEnable()
    {
        BindPauseAction();
    }

    private void OnDisable()
    {
        UnbindPauseAction();
    }

    private void Start()
    {
        if (Instance && Instance != this)
            Debug.LogWarning("Multiple GameManager instances detected in scene.", this);
        Instance = this;

        gameData = GameData.GetOrCreate();
        ResolveSceneReferences();

        if (!ui)
            Debug.LogWarning("GameManager could not find a HudController in scene.", this);

        if (forceMainMenuOnNextLoad)
        {
            forceMainMenuOnNextLoad = false;
            if (gameData) gameData.ResetAll();
            ShowMainMenu();
            return;
        }

        if (!gameData.runActive)
        {
            ShowMainMenu();
            return;
        }

        StartLevelFlow();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowMainMenu()
    {
        ResolveSceneReferences();

        if (!gameData) gameData = GameData.GetOrCreate();
        gameData.EndRun();

        ClearUserPause();
        PauseService.Pause(GameplayPauseTypes);

        Music?.PlayNameEntryTheme();
        if (cameraController) cameraController.EnterMainMenuMode();

        if (ui) ui.SetVisible(false);
        if (introOverlay) introOverlay.HideInstant();
        if (gameOverOverlay) gameOverOverlay.HideInstant();
        if (pauseOverlay) pauseOverlay.HideInstant();
        if (mainMenu) mainMenu.Show(HandleStartPressed);
        mainMenuInputUnlockTime = Time.unscaledTime + Mathf.Max(0f, mainMenuStartInputDelay);
    }

    public void StartNewRun()
    {
        forceMainMenuOnNextLoad = false;
        if (!gameData) gameData = GameData.GetOrCreate();
        gameData.ResetAll();
        gameData.BeginRun();
        StartLevelFlow();
    }

    public void ReloadSceneForRetry()
    {
        forceMainMenuOnNextLoad = false;
        if (!gameData) gameData = GameData.GetOrCreate();
        gameData.BeginRun();
        ReloadActiveScene();
    }

    public void ReloadSceneToMainMenu()
    {
        forceMainMenuOnNextLoad = true;
        if (!gameData) gameData = GameData.GetOrCreate();
        gameData.ResetAll();
        ReloadActiveScene();
    }

    private void ReloadActiveScene()
    {
        ClearUserPause();
        PauseService.ClearAll();

        if (!string.IsNullOrWhiteSpace(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            return;
        }

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name, LoadSceneMode.Single);
    }

    private void HandleStartPressed()
    {
        if (Time.unscaledTime < mainMenuInputUnlockTime) return;
        StartNewRun();
    }

    private void StartLevelFlow()
    {
        ResolveSceneReferences();
        ClearUserPause();

        PauseService.Resume(GameplayPauseTypes);
        PauseService.Pause(GameplayPauseTypes);

        if (mainMenu) mainMenu.HideInstant();
        if (gameOverOverlay) gameOverOverlay.HideInstant();
        if (cameraController) cameraController.ExitMainMenuMode();
        Music?.OnGameplayRunStarted();

        if (ui) ui.BeginLevel();
        if (ui) ui.SetVisible(false);

        var lives = gameData ? gameData.lives : 3;
        var world = gameData && !string.IsNullOrWhiteSpace(gameData.world) ? gameData.world : "-";
        if (!introOverlay)
        {
            ResumeGameplay();
            if (ui) ui.SetVisible(true);
            return;
        }

        introOverlay.Show(lives, world, () =>
        {
            if (ui) ui.SetVisible(true);
            ResumeGameplay();
        });
    }

    private void ResumeGameplay()
    {
        if (userPaused) return;
        PauseService.Resume(GameplayPauseTypes);
    }

    private void ResolveSceneReferences()
    {
        if (!ui)
            ui = FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
        if (!mainMenu)
            mainMenu = FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (!introOverlay)
            introOverlay = FindFirstObjectByType<IntroOverlayController>(FindObjectsInactive.Include);
        if (!pauseOverlay)
            pauseOverlay = FindFirstObjectByType<PauseOverlayController>(FindObjectsInactive.Include);
        if (!gameOverOverlay)
            gameOverOverlay = FindFirstObjectByType<GameOverOverlayController>(FindObjectsInactive.Include);
        if (!cameraController)
            cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
    }

    private bool ShouldAcceptPauseInput()
    {
        if (!gameData.runActive) return false;
        if (mainMenu && mainMenu.IsVisible) return false;
        if (introOverlay && introOverlay.IsVisible) return false;
        if (gameOverOverlay && gameOverOverlay.IsVisible) return false;
        return true;
    }

    private void BindPauseAction()
    {
        if (!pauseAction || pauseAction.action == null) return;
        pauseAction.action.performed += OnPausePerformed;
        pauseAction.SetEnabled(true);
    }

    private void UnbindPauseAction()
    {
        if (!pauseAction || pauseAction.action == null) return;
        pauseAction.action.performed -= OnPausePerformed;
        pauseAction.SetEnabled(false);
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!gameData) gameData = GameData.GetOrCreate();
        if (!gameData || !gameData.runActive) return;
        if (!ShouldAcceptPauseInput()) return;
        TogglePause();
    }

    private void TogglePause()
    {
        ResolveSceneReferences();
        if (userPaused)
        {
            userPaused = false;
            if (pauseOverlay) pauseOverlay.HideInstant();
            PauseService.Resume(GameplayPauseTypes);
            PlayPauseToggleSfx();
            return;
        }

        userPaused = true;
        if (pauseOverlay) pauseOverlay.Show();
        PauseService.Pause(GameplayPauseTypes);
        PlayPauseToggleSfx();
    }

    private void ClearUserPause()
    {
        ResolveSceneReferences();
        if (!userPaused)
        {
            if (pauseOverlay) pauseOverlay.HideInstant();
            return;
        }

        userPaused = false;
        if (pauseOverlay) pauseOverlay.HideInstant();
        PauseService.Resume(GameplayPauseTypes);
    }

    private void PlayPauseToggleSfx()
    {
        if (!pauseToggleSfx) return;
        AudioPlayer?.PlayOneShot(pauseToggleSfx);
    }
}
