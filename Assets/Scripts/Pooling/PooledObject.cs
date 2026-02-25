using UnityEngine;

[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool hasParticleSystem;
    [SerializeField, ConditionalField(nameof(hasParticleSystem))] private bool releaseOnParticleStop;
    [SerializeField] private bool destroyWhenReleaseFails = true;

    [SerializeField, HideInInspector] private bool isInPool;
    private PrefabPool ownerPool;
    private ParticleSystem particleSystemComponent;

    public PrefabPool OwnerPool => ownerPool;
    public bool IsInPool => isInPool;
    private ParticleSystem Particles => particleSystemComponent ? particleSystemComponent : particleSystemComponent = GetComponent<ParticleSystem>();

    private void Awake()
    {
        SyncParticleState();
        EnsureParticleStopCallback();
    }

    private void OnValidate()
    {
        SyncParticleState();
        EnsureParticleStopCallback();
    }

    private void OnParticleSystemStopped()
    {
        if (!releaseOnParticleStop) return;
        ReleaseToPool();
    }

    public void Bind(PrefabPool pool)
    {
        ownerPool = pool;
    }

    public void SetInPool(bool value)
    {
        isInPool = value;
    }

    public bool ReleaseToPool()
    {
        if (isInPool) return true;
        if (ownerPool && ownerPool.Release(this)) return true;
        return HandleReleaseFailure();
    }

    private void EnsureParticleStopCallback()
    {
        if (!releaseOnParticleStop) return;
        if (!Particles) return;

        var main = Particles.main;
        if (main.stopAction != ParticleSystemStopAction.Callback) main.stopAction = ParticleSystemStopAction.Callback;
    }

    private void SyncParticleState()
    {
        hasParticleSystem = Particles;
        if (hasParticleSystem) return;
        releaseOnParticleStop = false;
    }

    public bool HandleReleaseFailure()
    {
        if (!destroyWhenReleaseFails) return false;
        Destroy(gameObject);
        return true;
    }
}
