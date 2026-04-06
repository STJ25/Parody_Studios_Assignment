using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float acceleration = 20f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Custom Gravity")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float maxFallSpeed = -40f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    private Rigidbody rb;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private bool jumpInput;

    private Vector3 currentVelocity;
    private float verticalVelocity;
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.useGravity = false;

        playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];

        if (moveAction != null)
        {
            moveAction.performed += OnMove;
            moveAction.canceled += OnMoveCanceled;
        }
        else
        {
            Debug.LogError("Move action not found.", this);
        }

        if (jumpAction != null)
        {
            jumpAction.performed += OnJump;
        }
        else
        {
            Debug.LogError("Jump action not found.", this);
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMove;
            moveAction.canceled -= OnMoveCanceled;
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= OnJump;
        }
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        jumpInput = true;
    }

    private void FixedUpdate()
    {
        GroundCheck();
        ApplyGravity();
        Movement();
        Rotation();
        Jump();
        UpdateAnimation();
    }

    private void GroundCheck()
{
    float radius = 0.3f;
    Vector3 origin = transform.position + Vector3.up * 0.1f;

    isGrounded = Physics.SphereCast(
        origin,
        radius,
        Vector3.down,
        out RaycastHit hit,
        groundCheckDistance + 0.1f,
        groundLayer
    );

    if (isGrounded && verticalVelocity < 0f)
    {
        verticalVelocity = groundedGravity;
    }
}

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            verticalVelocity += gravity * Time.fixedDeltaTime;
            if (verticalVelocity < maxFallSpeed)
                verticalVelocity = maxFallSpeed;
        }
    }

    private void Movement()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (inputDir.magnitude < 0.1f)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;
            currentVelocity = Vector3.Lerp(currentVelocity, moveDir * moveSpeed, acceleration * Time.fixedDeltaTime);
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = currentVelocity.x;
        velocity.z = currentVelocity.z;
        velocity.y = verticalVelocity;

        rb.linearVelocity = velocity;
    }

    private void Rotation()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (inputDir.sqrMagnitude < 0.01f) return;

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;

        Vector3 moveDir = camForward.normalized * inputDir.z + camRight.normalized * inputDir.x;

        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        Quaternion smoothRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(smoothRotation);
    }

    private void Jump()
    {
        if (!jumpInput) return;
        jumpInput = false;

        if (isGrounded)
        {
            verticalVelocity = jumpForce;
        }
    }

    private void UpdateAnimation()
    {
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / moveSpeed);

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, normalizedSpeed, 0.1f, Time.fixedDeltaTime);
            animator.SetBool(IsGroundedHash, isGrounded);
        }
    }

    private void OnDrawGizmosSelected()
    {
        float radius = 0.3f;
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawLine(origin, origin + Vector3.down * (groundCheckDistance + 0.1f));
    }
}