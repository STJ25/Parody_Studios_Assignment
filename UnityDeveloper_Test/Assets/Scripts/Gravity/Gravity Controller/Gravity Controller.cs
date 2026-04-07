using UnityEngine;
using UnityEngine.InputSystem;

public class GravityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Gravity Settings")]
    [SerializeField] private float gravityStrength = 25f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Hologram Indicator")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowHeightOffset = 2f;
    [SerializeField] private float arrowRotationSpeed = 10f;

    public enum GravityState { Idle, Selecting }
    private GravityState state = GravityState.Idle;

    private Vector3 currentGravityDirection = Vector3.down;
    private Vector3 selectedGravityDirection = Vector3.down;

    private bool isRotating = false;
    private Quaternion targetRotation = Quaternion.identity;
    private Quaternion arrowTargetRotation = Quaternion.identity;

    private PlayerInput playerInput;
    private InputAction gravityModeAction;
    private InputAction gravitySelectAction;
    private InputAction gravityConfirmAction;

    private Rigidbody rb;
    private GameObject arrowInstance;

    public System.Action<Vector3> OnGravityChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        rb.useGravity = false;

        if (arrowPrefab != null)
        {
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
        if (state == GravityState.Selecting) UpdateArrow();
    }

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

    private void OnGravityConfirm(InputAction.CallbackContext ctx)
    {
        if (state != GravityState.Selecting) return;
        HideArrow();
        ShiftGravity(selectedGravityDirection);
        state = GravityState.Idle;
    }

    private void ShiftGravity(Vector3 newDirection)
    {
        currentGravityDirection = newDirection.normalized;
        Vector3 newUp = -currentGravityDirection;

        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, newUp);
        if (projectedForward.sqrMagnitude < 0.01f)
            projectedForward = Vector3.ProjectOnPlane(transform.right, newUp);

        targetRotation = Quaternion.LookRotation(projectedForward.normalized, newUp);
        isRotating = true;

        rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, newUp);

        OnGravityChanged?.Invoke(currentGravityDirection);
        Debug.Log($"[Gravity] Shifted to: {currentGravityDirection}");
    }

    private void SmoothRotateToTarget()
    {
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));

        if (Quaternion.Angle(rb.rotation, targetRotation) < 0.5f)
        {
            rb.MoveRotation(targetRotation);
            isRotating = false;
        }
    }

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

    private Quaternion GetArrowRotationFor(Vector3 gravityDirection)
    {
        // Arrow forward points toward the target surface (the new gravity direction)
        // Up is the current gravity up so it doesn't gimbal when selecting same-axis directions
        Vector3 gravityUp    = -currentGravityDirection.normalized;
        Vector3 arrowForward = gravityDirection.normalized;

        // Guard against forward and up being parallel (e.g. selecting straight down on default gravity)
        if (Mathf.Abs(Vector3.Dot(arrowForward, gravityUp)) > 0.99f)
            gravityUp = Vector3.ProjectOnPlane(transform.forward, arrowForward).normalized;

        return Quaternion.LookRotation(arrowForward, gravityUp);
    }

    public Vector3 GetGravityDirection() => currentGravityDirection.normalized;
    public Vector3 GetGravityUp() => -currentGravityDirection.normalized;
    public float GetGravityStrength() => gravityStrength;
    public bool IsSelecting() => state == GravityState.Selecting;
    public bool IsRotating() => isRotating;
}