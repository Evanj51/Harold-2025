using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomBoundsController : MonoBehaviour
{
    [Header("Camera Bounds")]
    [SerializeField] private Vector2 boundsPadding = Vector2.zero;
    [SerializeField] private bool overrideCameraBounds = true;
    [SerializeField] private Vector2 customLowerBounds;
    [SerializeField] private Vector2 customUpperBounds;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = -1f; // -1 means use camera default
    [SerializeField] private bool transitionOnEnter = true;
    [SerializeField] private bool snapCameraOnEnter = false;

    private BoxCollider2D boundsCollider;
    private Vector2 lowerBounds;
    private Vector2 upperBounds;

    private void Awake()
    {
        boundsCollider = GetComponent<BoxCollider2D>();
        boundsCollider.isTrigger = true;

        if (overrideCameraBounds)
        {
            lowerBounds = customLowerBounds;
            upperBounds = customUpperBounds;
        }
        else
        {
            // Calculate bounds from collider
            Bounds bounds = boundsCollider.bounds;
            lowerBounds = new Vector2(
                bounds.min.x + boundsPadding.x,
                bounds.min.y + boundsPadding.y
            );

            upperBounds = new Vector2(
                bounds.max.x - boundsPadding.x,
                bounds.max.y - boundsPadding.y
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            HollowKnightCamera camera = Camera.main?.GetComponent<HollowKnightCamera>();

            if (camera != null)
            {
                if (transitionOnEnter)
                {
                    if (snapCameraOnEnter)
                    {
                        camera.SetBounds(lowerBounds, upperBounds);
                        camera.SnapToTarget();
                    }
                    else
                    {
                        camera.TransitionToBounds(lowerBounds, upperBounds, transitionDuration);
                    }
                }
                else
                {
                    camera.SetBounds(lowerBounds, upperBounds);
                }
            }
        }
    }

    // For visualizing bounds in editor
    private void OnDrawGizmos()
    {
        // Draw room bounds
        if (overrideCameraBounds)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Gizmos.DrawCube(
                new Vector3((customLowerBounds.x + customUpperBounds.x) * 0.5f, (customLowerBounds.y + customUpperBounds.y) * 0.5f, 0),
                new Vector3(customUpperBounds.x - customLowerBounds.x, customUpperBounds.y - customLowerBounds.y, 0)
            );
        }
        else if (GetComponent<BoxCollider2D>() != null)
        {
            Bounds bounds = GetComponent<BoxCollider2D>().bounds;
            Vector2 min = new Vector2(bounds.min.x + boundsPadding.x, bounds.min.y + boundsPadding.y);
            Vector2 max = new Vector2(bounds.max.x - boundsPadding.x, bounds.max.y - boundsPadding.y);

            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Gizmos.DrawCube(
                new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0),
                new Vector3(max.x - min.x, max.y - min.y, 0)
            );
        }
    }
}