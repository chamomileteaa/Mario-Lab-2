using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
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

    [Header("Reset Flow")]
    [SerializeField] private bool reloadSceneWhenReturningToMenu = true;
    [SerializeField] private UnityEvent inSceneResetActions;

    private const PauseType GameplayPauseTypes = PauseType.Physics | PauseType.Animation | PauseType.Input;
    private static bool forceMainMenuOnNextSceneLoad;

    private GameData gameData;
    private bool userPaused;
    private AudioPlayer audioPlayer;
    private AudioPlayer AudioPlayer => audioPlayer ? audioPlayer : audioPlayer = GetComponent<AudioPlayer>();

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

        if (ConsumeForcedMainMenuRequest())
        {
            gameData?.ResetAll();
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
        ClearUserPause();
        PauseService.Pause(GameplayPauseTypes);
        if (cameraController) cameraController.EnterMainMenuMode();
        if (ui) ui.SetVisible(false);

        if (introOverlay) introOverlay.HideInstant();
        if (gameOverOverlay) gameOverOverlay.HideInstant();
        if (mainMenu)
            mainMenu.Show(HandleStartPressed);
    }

    public void StartNewRun()
    {
        forceMainMenuOnNextSceneLoad = false;
        gameData.ResetAll();
        gameData.BeginRun();
        StartLevelFlow();
    }

    public void ReloadCurrentLevel()
    {
        ResolveSceneReferences();
        ClearUserPause();
        PauseService.Resume(GameplayPauseTypes);
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    public void CompleteRunAndReturnToMainMenu()
    {
        ReturnToMainMenu();
    }

    public void ReturnToMainMenu()
    {
        ResolveSceneReferences();
        if (!gameData) gameData = GameData.GetOrCreate();
        gameData?.ResetAll();
        ClearUserPause();
        PauseService.Resume(GameplayPauseTypes);

        if (!reloadSceneWhenReturningToMenu)
        {
            inSceneResetActions?.Invoke();
            forceMainMenuOnNextSceneLoad = false;
            ShowMainMenu();
            return;
        }

        forceMainMenuOnNextSceneLoad = true;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void HandleStartPressed()
    {
        StartNewRun();
    }

    private void StartLevelFlow()
    {
        ResolveSceneReferences();
        ClearUserPause();

        // Release any existing lock (for example, from main menu) before re-pausing for intro.
        PauseService.Resume(GameplayPauseTypes);
        PauseService.Pause(GameplayPauseTypes);
        if (mainMenu) mainMenu.HideInstant();
        if (gameOverOverlay) gameOverOverlay.HideInstant();
        if (cameraController) cameraController.ExitMainMenuMode();

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

    private static bool ConsumeForcedMainMenuRequest()
    {
        if (!forceMainMenuOnNextSceneLoad) return false;
        forceMainMenuOnNextSceneLoad = false;
        return true;
    }

}
