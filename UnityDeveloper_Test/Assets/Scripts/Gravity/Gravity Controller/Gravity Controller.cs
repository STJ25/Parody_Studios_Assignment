using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the player's custom gravity system.
/// Owns the gravity direction state machine (Idle / Selecting) and is the
/// single source of truth for gravity direction and strength.
///
/// Responsibilities:
///   - Handles player input for gravity mode, direction selection, and confirmation.
///   - Rotates the player body to align with the new gravity up axis on shift.
///   - Manages the hologram arrow indicator that previews the selected gravity direction.
///   - Fires <see cref="OnGravityChanged"/> so dependent scripts can react to gravity shifts.
///
/// Does NOT apply gravity forces — that is handled by <see cref="ThirdPersonController"/>.
/// </summary>

public class GravityController : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [Tooltip("The camera transform used to resolve selected gravity direction relative to the player's view.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Gravity Settings")]
    [Tooltip("Acceleration applied in the gravity direction each FixedUpdate, in m/s².")]
    [SerializeField] private float gravityStrength = 25f;

    [Tooltip("Speed at which the player body rotates to align with the new gravity up axis after a gravity shift.")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Hologram Indicator")]
    [Tooltip("Arrow mesh prefab instantiated once at runtime to preview the selected gravity direction. Assign a prefab whose mesh Z-axis points in the intended direction.")]
    [SerializeField] private GameObject arrowPrefab;

    [Tooltip("Distance above the player (along current gravity up) at which the arrow indicator floats.")]
    [SerializeField] private float arrowHeightOffset = 2f;

    [Tooltip("Speed at which the arrow smoothly rotates to preview a newly selected direction.")]
    [SerializeField] private float arrowRotationSpeed = 10f;

    #endregion

    #region State

    /// <summary>
    /// Represents the two operating states of the gravity selection system.
    /// <list type="bullet">
    ///   <item><term>Idle</term><description>Normal gameplay. No gravity selection in progress.</description></item>
    ///   <item><term>Selecting</term><description>Player is previewing a new gravity direction. Arrow is visible.</description></item>
    /// </list>
    /// </summary>
    public enum GravityState { Idle, Selecting }
    private GravityState state = GravityState.Idle;

    private Vector3 currentGravityDirection  = Vector3.down;
    private Vector3 selectedGravityDirection = Vector3.down;

    private bool isRotating = false;
    private Quaternion targetRotation      = Quaternion.identity;
    private Quaternion arrowTargetRotation = Quaternion.identity;

    #endregion

    #region Private References

    private PlayerInput playerInput;
    private InputAction gravityModeAction;
    private InputAction gravitySelectAction;
    private InputAction gravityConfirmAction;

    private Rigidbody rb;
    private GameObject arrowInstance;

    #endregion

    #region Events

    /// <summary>
    /// Fired immediately after the gravity direction is confirmed and applied.
    /// Passes the new normalized gravity direction vector (pointing toward the new floor).
    /// Subscribed to by <see cref="ThirdPersonController"/> and <see cref="CameraController"/>.
    /// </summary>
    public System.Action<Vector3> OnGravityChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        rb.useGravity = false;

        if (arrowPrefab != null)
        {
            // Instantiate once and disable — cheaper than instantiate/destroy each selection session.
            arrowInstance = Instantiate(arrowPrefab);
            arrowInstance.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[Gravity] No arrow prefab assigned.", this);
        }
    }

    private void OnEnable()
    {
        gravityModeAction    = playerInput.actions["GravityMode"];
        gravitySelectAction  = playerInput.actions["GravitySelect"];
        gravityConfirmAction = playerInput.actions["GravityConfirm"];

        // Log errors before subscribing so they can fire even if the action is null.
        if (gravityModeAction == null)    Debug.LogError("GravityMode action not found.", this);
        if (gravitySelectAction == null)  Debug.LogError("GravitySelect action not found.", this);
        if (gravityConfirmAction == null) Debug.LogError("GravityConfirm action not found.", this);

        if (gravityModeAction != null)    gravityModeAction.performed    += OnGravityMode;
        if (gravitySelectAction != null)  gravitySelectAction.performed  += OnGravitySelect;
        if (gravityConfirmAction != null) gravityConfirmAction.performed += OnGravityConfirm;
    }

    private void OnDisable()
    {
        if (gravityModeAction != null)    gravityModeAction.performed    -= OnGravityMode;
        if (gravitySelectAction != null)  gravitySelectAction.performed  -= OnGravitySelect;
        if (gravityConfirmAction != null) gravityConfirmAction.performed -= OnGravityConfirm;
    }

    private void FixedUpdate()
    {
        if (isRotating) SmoothRotateToTarget();
    }

    private void Update()
    {
        // Arrow tracking is Update-driven so it stays smooth and frame-rate independent.
        if (state == GravityState.Selecting) UpdateArrow();
    }

    #endregion

    #region Input Callbacks

    /// <summary>
    /// Toggles gravity selection mode on/off (default: E key).
    /// Entering Selecting state shows the arrow and seeds it with the current direction.
    /// Exiting without confirming cancels the selection with no gravity change.
    /// </summary>
    private void OnGravityMode(InputAction.CallbackContext ctx)
    {
        if (state == GravityState.Idle)
        {
            state = GravityState.Selecting;
            selectedGravityDirection = currentGravityDirection;
            arrowTargetRotation = GetArrowRotationFor(selectedGravityDirection);
            ShowArrow();
            Debug.Log("[Gravity] Selection Mode ON");
        }
        else
        {
            state = GravityState.Idle;
            HideArrow();
            Debug.Log("[Gravity] Selection Mode OFF - cancelled");
        }
    }

    /// <summary>
    /// Updates the previewed gravity direction from a Vector2 directional input (default: arrow keys).
    /// Directions are resolved relative to the camera's current view projected onto the gravity plane,
    /// so the selection always matches what the player sees on screen.
    /// </summary>
    private void OnGravitySelect(InputAction.CallbackContext ctx)
    {
        if (state != GravityState.Selecting) return;

        Vector2 input = ctx.ReadValue<Vector2>();
        Vector3 gravityUp  = -currentGravityDirection.normalized;
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, gravityUp).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   gravityUp).normalized;

        if      (input.y >  0.5f) selectedGravityDirection =  camForward;
        else if (input.y < -0.5f) selectedGravityDirection = -camForward;
        else if (input.x >  0.5f) selectedGravityDirection =  camRight;
        else if (input.x < -0.5f) selectedGravityDirection = -camRight;

        arrowTargetRotation = GetArrowRotationFor(selectedGravityDirection);
        Debug.Log($"[Gravity] Previewing: {selectedGravityDirection}");
    }

    /// <summary>
    /// Confirms the currently previewed gravity direction and applies the shift (default: Enter key).
    /// Hides the arrow, calls <see cref="ShiftGravity"/>, and returns to Idle state.
    /// </summary>
    private void OnGravityConfirm(InputAction.CallbackContext ctx)
    {
        if (state != GravityState.Selecting) return;
        HideArrow();
        ShiftGravity(selectedGravityDirection);
        state = GravityState.Idle;
    }

    #endregion

    #region Gravity Logic

    /// <summary>
    /// Applies the new gravity direction.
    /// Reorients the player body so its up axis matches the new gravity up,
    /// preserves horizontal momentum relative to the new surface,
    /// and notifies all listeners via <see cref="OnGravityChanged"/>.
    /// </summary>
    private void ShiftGravity(Vector3 newDirection)
    {
        currentGravityDirection = newDirection.normalized;
        Vector3 newUp = -currentGravityDirection;

        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, newUp);

        // Fallback if forward is parallel to the new up axis (e.g. looking straight up/down).
        if (projectedForward.sqrMagnitude < 0.01f)
            projectedForward = Vector3.ProjectOnPlane(transform.right, newUp);

        targetRotation = Quaternion.LookRotation(projectedForward.normalized, newUp);
        isRotating = true;

        // Project existing velocity onto the new horizontal plane so momentum
        // carries over naturally without launching the player into the air.
        rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, newUp);

        OnGravityChanged?.Invoke(currentGravityDirection);
        Debug.Log($"[Gravity] Shifted to: {currentGravityDirection}");
    }

    /// <summary>
    /// Smoothly rotates the Rigidbody toward <see cref="targetRotation"/> each FixedUpdate.
    /// Snaps to the exact target when within 0.5 degrees to avoid infinite asymptotic drift.
    /// </summary>
    private void SmoothRotateToTarget()
    {
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));

        if (Quaternion.Angle(rb.rotation, targetRotation) < 0.5f)
        {
            rb.MoveRotation(targetRotation);
            isRotating = false;
        }
    }

    #endregion

    #region Arrow Indicator

    private void ShowArrow()
    {
        if (arrowInstance == null) return;
        arrowInstance.SetActive(true);
    }

    private void HideArrow()
    {
        if (arrowInstance == null) return;
        arrowInstance.SetActive(false);
    }

    /// <summary>
    /// Repositions and smoothly rotates the arrow indicator each frame while in Selecting state.
    /// Position is offset along the current gravity up axis so it always floats
    /// above the player's head regardless of which way gravity is pointing.
    /// </summary>
    private void UpdateArrow()
    {
        if (arrowInstance == null) return;

        Vector3 gravityUp = -currentGravityDirection.normalized;
        arrowInstance.transform.position = transform.position + gravityUp * arrowHeightOffset;
        arrowInstance.transform.rotation = Quaternion.Slerp(
            arrowInstance.transform.rotation,
            arrowTargetRotation,
            arrowRotationSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Computes the world rotation the arrow should face for a given gravity direction.
    /// Arrow forward is set to the selected gravity direction so it visually points
    /// toward the surface the player will fall onto.
    /// </summary>
    private Quaternion GetArrowRotationFor(Vector3 gravityDirection)
    {
        Vector3 gravityUp    = -currentGravityDirection.normalized;
        Vector3 arrowForward = gravityDirection.normalized;

        // Guard: if arrowForward and gravityUp are parallel (e.g. selecting straight down
        // on default gravity), LookRotation would produce NaN. Use a projected fallback instead.
        if (Mathf.Abs(Vector3.Dot(arrowForward, gravityUp)) > 0.99f)
            gravityUp = Vector3.ProjectOnPlane(transform.forward, arrowForward).normalized;

        return Quaternion.LookRotation(arrowForward, gravityUp);
    }

    #endregion

    #region Public API

    /// <summary>Returns the current normalized gravity direction vector (pointing toward the floor).</summary>
    public Vector3 GetGravityDirection() => currentGravityDirection.normalized;

    /// <summary>Returns the current normalized gravity up vector (pointing away from the floor).</summary>
    public Vector3 GetGravityUp() => -currentGravityDirection.normalized;

    /// <summary>Returns the gravity acceleration magnitude in m/s².</summary>
    public float GetGravityStrength() => gravityStrength;

    /// <summary>Returns true while the player is in gravity selection mode (arrow visible).</summary>
    public bool IsSelecting() => state == GravityState.Selecting;

    /// <summary>Returns true while the player body is smoothly rotating to align with a new gravity up axis.</summary>
    public bool IsRotating() => isRotating;

    #endregion
}