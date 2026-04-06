using UnityEngine;
using UnityEngine.InputSystem;

public class GravityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Gravity Settings")]
    [SerializeField] private float gravityStrength = 9.81f;
    [SerializeField] private float rotationSpeed = 5f;

    // States declaration
    public enum GravityState { Idle, Selecting }
    private GravityState state = GravityState.Idle;

    private Vector3 currentGravityDirection = Vector3.down;
    private Vector3 selectedGravityDirection = Vector3.down;

    private bool isRotating = false;
    private Quaternion targetRotation = Quaternion.identity;

    // Player inputs here 
    private PlayerInput playerInput;
    private InputAction gravityModeAction;
    private InputAction gravitySelectAction;
    private InputAction gravityConfirmAction;

    private Rigidbody rb;

    public System.Action<Vector3> OnGravityChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();

        rb.useGravity = false; // We apply gravity manually
    }

    private void OnEnable()
    {
        gravityModeAction = playerInput.actions["GravityMode"];
        gravitySelectAction = playerInput.actions["GravitySelect"];
        gravityConfirmAction = playerInput.actions["GravityConfirm"];

        if (gravityModeAction!= null) gravityModeAction.performed += OnGravityMode;
        if (gravitySelectAction!= null) gravitySelectAction.performed += OnGravitySelect;
        if (gravityConfirmAction!= null) gravityConfirmAction.performed += OnGravityConfirm;

        if (gravityModeAction == null) Debug.LogError("GravityMode action not found.",this);
        if (gravitySelectAction == null) Debug.LogError("GravitySelect action not found.",this);
        if (gravityConfirmAction == null) Debug.LogError("GravityConfirm action not found.",this);
    }

    private void OnDisable()
    {
        if (gravityModeAction!= null) gravityModeAction.performed -= OnGravityMode;
        if (gravitySelectAction!= null) gravitySelectAction.performed -= OnGravitySelect;
        if (gravityConfirmAction!= null) gravityConfirmAction.performed -= OnGravityConfirm;
    }

    private void FixedUpdate()
    {
        ApplyGravity();

        if (isRotating)
            SmoothRotateToTarget();
    }


    private void OnGravityMode(InputAction.CallbackContext ctx)
    {
        // E toggle
        if (state == GravityState.Idle)
        {
            state = GravityState.Selecting;
            selectedGravityDirection = currentGravityDirection;
            Debug.Log("[Gravity] Selection Mode ON — use arrow keys then Enter.");
        }
        else
        {
            state = GravityState.Idle;
            Debug.Log("[Gravity] Selection Mode OFF — cancelled.");
        }
    }

    private void OnGravitySelect(InputAction.CallbackContext ctx)
    {
        if (state != GravityState.Selecting) return;

        Vector2 input = ctx.ReadValue<Vector2>();

        // Project camera axes onto the current gravity plane taht is selected
        Vector3 gravityUp  = -currentGravityDirection.normalized;
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, gravityUp).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   gravityUp).normalized;

        if      (input.y >  0.5f) selectedGravityDirection =  camForward; 
        else if (input.y < -0.5f) selectedGravityDirection = -camForward; 
        else if (input.x >  0.5f) selectedGravityDirection =  camRight;  
        else if (input.x < -0.5f) selectedGravityDirection = -camRight;  

        Debug.Log($"[Gravity] Previewing direction: {selectedGravityDirection}");
    }

    private void OnGravityConfirm(InputAction.CallbackContext ctx)
    {
        if (state != GravityState.Selecting) return;

        ShiftGravity(selectedGravityDirection);
        state = GravityState.Idle;
    }

    private void ShiftGravity(Vector3 newDirection)
    {
        currentGravityDirection = newDirection.normalized;

        // Zero out velocity so the player doesn't carry momentum - player just moves without any input
        rb.linearVelocity = Vector3.zero;
        Vector3 newUp = -currentGravityDirection;

        // Project current forward onto the new horizontal plane so that he dosen't tumble
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, newUp);

        if (projectedForward.sqrMagnitude < 0.01f)
            projectedForward = Vector3.ProjectOnPlane(transform.right, newUp);

        targetRotation = Quaternion.LookRotation(projectedForward.normalized, newUp);
        isRotating     = true;

        // Notify ThirdPersonController so it can update
        OnGravityChanged?.Invoke(currentGravityDirection);

        Debug.Log($"[Gravity] Shifted to: {currentGravityDirection}");
    }

    private void ApplyGravity()
    {
        rb.AddForce(currentGravityDirection * gravityStrength, ForceMode.Acceleration);
    }

    private void SmoothRotateToTarget()
    {
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));

        // Snap to target
        if (Quaternion.Angle(rb.rotation, targetRotation) < 0.5f)
        {
            rb.MoveRotation(targetRotation);
            isRotating = false;
        }
    }

    // Public things that the Player controller references
    public Vector3 GetGravityDirection() => currentGravityDirection;
    public Vector3 GetGravityUp() => -currentGravityDirection.normalized;
    public bool IsSelecting() => state == GravityState.Selecting;
}