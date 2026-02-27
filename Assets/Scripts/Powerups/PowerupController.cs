using UnityEngine;

public class PowerupController : MonoBehaviour
{
    private const string PlayerTag = "Player";

    public enum PowerupType
    {
        FormUpgrade = 0,
        Star = 1
    }

    [SerializeField] private PowerupType powerupType = PowerupType.FormUpgrade;
    [SerializeField] private MarioController.MarioForm grantedForm = MarioController.MarioForm.Big;
    [SerializeField, Min(0f)] private float starDuration = 10f;
    [SerializeField] private bool despawnOnCollect = true;
    private bool collected;

    private void OnEnable()
    {
        collected = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryCollect(collision.collider)) return;
        TryCollect(collision.otherCollider);
    }

    public void ApplyTo(MarioController mario)
    {
        if (!mario) return;

        switch (powerupType)
        {
            case PowerupType.FormUpgrade:
                mario.SetForm(grantedForm);
                break;

            case PowerupType.Star:
                mario.ActivateStarPower(starDuration);
                break;
        }
    }

    private bool TryCollect(Collider2D collider)
    {
        if (collected) return false;
        if (!collider) return false;
        if (!collider.CompareColliderTag(PlayerTag)) return false;
        if (!collider.TryGetComponentInParent(out MarioController mario)) return false;

        ApplyTo(mario);
        collected = true;
        if (despawnOnCollect)
            PrefabPoolService.Despawn(gameObject);
        return true;
    }
}
