using UnityEngine;

public static class ColliderExtensions
{
    public static bool CompareColliderTag(this Collider2D collider, string tag)
    {
        if (!collider) return false;
        if (collider.CompareTag(tag)) return true;
        if (collider.attachedRigidbody && collider.attachedRigidbody.CompareTag(tag)) return true;
        var root = collider.transform ? collider.transform.root : null;
        return root && root.CompareTag(tag);
    }

    public static bool TryGetComponentInParent<T>(this Collider2D collider, out T component) where T : Component
    {
        component = null;
        if (!collider) return false;

        component = collider.GetComponentInParent<T>();
        if (component) return true;

        if (!collider.attachedRigidbody) return false;
        component = collider.attachedRigidbody.GetComponentInParent<T>();
        return component;
    }

    public static bool TryGetComponentInChildren<T>(this Collider2D collider, out T component) where T : Component
    {
        component = null;
        if (!collider) return false;

        component = collider.GetComponentInChildren<T>();
        if (component) return true;

        if (!collider.attachedRigidbody) return false;
        component = collider.attachedRigidbody.GetComponentInChildren<T>();
        return component;
    }
}
