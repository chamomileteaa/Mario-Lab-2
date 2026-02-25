using UnityEngine;

[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool hasParticleSystem;
    [SerializeField, ConditionalField(nameof(hasParticleSystem), true, "Particle")] private bool autoReleaseOnParticleStop;
    [SerializeField] private bool destroyWhenReleaseFails = true;

    [SerializeField, HideInInspector] private bool isInPool;
    private PrefabPool ownerPool;
    private ParticleSystem particleSystemComponent;

    public PrefabPool OwnerPool => ownerPool;
    public bool IsInPool => isInPool;
    private ParticleSystem Particles => particleSystemComponent ? particleSystemComponent : particleSystemComponent = GetComponent<ParticleSystem>();

    private void Awake()
    {
        RefreshParticleOptions();
    }

    private void OnValidate()
    {
        RefreshParticleOptions();
    }

    private void OnParticleSystemStopped()
    {
        if (!autoReleaseOnParticleStop) return;
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

    private void RefreshParticleOptions()
    {
        hasParticleSystem = Particles;
        if (!hasParticleSystem)
        {
            autoReleaseOnParticleStop = false;
            return;
        }

        if (!autoReleaseOnParticleStop) return;

        var main = Particles.main;
        if (main.stopAction != ParticleSystemStopAction.Callback) main.stopAction = ParticleSystemStopAction.Callback;
    }

    public bool HandleReleaseFailure()
    {
        if (!destroyWhenReleaseFails) return false;
        Destroy(gameObject);
        return true;
    }
}
