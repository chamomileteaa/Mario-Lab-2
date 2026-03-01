using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class PipeTeleporter : MonoBehaviour
{
    private enum PipeMode
    {
        EntryOnly = 0,
        ExitOnly = 1,
        EntryAndExit = 2
    }

    private enum PipeDirection
    {
        Down = 0,
        Up = 1,
        Left = 2,
        Right = 3
    }

    [Header("Connection")]
    [SerializeField] private PipeTeleporter connectedPipe;
    [SerializeField] private PipeMode mode = PipeMode.EntryAndExit;
    [SerializeField] private bool showConnectionGizmo = true;
    [SerializeField] private bool instantTeleport;

    [Header("Pipe Rendering")]
    [SerializeField, SortingLayerSelector] private string pipeSortingLayer = "Default";
    [SerializeField] private int pipeSortingOrder;

    [Header("Directions")]
    [SerializeField, ConditionalField("showEnterDirection", true)]
    private PipeDirection enterDirection = PipeDirection.Down;
    [SerializeField, ConditionalField("showExitDirection", true)]
    private PipeDirection exitDirection = PipeDirection.Up;

    [Header("Entry Rules")]
    [SerializeField, ConditionalField("showEntryRules", true)]
    private bool allowDropInEntry;
    [SerializeField, ConditionalField("showEntryRules", true)]
    private bool requireCrouchForDownEntry = true;
    [SerializeField, ConditionalField("showEntryRules", true)]
    [Min(0f)] private float minDropVelocity = 0.1f;
    [SerializeField, ConditionalField("showEntryRules", true)]
    [Range(0f, 1f)] private float inputThreshold = 0.5f;

    [Header("Travel")]
    [SerializeField, ConditionalField("showEntryTravelSettings", true)]
    [Min(0f)] private float entryDistance = 1f;
    [SerializeField, ConditionalField("showExitTravelSettings", true)]
    [Min(0f)] private float exitDistance = 1f;
    [SerializeField, ConditionalField("showEntryTravelSettings", true)]
    [Min(0.01f)] private float travelSpeed = 4f;
    [SerializeField, ConditionalField("showEntryTravelSettings", true)]
    [Min(0f)] private float midTeleportDelay = 0.04f;
    [SerializeField, ConditionalField("showEntryRules", true)]
    [Min(0f)] private float postExitLockTime = 0.12f;
    [SerializeField, ConditionalField("showEntryRules", true)]
    [Min(0f)] private float postExitInvulnerabilityTime = 0.35f;

    [SerializeField, HideInInspector] private bool showEnterDirection;
    [SerializeField, HideInInspector] private bool showExitDirection;
    [SerializeField, HideInInspector] private bool showEntryRules;
    [SerializeField, HideInInspector] private bool showEntryTravelSettings;
    [SerializeField, HideInInspector] private bool showExitTravelSettings;

    private static readonly Dictionary<int, float> marioLockUntil = new Dictionary<int, float>(8);

    private BoxCollider2D triggerCollider;
    private Coroutine activeTravelRoutine;
    private BoxCollider2D TriggerCollider => triggerCollider ? triggerCollider : triggerCollider = GetComponent<BoxCollider2D>();
    private bool CanBeEntered => mode != PipeMode.ExitOnly;
    private bool CanBeExit => mode != PipeMode.EntryOnly;

    private void Awake()
    {
        TriggerCollider.isTrigger = true;
        RefreshInspectorFlags();
    }

    private void OnValidate()
    {
        if (TriggerCollider) TriggerCollider.isTrigger = true;
        RefreshInspectorFlags();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartTravel(other, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartTravel(other, false);
    }

    private void TryStartTravel(Collider2D other, bool fromEnter)
    {
        if (activeTravelRoutine != null) return;
        if (!CanBeEntered) return;
        if (!connectedPipe)
        {
            Debug.LogWarning("PipeTeleporter missing connected pipe reference.", this);
            return;
        }
        if (!connectedPipe.CanBeExit)
        {
            Debug.LogWarning("Connected pipe is not configured as an exit.", connectedPipe);
            return;
        }

        if (!other.CompareColliderTag("Player")) return;
        if (!other.TryGetComponentInParent<MarioController>(out var mario) || !mario) return;
        if (!CanEnter(mario, fromEnter)) return;

        activeTravelRoutine = StartCoroutine(TravelRoutine(mario));
    }

    private bool CanEnter(MarioController mario, bool fromEnter)
    {
        if (!mario) return false;
        if (mario.IsDead || mario.IsWinning || mario.IsPipeTravelling) return false;
        if (!mario.IsGrounded) return false;
        if (PauseService.IsPaused(PauseType.Input) || PauseService.IsPaused(PauseType.Physics)) return false;
        if (IsLocked(mario)) return false;

        var moveInput = mario.MoveInput;
        var downPressed = moveInput.y <= -inputThreshold || (requireCrouchForDownEntry && mario.IsCrouching);
        var upPressed = moveInput.y >= inputThreshold;
        var leftPressed = moveInput.x <= -inputThreshold;
        var rightPressed = moveInput.x >= inputThreshold;

        if (allowDropInEntry && enterDirection == PipeDirection.Down && fromEnter)
        {
            if (TryGetMarioBody(mario, out var body))
            {
                if (body.linearVelocity.y <= -minDropVelocity)
                    return true;
            }
        }

        return enterDirection switch
        {
            PipeDirection.Down => requireCrouchForDownEntry ? mario.IsCrouching : downPressed,
            PipeDirection.Up => upPressed,
            PipeDirection.Left => leftPressed,
            PipeDirection.Right => rightPressed,
            _ => false
        };
    }

    private IEnumerator TravelRoutine(MarioController mario)
    {
        if (!TryGetMarioBody(mario, out var body) || !TryGetMarioCollider(mario, out var marioCollider))
        {
            activeTravelRoutine = null;
            yield break;
        }

        var spriteStates = CacheSpriteSorting(mario);
        var originalSimulated = body.simulated;
        var originalColliderEnabled = marioCollider.enabled;
        var destination = connectedPipe;
        var destinationPostExitLock = destination ? destination.postExitLockTime : 0f;
        var destinationPostExitInvulnerability = destination ? destination.postExitInvulnerabilityTime : 0f;
        var pausedInputForTravel = false;
        var travelStateSet = false;
        var forcedCrouchStateSet = false;

        try
        {
            var start = mario.transform.position;
            var entryCenter = GetWorldPoint();
            var entryTarget = BuildAxisTarget(start, entryCenter, enterDirection, entryDistance, false);
            var entryTravelDuration = instantTeleport ? 0f : EstimateTravelDuration(start, entryTarget, travelSpeed);
            var delayDuration = instantTeleport ? 0f : Mathf.Max(0f, midTeleportDelay);
            var destinationCenter = destination ? destination.GetWorldPoint() : Vector2.zero;
            var destinationSpawnInside = destination
                ? BuildAxisTarget(destinationCenter, destinationCenter, destination.exitDirection, destination.exitDistance, true)
                : Vector3.zero;
            var destinationExitTarget = destination
                ? BuildAxisTarget(destinationSpawnInside, destinationCenter, destination.exitDirection, 0f, false)
                : Vector3.zero;
            var exitSpeed = destination ? destination.travelSpeed : travelSpeed;
            var exitTravelDuration = destination && !destination.instantTeleport
                ? EstimateTravelDuration(destinationSpawnInside, destinationExitTarget, exitSpeed)
                : 0f;
            var totalPipeInvulnerability =
                entryTravelDuration +
                delayDuration +
                exitTravelDuration +
                Mathf.Max(0f, postExitInvulnerabilityTime) +
                Mathf.Max(0f, destinationPostExitInvulnerability);

            mario.ActivatePipeInvulnerability(totalPipeInvulnerability);
            SetLock(mario, 10f);
            PauseService.Pause(PauseType.Input);
            pausedInputForTravel = true;
            mario.SetPipeTravelState(true);
            travelStateSet = true;
            if (ShouldForceCrouchDuringTravel())
            {
                mario.SetForcedCrouchState(true);
                forcedCrouchStateSet = true;
            }
            mario.SetForcedMoveInput(GetForcedMoveInput(enterDirection), IsHorizontal(enterDirection));
            mario.NotifyPipeTravelled();

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            marioCollider.enabled = false;

            ApplyMarioBehindPipeSorting(mario, this);

            if (instantTeleport)
                mario.transform.position = entryTarget;
            else
                yield return MoveToTargetAtSpeed(mario.transform, entryTarget, travelSpeed);

            if (!instantTeleport && midTeleportDelay > 0f)
                yield return new WaitForSeconds(midTeleportDelay);

            if (!destination)
            {
                Debug.LogWarning("PipeTeleporter lost connected destination during travel.", this);
                yield break;
            }

            var destExitMouth = destinationCenter;
            var destExitDirection = destination.exitDirection;
            ApplyMarioBehindPipeSorting(mario, destination);
            mario.SetForcedMoveInput(GetForcedMoveInput(destExitDirection), IsHorizontal(destExitDirection));

            var spawnInside = destination.instantTeleport
                ? BuildAxisTarget(destExitMouth, destExitMouth, destExitDirection, 0f, false)
                : destinationSpawnInside;
            mario.transform.position = spawnInside;

            if (destination.instantTeleport)
                mario.transform.position = destinationExitTarget;
            else
                yield return MoveToTargetAtSpeed(mario.transform, destinationExitTarget, exitSpeed);
        }
        finally
        {
            if (body)
            {
                body.simulated = originalSimulated;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (marioCollider)
                marioCollider.enabled = originalColliderEnabled;

            RestoreSpriteSorting(spriteStates);

            if (mario)
            {
                mario.SetForcedMoveInput(Vector2.zero, false);
                if (forcedCrouchStateSet) mario.SetForcedCrouchState(false);
                if (travelStateSet) mario.SetPipeTravelState(false);
                if (pausedInputForTravel) PauseService.Resume(PauseType.Input);
                SetLock(mario, postExitLockTime + destinationPostExitLock);
                mario.ActivatePipeInvulnerability(postExitInvulnerabilityTime + destinationPostExitInvulnerability);
            }

            activeTravelRoutine = null;
        }
    }

    private Vector2 GetWorldPoint()
    {
        return TriggerCollider ? (Vector2)TriggerCollider.bounds.center : (Vector2)transform.position;
    }

    private static Vector2 DirectionToVector(PipeDirection direction)
    {
        return direction switch
        {
            PipeDirection.Down => Vector2.down,
            PipeDirection.Up => Vector2.up,
            PipeDirection.Left => Vector2.left,
            PipeDirection.Right => Vector2.right,
            _ => Vector2.zero
        };
    }

    private static IEnumerator MoveToTargetAtSpeed(Transform target, Vector3 to, float speed)
    {
        if (!target) yield break;
        var safeSpeed = Mathf.Max(0.01f, speed);
        while ((target.position - to).sqrMagnitude > 0.0001f)
        {
            target.position = Vector3.MoveTowards(target.position, to, safeSpeed * Time.deltaTime);
            yield return null;
        }

        target.position = to;
    }

    private static Vector3 BuildAxisTarget(Vector3 start, Vector3 center, PipeDirection direction, float distance, bool oppositeDirection)
    {
        var dir = DirectionToVector(direction);
        if (oppositeDirection) dir = -dir;

        var target = start;
        if (IsHorizontal(direction))
        {
            target.x = center.x + (dir.x * distance);
            return target;
        }

        target.y = center.y + (dir.y * distance);
        return target;
    }

    private static float EstimateTravelDuration(Vector3 from, Vector3 to, float speed)
    {
        var distance = Vector3.Distance(from, to);
        if (distance <= 0f) return 0f;
        return distance / Mathf.Max(0.01f, speed);
    }

    private static bool IsHorizontal(PipeDirection direction)
    {
        return direction == PipeDirection.Left || direction == PipeDirection.Right;
    }

    private static Vector2 GetForcedMoveInput(PipeDirection direction)
    {
        return direction switch
        {
            PipeDirection.Left => Vector2.left,
            PipeDirection.Right => Vector2.right,
            _ => Vector2.zero
        };
    }

    private static bool TryGetMarioBody(MarioController mario, out Rigidbody2D body)
    {
        body = mario ? mario.GetComponent<Rigidbody2D>() : null;
        return body;
    }

    private static bool TryGetMarioCollider(MarioController mario, out Collider2D collider)
    {
        collider = mario ? mario.GetComponent<Collider2D>() : null;
        return collider;
    }

    private static bool IsLocked(MarioController mario)
    {
        if (!mario) return false;
        var key = mario.GetInstanceID();
        if (!marioLockUntil.TryGetValue(key, out var until)) return false;
        if (Time.time < until) return true;
        marioLockUntil.Remove(key);
        return false;
    }

    private static void SetLock(MarioController mario, float seconds)
    {
        if (!mario) return;
        var key = mario.GetInstanceID();
        marioLockUntil[key] = Time.time + Mathf.Max(0f, seconds);
    }

    private void RefreshInspectorFlags()
    {
        showEnterDirection = mode != PipeMode.ExitOnly;
        showExitDirection = mode != PipeMode.EntryOnly;
        showEntryRules = mode != PipeMode.ExitOnly;
        showEntryTravelSettings = mode != PipeMode.ExitOnly && !instantTeleport;
        showExitTravelSettings = mode != PipeMode.EntryOnly && !instantTeleport;
    }

    private bool ShouldForceCrouchDuringTravel()
    {
        return enterDirection == PipeDirection.Down && requireCrouchForDownEntry;
    }

    private static SortingState[] CacheSpriteSorting(MarioController mario)
    {
        if (!mario) return null;
        var renderers = mario.GetComponentsInChildren<SpriteRenderer>(true);
        var states = new SortingState[renderers.Length];
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            states[i] = new SortingState
            {
                Renderer = renderer,
                SortingLayerId = renderer ? renderer.sortingLayerID : 0,
                SortingOrder = renderer ? renderer.sortingOrder : 0
            };
        }

        return states;
    }

    private static void RestoreSpriteSorting(SortingState[] states)
    {
        if (states == null) return;
        for (var i = 0; i < states.Length; i++)
        {
            var state = states[i];
            if (!state.Renderer) continue;
            state.Renderer.sortingLayerID = state.SortingLayerId;
            state.Renderer.sortingOrder = state.SortingOrder;
        }
    }

    private static void ApplyMarioBehindPipeSorting(MarioController mario, PipeTeleporter pipe)
    {
        if (!mario || !pipe) return;
        var targetLayer = SortingLayer.NameToID(pipe.pipeSortingLayer);
        if (targetLayer == 0 && !string.Equals(pipe.pipeSortingLayer, "Default", System.StringComparison.Ordinal))
            targetLayer = SortingLayer.NameToID("Default");
        var targetOrder = pipe.pipeSortingOrder - 1;
        var renderers = mario.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!renderer) continue;
            renderer.sortingLayerID = targetLayer;
            renderer.sortingOrder = targetOrder;
        }
    }

    private struct SortingState
    {
        public SpriteRenderer Renderer;
        public int SortingLayerId;
        public int SortingOrder;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showConnectionGizmo) return;
        if (!connectedPipe) return;

        var from = transform.position;
        var to = connectedPipe.transform.position;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(from, 0.1f);
        Gizmos.DrawWireSphere(to, 0.1f);

        var connectedCollider = connectedPipe.GetComponent<BoxCollider2D>();
        if (!connectedCollider) return;

        var bounds = connectedCollider.bounds;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
