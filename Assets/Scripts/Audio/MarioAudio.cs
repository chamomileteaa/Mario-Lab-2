using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(MarioController))]
public class MarioAudio : AudioPlayer
{
    public enum MarioSfxType
    {
        Jump = 0,
        Powerup = 1,
        Powerdown = 2,
        OneUp = 3,
        Coin = 4,
        Stomp = 5,
        Fireball = 6,
        Pipe = 7,
        Skid = 8,
        Kick = 9,
        JumpSmall = 10
    }

    [SerializeField] private SerializedEnumDictionary<MarioSfxType, AudioClip> cues = new SerializedEnumDictionary<MarioSfxType, AudioClip>();
    [SerializeField, Min(0f)] private float shortJumpDecisionWindow = 0.08f;

    private MarioController mario;
    private Coroutine jumpAudioRoutine;

    private MarioController Mario => mario ? mario : mario = GetComponent<MarioController>();

    private void Awake()
    {
        // Ensure AudioSource exists before first cue to avoid first-play setup hitch.
        _ = Source;
    }

    private void OnEnable()
    {
        SetMarioSubscriptions(true);
    }

    private void OnDisable()
    {
        SetMarioSubscriptions(false);
    }

    public void Play(MarioSfxType type)
    {
        if (!cues.TryGetValue(type, out var clip) || !clip) return;
        PlayOneShot(clip);
    }

    private void OnJumped()
    {
        if (jumpAudioRoutine != null)
            StopCoroutine(jumpAudioRoutine);

        jumpAudioRoutine = StartCoroutine(PlayJumpAudioWithDecision());
    }

    private void OnFormChanged(MarioController.MarioForm previousForm, MarioController.MarioForm nextForm)
    {
        if (nextForm > previousForm) Play(MarioSfxType.Powerup);
        else if (nextForm < previousForm) Play(MarioSfxType.Powerdown);
    }

    private void OnExtraLifeCollected() => Play(MarioSfxType.OneUp);
    private void OnCoinCollected() => Play(MarioSfxType.Coin);
    private void OnEnemyStomped() => Play(MarioSfxType.Stomp);
    private void OnDamaged() => Play(MarioSfxType.Powerdown);
    private void OnFireballShot() => Play(MarioSfxType.Fireball);
    private void OnPipeTravelled() => Play(MarioSfxType.Pipe);
    private void OnSkidded() => Play(MarioSfxType.Skid);
    private void OnKicked() => Play(MarioSfxType.Kick);
    private void OnStarPowerChanged(bool active) { if (active) Play(MarioSfxType.Powerup); }

    private IEnumerator PlayJumpAudioWithDecision()
    {
        yield return null;

        var elapsed = 0f;
        while (elapsed < shortJumpDecisionWindow)
        {
            if (!Mario.IsJumpHeld)
            {
                Play(MarioSfxType.JumpSmall);
                jumpAudioRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Play(MarioSfxType.Jump);
        jumpAudioRoutine = null;
    }

    private void SetMarioSubscriptions(bool subscribe)
    {
        if (!Mario) return;

        if (subscribe)
        {
            Mario.Jumped += OnJumped;
            Mario.FormChanged += OnFormChanged;
            Mario.ExtraLifeCollected += OnExtraLifeCollected;
            Mario.CoinCollected += OnCoinCollected;
            Mario.EnemyStomped += OnEnemyStomped;
            Mario.Damaged += OnDamaged;
            Mario.FireballShot += OnFireballShot;
            Mario.PipeTravelled += OnPipeTravelled;
            Mario.Skidded += OnSkidded;
            Mario.Kicked += OnKicked;
            Mario.StarPowerChanged += OnStarPowerChanged;
            return;
        }

        Mario.Jumped -= OnJumped;
        Mario.FormChanged -= OnFormChanged;
        Mario.ExtraLifeCollected -= OnExtraLifeCollected;
        Mario.CoinCollected -= OnCoinCollected;
        Mario.EnemyStomped -= OnEnemyStomped;
        Mario.Damaged -= OnDamaged;
        Mario.FireballShot -= OnFireballShot;
        Mario.PipeTravelled -= OnPipeTravelled;
        Mario.Skidded -= OnSkidded;
        Mario.Kicked -= OnKicked;
        Mario.StarPowerChanged -= OnStarPowerChanged;
    }

}
