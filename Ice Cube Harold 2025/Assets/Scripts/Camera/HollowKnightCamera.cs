using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class HollowKnightCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool findPlayerOnStart = true;

    [Header("Following")]
    [SerializeField] private float horizontalSmoothTime = 0.2f;
    [SerializeField] private float verticalSmoothTime = 0.3f;
    [SerializeField] private float fallingSmoothTime = 0.1f;
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSmoothTime = 0.5f;
    [SerializeField] private float verticalOffset = 1f;

    [Header("Bounds")]
    [SerializeField] private bool useManualBounds = false;
    [SerializeField] private Vector2 manualLowerBounds;
    [SerializeField] private Vector2 manualUpperBounds;
    [SerializeField] private bool autoDetectPlatforms = true;
    [SerializeField] private LayerMask platformLayerMask;

    [Header("Room Transitions")]
    [SerializeField] private float roomTransitionTime = 0.7f;
    [SerializeField] private AnimationCurve roomTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Camera state
    private Vector3 currentVelocity = Vector3.zero;
    private float lookAheadDir = 0;
    private float targetLookAheadX = 0;
    private float currentLookAheadX = 0;
    private float lookAheadVelocity = 0;
    private Vector2 lowerBounds;
    private Vector2 upperBounds;
    private bool isTransitioning = false;
    private Camera mainCamera;
    private float cameraHalfHeight;
    private float cameraHalfWidth;
    private float previousTargetPositionX;
    private bool wasFalling = false;
    private Rigidbody2D targetRigidbody;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();

        // Calculate camera dimensions
        cameraHalfHeight = mainCamera.orthographicSize;
        cameraHalfWidth = cameraHalfHeight * mainCamera.aspect;

        if (useManualBounds)
        {
            SetBounds(manualLowerBounds, manualUpperBounds);
        }
        else
        {
            // Set large default bounds if not using manual bounds
            SetBounds(new Vector2(-1000, -1000), new Vector2(1000, 1000));
        }
    }

    private void Start()
    {
        if (findPlayerOnStart)
        {
            FindAndSetTarget();
        }

        if (target != null)
        {
            previousTargetPositionX = target.position.x;
            targetRigidbody = target.GetComponent<Rigidbody2D>();
            transform.position = CalculateTargetPosition();
        }
        else
        {
            Debug.LogWarning("HollowKnightCamera: No target found. Camera will remain stationary.");
        }

        if (autoDetectPlatforms)
        {
            DetectLevelBounds();
        }
    }

    private void FindAndSetTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            target = player.transform;
            Debug.Log("HollowKnightCamera: Found player with tag '" + playerTag + "'");
        }
        else
        {
            Debug.LogWarning("HollowKnightCamera: Could not find player with tag '" + playerTag + "'");
        }
    }

    private void DetectLevelBounds()
    {
        // This is a simple implementation. You might want to expand this based on your level design
        Collider2D[] platforms = Physics2D.OverlapAreaAll(
            new Vector2(-1000, -1000),
            new Vector2(1000, 1000),
            platformLayerMask
        );

        if (platforms.Length > 0)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (Collider2D platform in platforms)
            {
                Bounds bounds = platform.bounds;
                min.x = Mathf.Min(min.x, bounds.min.x);
                min.y = Mathf.Min(min.y, bounds.min.y);
                max.x = Mathf.Max(max.x, bounds.max.x);
                max.y = Mathf.Max(max.y, bounds.max.y);
            }

            // Add some padding
            min -= new Vector2(5f, 5f);
            max += new Vector2(5f, 5f);

            SetBounds(min, max);
            Debug.Log("HollowKnightCamera: Auto-detected level bounds: " + min + " to " + max);
        }
    }

    private void LateUpdate()
    {
        // If target is null, try to find it again
        if (target == null)
        {
            FindAndSetTarget();
            if (target == null) return;
        }

        if (isTransitioning)
            return;

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPosition = CalculateTargetPosition();

        // Detect change in movement direction
        float directionChange = Mathf.Sign(target.position.x - previousTargetPositionX);
        previousTargetPositionX = target.position.x;

        // Look ahead system
        if (directionChange != 0 && directionChange != lookAheadDir)
        {
            lookAheadDir = directionChange;
        }
        else if (directionChange == 0)
        {
            // Gradually reduce look ahead when not moving
            targetLookAheadX = Mathf.MoveTowards(targetLookAheadX, 0, Time.deltaTime);
        }

        // Adjust look ahead distance
        if (lookAheadDir != 0)
        {
            targetLookAheadX = lookAheadDir * lookAheadDistance;
        }

        // Smoothly adjust look ahead
        currentLookAheadX = Mathf.SmoothDamp(currentLookAheadX, targetLookAheadX, ref lookAheadVelocity, lookAheadSmoothTime);

        // Apply look ahead
        targetPosition.x += currentLookAheadX;

        // Get vertical damping based on falling state
        float verticalDamping = verticalSmoothTime;

        // Detect falling
        bool isFalling = false;
        if (targetRigidbody != null)
        {
            isFalling = targetRigidbody.linearVelocity.y < -0.5f;
        }

        if (isFalling && !wasFalling)
        {
            verticalDamping = fallingSmoothTime;
        }
        wasFalling = isFalling;

        // Apply smooth damping with different horizontal/vertical values
        Vector3 smoothedPosition = new Vector3(
            Mathf.SmoothDamp(transform.position.x, targetPosition.x, ref currentVelocity.x, horizontalSmoothTime),
            Mathf.SmoothDamp(transform.position.y, targetPosition.y, ref currentVelocity.y, verticalDamping),
            targetPosition.z
        );

        // Constrain to bounds
        smoothedPosition = ConstrainToBounds(smoothedPosition);

        // Apply position
        transform.position = smoothedPosition;
    }

    private Vector3 CalculateTargetPosition()
    {
        return new Vector3(
            target.position.x,
            target.position.y + verticalOffset,
            transform.position.z
        );
    }

    private Vector3 ConstrainToBounds(Vector3 position)
    {
        // Constrain position to bounds while accounting for camera size
        position.x = Mathf.Clamp(position.x, lowerBounds.x + cameraHalfWidth, upperBounds.x - cameraHalfWidth);
        position.y = Mathf.Clamp(position.y, lowerBounds.y + cameraHalfHeight, upperBounds.y - cameraHalfHeight);

        return position;
    }

    public void SetBounds(Vector2 lower, Vector2 upper)
    {
        lowerBounds = lower;
        upperBounds = upper;

        // If bounds are invalid (upper < lower), use large default bounds
        if (upperBounds.x < lowerBounds.x)
        {
            lowerBounds.x = -1000;
            upperBounds.x = 1000;
        }

        if (upperBounds.y < lowerBounds.y)
        {
            lowerBounds.y = -1000;
            upperBounds.y = 1000;
        }

        // Move camera immediately if outside of new bounds
        if (!isTransitioning)
        {
            Vector3 constrainedPosition = ConstrainToBounds(transform.position);
            if (constrainedPosition != transform.position)
            {
                transform.position = constrainedPosition;
            }
        }
    }

    public void TransitionToPosition(Vector3 targetPosition, float customDuration = -1)
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionRoutine(targetPosition, customDuration > 0 ? customDuration : roomTransitionTime));
        }
    }

    public void TransitionToBounds(Vector2 lower, Vector2 upper, float customDuration = -1)
    {
        SetBounds(lower, upper);

        // Calculate center position respecting the new bounds
        Vector3 centerPosition = new Vector3(
            (lower.x + upper.x) * 0.5f,
            (lower.y + upper.y) * 0.5f,
            transform.position.z
        );

        TransitionToPosition(ConstrainToBounds(centerPosition), customDuration);
    }

    private IEnumerator TransitionRoutine(Vector3 targetPosition, float duration)
    {
        isTransitioning = true;
        Vector3 startPosition = transform.position;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = roomTransitionCurve.Evaluate(elapsedTime / duration);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        isTransitioning = false;
    }

    // Public methods for use by game systems
    public void FocusOnTarget()
    {
        if (target != null && !isTransitioning)
        {
            transform.position = ConstrainToBounds(CalculateTargetPosition());
            currentVelocity = Vector3.zero;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            previousTargetPositionX = target.position.x;
            targetRigidbody = target.GetComponent<Rigidbody2D>();
        }
    }

    // Called when player is teleporting or respawning
    public void SnapToTarget()
    {
        if (target != null)
        {
            transform.position = ConstrainToBounds(CalculateTargetPosition());
            currentVelocity = Vector3.zero;
            lookAheadDir = 0;
            currentLookAheadX = 0;
            targetLookAheadX = 0;
        }
    }

    // For visualizing bounds in editor
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && useManualBounds)
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawWireCube(
                new Vector3((manualLowerBounds.x + manualUpperBounds.x) * 0.5f, (manualLowerBounds.y + manualUpperBounds.y) * 0.5f, 0),
                new Vector3(manualUpperBounds.x - manualLowerBounds.x, manualUpperBounds.y - manualLowerBounds.y, 0)
            );
        }
        else if (Application.isPlaying)
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawWireCube(
                new Vector3((lowerBounds.x + upperBounds.x) * 0.5f, (lowerBounds.y + upperBounds.y) * 0.5f, 0),
                new Vector3(upperBounds.x - lowerBounds.x, upperBounds.y - lowerBounds.y, 0)
            );
        }
    }
}