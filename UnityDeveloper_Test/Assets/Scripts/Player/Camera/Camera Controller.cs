using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person camera controller that orbits around a pivot transform.
/// Supports dynamic gravity by re-aligning its rotational basis whenever gravity changes.
/// 
/// Responsibilities:
/// - Handles mouse input for camera rotation (yaw/pitch)
/// - Smooths camera movement for better feel
/// - Reorients camera based on custom gravity direction
/// 
/// Dependencies:
/// - PlayerInput (New Input System)
/// - GravityController (optional, for dynamic gravity support)
/// </summary>

public class CameraController : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [Tooltip("Pivot point the camera rotates around (usually a child of the player).")]
    [SerializeField] private Transform pivot;

    [Header("Mouse Look")]
    [Tooltip("Sensitivity multiplier for mouse input.")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("Minimum vertical angle (looking down limit).")]
    [SerializeField] private float verticalMinClamp = -30f;

    [Tooltip("Maximum vertical angle (looking up limit).")]
    [SerializeField] private float verticalMaxClamp = 60f;

    [Tooltip("Invert vertical mouse input.")]
    [SerializeField] private bool invertY = false;

    [Header("Smoothing")]
    [Tooltip("Time it takes to smooth camera rotation.")]
    [SerializeField] private float smoothTime = 0.05f;

    #endregion

    #region Private References

    private PlayerInput playerInput;
    private GravityController gravityController;
    private InputAction lookAction;

    #endregion

    #region Rotation State

    // Raw input-driven angles
    private float yaw;
    private float pitch;

    // Smoothed angles
    private float currentYaw;
    private float currentPitch;

    // Velocity refs used by SmoothDamp
    private float yawVelocity;
    private float pitchVelocity;

    /// <summary>
    /// Defines the base rotation aligned with the current gravity up axis.
    /// Ensures camera orbit remains correct regardless of gravity direction.
    /// </summary>
    private Quaternion gravityBaseRotation = Quaternion.identity;

    #endregion

    #region Unity Cycle

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        gravityController = GetComponent<GravityController>();

        if (pivot == null)
        {
            Debug.LogError("Pivot is not assigned.", this);
            return;
        }

        // Initialize yaw from current pivot rotation
        yaw = pivot.eulerAngles.y;
        currentYaw = yaw;
    }

    private void OnEnable()
    {
        lookAction = playerInput.actions["Look"];

        if (lookAction == null)
            Debug.LogError("Look action not found.", this);

        // Subscribe to gravity changes if available
        if (gravityController != null)
            gravityController.OnGravityChanged += OnGravityChanged;
    }

    private void OnDisable()
    {
        lookAction = null;

        if (gravityController != null)
            gravityController.OnGravityChanged -= OnGravityChanged;
    }

    private void Update()
    {
        HandleMouseLook();
    }

    #endregion

    #region Gravity Handling

    /// <summary>
    /// Recalculates the camera's base rotation when gravity changes.
    /// This ensures that yaw/pitch operate relative to the new "up" direction.
    /// </summary>
    private void OnGravityChanged(Vector3 newGravityDirection)
    {
        Vector3 newGravityUp = -newGravityDirection.normalized;

        // Project current forward onto new gravity plane to preserve facing direction
        Vector3 projectedForward = Vector3.ProjectOnPlane(pivot.forward, newGravityUp).normalized;

        // Fallback if projection fails (edge case: looking straight into gravity axis)
        if (projectedForward.sqrMagnitude < 0.001f)
            projectedForward = Vector3.ProjectOnPlane(Vector3.forward, newGravityUp).normalized;

        gravityBaseRotation = Quaternion.LookRotation(projectedForward, newGravityUp);

        // Reset rotation state to avoid snapping artifacts
        yaw = currentYaw = 0f;
        pitch = currentPitch = 0f;
        yawVelocity = pitchVelocity = 0f;
    }

    #endregion

    #region Camera Logic

    /// <summary>
    /// Handles mouse input, applies sensitivity, clamps pitch,
    /// smooths rotation, and applies final rotation to the pivot.
    /// </summary>
    private void HandleMouseLook()
    {
        if (lookAction == null) return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        // Accumulate raw input
        yaw   += lookInput.x * mouseSensitivity;
        pitch += lookInput.y * mouseSensitivity * (invertY ? 1f : -1f);

        // Clamp vertical rotation
        pitch = Mathf.Clamp(pitch, verticalMinClamp, verticalMaxClamp);

        // Smooth rotation over time
        currentYaw   = Mathf.SmoothDampAngle(currentYaw, yaw, ref yawVelocity, smoothTime);
        currentPitch = Mathf.SmoothDampAngle(currentPitch, pitch, ref pitchVelocity, smoothTime);

        // Apply rotation relative to gravity-aligned base
        pivot.rotation = gravityBaseRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    #endregion
}
