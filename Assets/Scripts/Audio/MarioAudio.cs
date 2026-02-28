using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MarioController))]
public class MarioAudio : AudioPlayer
{
    public enum MarioSfxType
    {
        Jump = 0,
        Grow = 1,
        Shrink = 2,
        OneUp = 3,
        Coin = 4,
        Stomp = 5,
        Damage = 6
    }

    [SerializeField] private SerializedEnumDictionary<MarioSfxType, AudioClip> cues = new SerializedEnumDictionary<MarioSfxType, AudioClip>();

    private MarioController mario;

    private MarioController Mario => mario ? mario : mario = GetComponent<MarioController>();

    private void OnEnable()
    {
        if (!Mario) return;

        Mario.Jumped += OnJumped;
        Mario.FormChanged += OnFormChanged;
        Mario.ExtraLifeCollected += OnExtraLifeCollected;
        Mario.CoinCollected += OnCoinCollected;
        Mario.EnemyStomped += OnEnemyStomped;
        Mario.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (!Mario) return;

        Mario.Jumped -= OnJumped;
        Mario.FormChanged -= OnFormChanged;
        Mario.ExtraLifeCollected -= OnExtraLifeCollected;
        Mario.CoinCollected -= OnCoinCollected;
        Mario.EnemyStomped -= OnEnemyStomped;
        Mario.Damaged -= OnDamaged;
    }

    public void Play(MarioSfxType type)
    {
        if (!cues.TryGetValue(type, out var clip) || !clip) return;
        PlayOneShot(clip);
    }

    private void OnJumped() => Play(MarioSfxType.Jump);

    private void OnFormChanged(MarioController.MarioForm previousForm, MarioController.MarioForm nextForm)
    {
        if (nextForm > previousForm) Play(MarioSfxType.Grow);
        else if (nextForm < previousForm) Play(MarioSfxType.Shrink);
    }

    private void OnExtraLifeCollected() => Play(MarioSfxType.OneUp);
    private void OnCoinCollected() => Play(MarioSfxType.Coin);
    private void OnEnemyStomped() => Play(MarioSfxType.Stomp);
    private void OnDamaged() => Play(MarioSfxType.Damage);

}
