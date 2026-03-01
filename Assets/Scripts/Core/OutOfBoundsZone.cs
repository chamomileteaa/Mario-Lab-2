using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class OutOfBoundsZone : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [SerializeField] private bool affectMario = true;
    [SerializeField] private bool despawnEntities = true;
    [SerializeField] private bool despawnOtherRigidbodies;

    private BoxCollider2D zoneCollider;
    private BoxCollider2D ZoneCollider => zoneCollider ? zoneCollider : zoneCollider = GetComponent<BoxCollider2D>();

    private void Awake()
    {
        ZoneCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        ZoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other) return;

        if (affectMario && other.CompareColliderTag(PlayerTag) &&
            other.TryGetComponentInParent(out MarioController mario))
        {
            if (mario.IsPipeInvulnerable) return;
            mario.KillFromOutOfBounds();
            return;
        }

        if (despawnEntities && other.TryGetComponentInParent(out EntityController entity))
        {
            PrefabPoolService.Despawn(entity.gameObject);
            return;
        }

        if (!despawnOtherRigidbodies) return;
        if (!other.attachedRigidbody) return;
        if (other.CompareColliderTag(PlayerTag)) return;
        PrefabPoolService.Despawn(other.attachedRigidbody.gameObject);
    }
}
