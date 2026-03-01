using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform followTarget;

    [Header("Follow")]
    [SerializeField] private Vector2 followOffset = new Vector2(2f, 0f);
    [SerializeField] private Vector2 deadZoneHalfSize = new Vector2(1.2f, 0.6f);
    [SerializeField] private bool lockHorizontal;
    [SerializeField] private bool lockVertical = true;

    [Header("Motion")]
    [SerializeField] private Vector2 smoothTime = new Vector2(0.05f, 0.08f);

    [Header("Rules")]
    [SerializeField] private bool preventBacktrackingX = true;

    [Header("Main Menu")]
    [SerializeField] private Vector2 mainMenuOffset = new Vector2(0f, 6f);

    private readonly List<CameraBounds2D> enteredBounds = new List<CameraBounds2D>();
    private readonly List<CameraBounds2D> sceneBounds = new List<CameraBounds2D>();

    private Camera sceneCamera;
    private Camera SceneCamera => sceneCamera ? sceneCamera : sceneCamera = GetComponent<Camera>();
    private Color defaultBackgroundColor;

    private CameraBounds2D activeBounds;
    public CameraBounds2D ActiveBounds
    {
        get => activeBounds;
        private set
        {
            if (activeBounds == value) return;
            activeBounds = value;
            ActiveBoundsChanged?.Invoke(activeBounds);
        }
    }

    public event Action<CameraBounds2D> ActiveBoundsChanged;

    private Vector2 dampingVelocity;
    private float lockedX;
    private float lockedY;
    private float maxReachedX;
    private bool mainMenuMode;
    private bool savedLockState;
    private bool savedLockHorizontal;
    private bool savedLockVertical;

    private void Awake()
    {
        defaultBackgroundColor = SceneCamera.backgroundColor;
        lockedX = transform.position.x;
        lockedY = transform.position.y;
        maxReachedX = transform.position.x;

        CacheSceneBounds();
        UpdateActiveBounds();
        ApplyBackgroundColor();
    }

    private void OnEnable()
    {
        CameraBounds2D.MarioEntered += OnMarioEntered;
        CameraBounds2D.MarioExited += OnMarioExited;
    }

    private void OnDisable()
    {
        CameraBounds2D.MarioEntered -= OnMarioEntered;
        CameraBounds2D.MarioExited -= OnMarioExited;
    }

    private void OnMarioEntered(CameraBounds2D bounds)
    {
        if (!bounds) return;
        if (!enteredBounds.Contains(bounds)) enteredBounds.Add(bounds);

        UpdateActiveBounds();
        ApplyBackgroundColor();

        if (bounds.ResetProgressOnEnter) maxReachedX = transform.position.x;
    }

    private void OnMarioExited(CameraBounds2D bounds)
    {
        if (!bounds) return;
        enteredBounds.Remove(bounds);
        UpdateActiveBounds();
        ApplyBackgroundColor();
    }

    private void LateUpdate()
    {
        if (mainMenuMode)
        {
            transform.position = GetMainMenuPosition();
            return;
        }

        if (!followTarget) return;
        if (!ActiveBounds) return;

        var currentPosition = transform.position;
        var targetPosition = (Vector2)followTarget.position + followOffset;

        var desiredX = lockHorizontal
            ? lockedX
            : ApplyDeadZone(currentPosition.x, targetPosition.x, deadZoneHalfSize.x);
        var desiredY = lockVertical
            ? lockedY
            : ApplyDeadZone(currentPosition.y, targetPosition.y, deadZoneHalfSize.y);

        if (preventBacktrackingX) desiredX = Mathf.Max(desiredX, maxReachedX);

        currentPosition.x = SmoothDampOrSnap(currentPosition.x, desiredX, ref dampingVelocity.x, smoothTime.x);
        currentPosition.y = SmoothDampOrSnap(currentPosition.y, desiredY, ref dampingVelocity.y, smoothTime.y);

        currentPosition = SceneCamera.ClampToOrthographicBounds(ActiveBounds.WorldRect, currentPosition);
        transform.position = currentPosition;

        if (preventBacktrackingX) maxReachedX = Mathf.Max(maxReachedX, currentPosition.x);
    }

    private void CacheSceneBounds()
    {
        sceneBounds.Clear();
        var found = FindObjectsByType<CameraBounds2D>(FindObjectsSortMode.None);
        sceneBounds.AddRange(found);
    }

    private void UpdateActiveBounds()
    {
        var point = followTarget ? (Vector2)followTarget.position : (Vector2)transform.position;
        ActiveBounds = PickActiveBounds(enteredBounds, sceneBounds, point);
    }

    private static CameraBounds2D PickActiveBounds(
        IReadOnlyList<CameraBounds2D> entered,
        IReadOnlyList<CameraBounds2D> all,
        Vector2 point)
    {
        return PickHighestPriority(entered)
               ?? PickHighestPriorityContaining(all, point)
               ?? PickNearest(all, point);
    }

    private static CameraBounds2D PickHighestPriority(IReadOnlyList<CameraBounds2D> source)
    {
        CameraBounds2D best = null;
        var bestPriority = int.MinValue;

        foreach (var bounds in source)
        {
            if (!bounds) continue;
            if (bounds.Priority < bestPriority) continue;

            best = bounds;
            bestPriority = bounds.Priority;
        }

        return best;
    }

    private static CameraBounds2D PickHighestPriorityContaining(IReadOnlyList<CameraBounds2D> source, Vector2 point)
    {
        CameraBounds2D best = null;
        var bestPriority = int.MinValue;

        foreach (var bounds in source)
        {
            if (!bounds) continue;
            if (!bounds.WorldRect.Contains(point)) continue;
            if (bounds.Priority < bestPriority) continue;

            best = bounds;
            bestPriority = bounds.Priority;
        }

        return best;
    }

    private static CameraBounds2D PickNearest(IReadOnlyList<CameraBounds2D> source, Vector2 point)
    {
        CameraBounds2D best = null;
        var bestDistance = float.PositiveInfinity;

        foreach (var bounds in source)
        {
            if (!bounds) continue;

            var distance = (bounds.WorldRect.center - point).sqrMagnitude;
            if (distance >= bestDistance) continue;

            best = bounds;
            bestDistance = distance;
        }

        return best;
    }

    private static float ApplyDeadZone(float current, float target, float halfSize)
    {
        if (halfSize <= 0f) return target;

        var delta = target - current;
        if (Mathf.Abs(delta) <= halfSize) return current;
        return target - Mathf.Sign(delta) * halfSize;
    }

    private static float SmoothDampOrSnap(float current, float target, ref float velocity, float smoothTimeValue)
    {
        if (smoothTimeValue <= 0f) return target;
        return Mathf.SmoothDamp(current, target, ref velocity, smoothTimeValue);
    }

    public void SetAxisLocks(bool horizontalLocked, bool verticalLocked, bool lockToCurrentPosition = true)
    {
        if (horizontalLocked && lockToCurrentPosition) lockedX = transform.position.x;
        if (verticalLocked && lockToCurrentPosition) lockedY = transform.position.y;
        lockHorizontal = horizontalLocked;
        lockVertical = verticalLocked;
    }

    public void EnterMainMenuMode()
    {
        if (!savedLockState)
        {
            savedLockHorizontal = lockHorizontal;
            savedLockVertical = lockVertical;
            savedLockState = true;
        }

        mainMenuMode = true;
        transform.position = GetMainMenuPosition();
        SetAxisLocks(true, true, true);
        if (preventBacktrackingX) maxReachedX = transform.position.x;
    }

    public void ExitMainMenuMode()
    {
        if (!mainMenuMode) return;
        mainMenuMode = false;

        if (savedLockState)
        {
            SetAxisLocks(savedLockHorizontal, savedLockVertical, true);
            savedLockState = false;
        }
    }

    public void SetMainMenuOffset(Vector2 offset)
    {
        mainMenuOffset = offset;
        if (!mainMenuMode) return;
        transform.position = GetMainMenuPosition();
    }

    private Vector3 GetMainMenuPosition()
    {
        var current = transform.position;
        if (!followTarget) return current;

        var position = new Vector3(
            followTarget.position.x + followOffset.x + mainMenuOffset.x,
            followTarget.position.y + followOffset.y + mainMenuOffset.y,
            current.z);

        if (ActiveBounds)
            position = SceneCamera.ClampToOrthographicBounds(ActiveBounds.WorldRect, position);

        return position;
    }

    private void ApplyBackgroundColor()
    {
        var camera = SceneCamera;
        if (!camera) return;

        if (ActiveBounds && ActiveBounds.OverrideBackgroundColor)
        {
            camera.backgroundColor = ActiveBounds.BackgroundColor;
            return;
        }

        camera.backgroundColor = defaultBackgroundColor;
    }
}
