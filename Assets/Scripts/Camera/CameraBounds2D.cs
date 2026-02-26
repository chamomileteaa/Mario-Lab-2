using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class CameraBounds2D : MonoBehaviour
{
    public static event Action<CameraBounds2D> MarioEntered;
    public static event Action<CameraBounds2D> MarioExited;

    [field: SerializeField] public int Priority { get; private set; }
    [field: SerializeField] public bool ResetProgressOnEnter { get; private set; }
    [field: SerializeField] public bool OverrideBackgroundColor { get; private set; }
    [field: SerializeField] public Color BackgroundColor { get; private set; } = Color.black;

    private BoxCollider2D boxCollider;

    private BoxCollider2D BoxCollider
    {
        get
        {
            if (boxCollider) return boxCollider;
            boxCollider = GetComponent<BoxCollider2D>();
            return boxCollider;
        }
    }

    public Rect WorldRect
    {
        get
        {
            var bounds = BoxCollider.bounds;
            return new Rect(bounds.min, bounds.size);
        }
    }

    private void Awake()
    {
        BoxCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        BoxCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareColliderTag("Player")) return;
        MarioEntered?.Invoke(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareColliderTag("Player")) return;
        MarioExited?.Invoke(this);
    }
}
