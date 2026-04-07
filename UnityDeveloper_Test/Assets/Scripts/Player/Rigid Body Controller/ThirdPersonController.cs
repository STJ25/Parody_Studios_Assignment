using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all player movement, rotation, jumping, and animation for a third-person character.
/// Designed to work with any gravity direction supplied by <see cref="GravityController"/>.
/// All physics operations run in FixedUpdate for frame-rate independence.
///
/// Responsibilities:
///   - Reads movement and jump input via the Unity Input System.
///   - Applies camera-relative movement projected onto the current gravity plane.
///   - Performs a SphereCast ground check relative to the current gravity up axis.
///   - Applies custom gravity and enforces a max fall speed.
///   - Handles jump buffering for responsive feel.
///   - Drives Animator parameters for movement and grounded state.
///
/// Does NOT own gravity direction or strength — those are owned by <see cref="GravityController"/>.
/// </summary>

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(GravityController))]
public class ThirdPersonController : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [Tooltip("The camera transform used to resolve camera-relative movement direction.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement Settings")]
    [Tooltip("Maximum horizontal movement speed in m/s.")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("Speed at which the player body rotates to face the movement direction.")]
    [SerializeField] private float rotationSpeed = 10f;

    [Tooltip("Rate of acceleration toward target horizontal velocity. Higher values feel snappier.")]
    [SerializeField] private float acceleration = 20f;

    [Header("Jump Settings")]
    [Tooltip("Vertical impulse force applied along the gravity up axis on jump.")]
    [SerializeField] private float jumpForce = 8f;

    [Tooltip("Time window in seconds during which a jump input is buffered before landing.")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Tooltip("Radius of the SphereCast used for ground detection.")]
    [SerializeField] private float groundCheckRadius = 0.3f;

    [Tooltip("Cast distance below the player origin used to determine if grounded.")]
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Tooltip("Layer mask defining what surfaces count as ground.")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Gravity")]
    [Tooltip("Maximum speed the player can fall in the gravity direction, in m/s.")]
    [SerializeField] private float maxFallSpeed = 40f;

    [Header("Animation")]
    [Tooltip("Animator component on the player mesh. If null, animation updates are skipped.")]
    [SerializeField] private Animator animator;

    #endregion

    #region Private Variables

    // Pre-hashed animator parameter IDs — avoids string lookups every frame
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    // Cached component references
    private Rigidbody rb;
    private PlayerInput playerInput;
    private GravityController gravityController;

    // Input actions resolved from the PlayerInput component
    private InputAction moveAction;
    private InputAction jumpAction;

    // Runtime state
    private Vector2 moveInput;
    private Vector3 currentHorizontalVelocity;
    private float jumpBufferCounter;
    private bool isGrounded;

    #endregion

    #region Unity Cycle
    /// <summary>
    /// Caches component references and configures the Rigidbody.
    /// Gravity is disabled here since <see cref="GravityController"/> owns gravity application.
    /// </summary>

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        gravityController = GetComponent<GravityController>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.useGravity = false;
    }

    /// <summary>
    /// Subscribes to input actions and listens for gravity change events.
    /// Input actions are resolved by name from the PlayerInput component.
    /// </summary>

    private void OnEnable()
    {
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];

        if (moveAction != null) { moveAction.performed += OnMove; moveAction.canceled += OnMoveCanceled; }
        else Debug.LogError("Move action not found.", this);

        if (jumpAction != null) jumpAction.performed += OnJump;
        else Debug.LogError("Jump action not found.", this);

        gravityController.OnGravityChanged += OnGravityChanged;
    }

    /// <summary>
    /// Unsubscribes all input and gravity event listeners to prevent ghost callbacks.
    /// </summary>
    private void OnDisable()
    {
        if (moveAction != null) { moveAction.performed -= OnMove; moveAction.canceled -= OnMoveCanceled; }
        if (jumpAction != null) jumpAction.performed -= OnJump;

        gravityController.OnGravityChanged -= OnGravityChanged;
    }

    // Store raw movement input each frame
    private void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    // Zero out input when the move action is released
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;

    // Seed the jump buffer — allows jump input slightly before landing
    private void OnJump(InputAction.CallbackContext ctx) => jumpBufferCounter = jumpBufferTime;

    // Reset horizontal velocity on gravity shift to prevent carrying incorrect momentum
    private void OnGravityChanged(Vector3 _) => currentHorizontalVelocity = Vector3.zero;

    /// <summary>
    /// Main physics loop. Runs all movement systems in a fixed order each FixedUpdate.
    /// Order matters — ground check must run before gravity and jump.
    /// </summary>
    private void FixedUpdate()
    {
        Vector3 gravityUp = gravityController.GetGravityUp();

        jumpBufferCounter -= Time.fixedDeltaTime;

        GroundCheck(gravityUp);
        ApplyGravity(gravityUp);
        Movement(gravityUp);
        Rotation(gravityUp);
        Jump(gravityUp);
        UpdateAnimation(gravityUp);
    }

    #endregion

    #region Gravity and Ground Check
    /// <summary>
    /// Casts a sphere downward along the current gravity direction to determine if the player is grounded.
    /// Uses a SphereCast rather than a Raycast for more forgiving edge detection on uneven surfaces.
    /// </summary>
    private void GroundCheck(Vector3 gravityUp)
    {
        Vector3 origin = transform.position + gravityUp * 0.1f;
        isGrounded = Physics.SphereCast(origin, groundCheckRadius, -gravityUp, out RaycastHit _, groundCheckDistance + 0.1f, groundLayer);
    }

    /// <summary>
    /// Applies custom gravity each FixedUpdate and clamps fall speed to <see cref="maxFallSpeed"/>.
    /// When grounded, cancels any negative vertical velocity to prevent the player sinking into the floor.
    /// Gravity direction and strength are sourced from <see cref="GravityController"/>.
    /// </summary>
    private void ApplyGravity(Vector3 gravityUp)
    {
        Vector3 vel = rb.linearVelocity;
        float verticalSpeed = Vector3.Dot(vel, gravityUp);
        if (isGrounded)
        {
            if (verticalSpeed < 0f)
            {
                vel -= gravityUp * verticalSpeed;
                rb.linearVelocity = vel;
            }
            return;
        }
        
        vel += gravityController.GetGravityDirection() * gravityController.GetGravityStrength() * Time.fixedDeltaTime;
        
        float newVerticalSpeed = Vector3.Dot(vel, gravityUp);
        
        if (newVerticalSpeed < -maxFallSpeed)
            vel -= gravityUp * (newVerticalSpeed + maxFallSpeed);
        
        rb.linearVelocity = vel;
    }

    #endregion

    #region Movement Rotation & Jump
    /// <summary>
    /// Resolves camera-relative horizontal movement and smoothly accelerates toward target velocity.
    /// Movement is projected onto the current gravity plane so it works regardless of gravity direction.
    /// Vertical velocity is preserved separately and recombined to avoid overwriting jump/fall state.
    /// </summary>
    private void Movement(Vector3 gravityUp)
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, gravityUp).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, gravityUp).normalized;
        Vector3 targetHorizontal = inputDir.magnitude > 0.1f? (camForward * inputDir.z + camRight * inputDir.x) * moveSpeed : Vector3.zero;
        currentHorizontalVelocity = Vector3.Lerp(currentHorizontalVelocity, targetHorizontal, 1f - Mathf.Exp(-acceleration * Time.fixedDeltaTime));
        Vector3 verticalVel = Vector3.Project(rb.linearVelocity, gravityUp);
        rb.linearVelocity = currentHorizontalVelocity + verticalVel;
    }

    /// <summary>
    /// Rotates the player to face the current movement direction relative to the camera.
    /// Skipped while <see cref="GravityController"/> is rotating the body after a gravity shift
    /// to avoid fighting the gravity reorientation rotation.
    /// </summary>
    private void Rotation(Vector3 gravityUp)
    {
        if (gravityController.IsRotating()) return;
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDir.sqrMagnitude < 0.01f) return;
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, gravityUp).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right, gravityUp).normalized;
        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;
        Quaternion targetRotation = Quaternion.LookRotation(moveDir, gravityUp);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    /// <summary>
    /// Applies a jump impulse along the gravity up axis when grounded and within the jump buffer window.
    /// Strips existing vertical velocity before applying the impulse for consistent jump height.
    /// </summary>
    private void Jump(Vector3 gravityUp)
    {
        if (jumpBufferCounter <= 0f || !isGrounded) return;
        jumpBufferCounter = 0f;
        Vector3 vel = rb.linearVelocity;
        vel -= gravityUp * Vector3.Dot(vel, gravityUp);
        vel += gravityUp * jumpForce;
        rb.linearVelocity = vel;
    }

    #endregion
    
    /// <summary>
    /// Updates the Animator's Speed and IsGrounded parameters each FixedUpdate.
    /// Speed is normalized against <see cref="moveSpeed"/> so the animator is always in 0–1 range.
    /// Dampened with a 0.1s smoothing time to avoid jittery blend tree transitions.
    /// </summary>
    private void UpdateAnimation(Vector3 gravityUp)
    {
        float horizontalSpeed = Vector3.ProjectOnPlane(rb.linearVelocity, gravityUp).magnitude;
        float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / moveSpeed);
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, normalizedSpeed, 0.1f, Time.fixedDeltaTime);
            animator.SetBool(IsGroundedHash, isGrounded);
        }
    }

    /// <summary>
    /// Draws the ground check SphereCast gizmo in the Scene view when this object is selected.
    /// Useful for visually tuning <see cref="groundCheckRadius"/> and <see cref="groundCheckDistance"/>.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (gravityController == null) return;
        Vector3 gravityUp = gravityController.GetGravityUp();
        Vector3 origin = transform.position + gravityUp * 0.1f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawLine(origin, origin + (-gravityUp) * (groundCheckDistance + 0.1f));
    }
}