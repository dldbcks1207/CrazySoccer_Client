using UnityEngine;

public class CameraMoveScript : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    [Range(0f, 1f)]
    public float smoothTime = 0.2f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public BoxCollider2D mapBounds;

    private Vector3 velocity = Vector3.zero;
    private Camera cam;

    private Vector2 minBounds;
    private Vector2 maxBounds;

    void Start()
    {
        cam = GetComponent<Camera>();
        CalculateCameraBounds();

    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + offset;

        if (mapBounds != null)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    private void CalculateCameraBounds()
    {
        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        minBounds = new Vector2(mapBounds.bounds.min.x + camWidth, mapBounds.bounds.min.y + camHeight);
        maxBounds = new Vector2(mapBounds.bounds.max.x - camWidth, mapBounds.bounds.max.y - camHeight);
    }
}