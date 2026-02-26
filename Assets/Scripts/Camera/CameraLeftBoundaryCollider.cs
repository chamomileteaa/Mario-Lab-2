using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class CameraLeftBoundaryCollider : MonoBehaviour
{
    [SerializeField] private Camera trackedCamera;
    [SerializeField, Min(0.01f)] private float wallThickness = 0.2f;
    [SerializeField, Min(0f)] private float extraHeight = 100f;
    [SerializeField] private float wallEdgeOffset;
    [SerializeField] private bool followCameraY = true;

    private Rigidbody2D body2D;
    private BoxCollider2D boxCollider2D;
    private float initialY;

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D Box => boxCollider2D ? boxCollider2D : boxCollider2D = GetComponent<BoxCollider2D>();
    private Camera SceneCamera => trackedCamera ? trackedCamera : trackedCamera = Camera.main;

    public void SetTrackedCamera(Camera camera)
    {
        trackedCamera = camera;
    }

    private void Reset()
    {
        SetupComponents();
        var sceneCamera = Camera.main;
        if (sceneCamera) trackedCamera = sceneCamera;
    }

    private void Awake()
    {
        SetupComponents();
        initialY = transform.position.y;
        SyncToCamera();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) SetupComponents();
    }

    private void LateUpdate()
    {
        SyncToCamera();
    }

    private void SetupComponents()
    {
        var rigidbody2D = Body;
        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.simulated = true;
        rigidbody2D.gravityScale = 0f;
        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.angularVelocity = 0f;

        var collider2D = Box;
        collider2D.isTrigger = false;
        collider2D.size = new Vector2(Mathf.Max(0.01f, wallThickness), Mathf.Max(1f, collider2D.size.y));
        collider2D.offset = Vector2.zero;
    }

    private void SyncToCamera()
    {
        var sceneCamera = SceneCamera;
        if (!sceneCamera || !sceneCamera.orthographic) return;

        var halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
        var halfHeight = sceneCamera.orthographicSize;

        var colliderSize = Box.size;
        colliderSize.x = Mathf.Max(0.01f, wallThickness);
        colliderSize.y = Mathf.Max(1f, halfHeight * 2f + extraHeight);
        Box.size = colliderSize;

        var leftEdge = sceneCamera.transform.position.x - halfWidth + wallEdgeOffset;
        var targetX = leftEdge - colliderSize.x * 0.5f;
        var targetY = followCameraY ? sceneCamera.transform.position.y : initialY;

        Body.position = new Vector2(targetX, targetY);
    }
}
