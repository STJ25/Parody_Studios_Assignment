using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform pivot;
    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalMinClamp = -30f;
    [SerializeField] private float verticalMaxClamp = 60f;
    [SerializeField] private bool invertY = false;
    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.05f;

    private PlayerInput playerInput;
    private GravityController gravityController;
    private InputAction lookAction;
    private float yaw;
    private float pitch;
    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;
    private float pitchVelocity;

    // Encodes current gravity orientation so yaw/pitch orbit the correct up axis
    private Quaternion gravityBaseRotation = Quaternion.identity;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        gravityController = GetComponent<GravityController>();
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        if (pivot == null) { Debug.LogError("Pivot is not assigned.", this); return; }
        yaw = pivot.eulerAngles.y;
        currentYaw = yaw;
    }

    private void OnEnable()
    {
        lookAction = playerInput.actions["Look"];
        if (lookAction == null) Debug.LogError("Look action not found.", this);
        if (gravityController != null) gravityController.OnGravityChanged += OnGravityChanged;
    }

    private void OnDisable()
    {
        lookAction = null;
        if (gravityController != null) gravityController.OnGravityChanged -= OnGravityChanged;
    }

    private void OnGravityChanged(Vector3 newGravityDirection)
    {
        Vector3 newGravityUp = -newGravityDirection.normalized;

        // Re-anchor the base rotation to the pivot's current forward on the new gravity plane
        Vector3 projectedForward = Vector3.ProjectOnPlane(pivot.forward, newGravityUp).normalized;
        if (projectedForward.sqrMagnitude < 0.001f)
            projectedForward = Vector3.ProjectOnPlane(Vector3.forward, newGravityUp).normalized;
        
        gravityBaseRotation = Quaternion.LookRotation(projectedForward, newGravityUp);
        yaw = 0f; currentYaw = 0f;
        pitch = 0f; currentPitch = 0f;
        yawVelocity = 0f; pitchVelocity = 0f;
    }

    private void Update()
    {
        HandleMouseLook();
    }

    private void HandleMouseLook()
    {
        if (lookAction == null) return;
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        yaw   += lookInput.x * mouseSensitivity;
        pitch += lookInput.y * mouseSensitivity * (invertY ? 1f : -1f);
        pitch = Mathf.Clamp(pitch, verticalMinClamp, verticalMaxClamp);
        currentYaw   = Mathf.SmoothDampAngle(currentYaw,   yaw,   ref yawVelocity,   smoothTime);
        currentPitch = Mathf.SmoothDampAngle(currentPitch, pitch, ref pitchVelocity, smoothTime);
        // gravityBaseRotation orients yaw/pitch to work relative to the current gravity up axis
        pivot.rotation = gravityBaseRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
    }
}