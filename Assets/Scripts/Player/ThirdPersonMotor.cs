using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayerInputReader))]
public class ThirdPersonMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraTransform;
    [SerializeField] Transform visualRoot;

    [Header("Speed")]
    float walkSpeed = 6f;
    float sprintSpeed = 8.5f;

    [Header("Ground Movement")]
    [SerializeField] float groundAcceleration = 30f;
    [SerializeField] float groundDeceleration = 40f;
    [SerializeField] float groundTurnAcceleration = 48f;
    [SerializeField] float stopSpeedThreshold = 0.08f;

    [Header("Air Movement")]
    [SerializeField] float airAcceleration = 10f;
    [SerializeField] float maxAirSpeed = 6.5f;

    [Header("Rotation")]
    [SerializeField] float rotationSmoothTime = 0.04f;

    [Header("Jump / Gravity")]
    [SerializeField] float gravity = -25f;
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float groundedStickForce = -2f;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] float groundCheckDistance = 0.1f;
    [SerializeField] float groundProbeOffset = 0.05f;

    Rigidbody rb;
    Collider bodyCollider;
    PlayerInputReader input;
    string schema;

    Vector3 horizontalVelocity;
    float verticalVelocity;
    float rotationVelocity;
    bool isGrounded;
    float coyoteCounter;
    float jumpBufferCounter;

    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    public bool IsGrounded => isGrounded;
    public Vector3 MoveDirection { get; private set; }

    public float HorizontalSpeed
    {
        get
        {
            float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
            return speed < stopSpeedThreshold ? 0f : speed;
        }
    }

    public float NormalizedSpeed
    {
        get
        {
            float maxSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            if (maxSpeed <= 0f) return 0f;
            return Mathf.Clamp01(HorizontalSpeed / maxSpeed);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        input = GetComponent<PlayerInputReader>();

        rb.useGravity = false;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (rb.interpolation == RigidbodyInterpolation.None)
            rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (visualRoot == null)
            visualRoot = transform;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        UpdateGroundedState();
        UpdateJumpTimers(dt);
        HandleMovement(dt);
        HandleRotation(dt);
        HandleVertical(dt);
        ApplyVelocity();
    }

    void HandleMovement(float dt)
    {
        Vector2 rawInput = input.Move;
        Vector2 moveInput = Vector2.ClampMagnitude(rawInput, 1f);

        Vector3 desiredMove = GetCameraRelativeDirection(moveInput);
        MoveDirection = desiredMove;

        float targetSpeed = IsSprint() ? sprintSpeed : walkSpeed;
        Vector3 targetHorizontalVelocity = desiredMove * targetSpeed;

        Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        bool hasInput = moveInput.sqrMagnitude > 0.0001f;

        if (!isGrounded)
        {
            horizontalVelocity = Vector3.MoveTowards(
                currentHorizontalVelocity,
                targetHorizontalVelocity,
                airAcceleration * dt
            );

            float airSpeed = horizontalVelocity.magnitude;
            if (airSpeed > maxAirSpeed)
                horizontalVelocity = horizontalVelocity.normalized * maxAirSpeed;

            return;
        }

        if (!hasInput)
        {
            horizontalVelocity = Vector3.MoveTowards(
                currentHorizontalVelocity,
                Vector3.zero,
                groundDeceleration * dt
            );

            if (horizontalVelocity.sqrMagnitude < stopSpeedThreshold * stopSpeedThreshold)
                horizontalVelocity = Vector3.zero;

            return;
        }

        float alignment = 1f;
        if (currentHorizontalVelocity.sqrMagnitude > 0.0001f)
        {
            alignment = Vector3.Dot(
                currentHorizontalVelocity.normalized,
                targetHorizontalVelocity.normalized
            );
        }

        float accel = alignment < 0.5f ? groundTurnAcceleration : groundAcceleration;

        horizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetHorizontalVelocity,
            accel * dt
        );
    }

    bool IsSprint()
    {
        bool isMoving = input.Move.sqrMagnitude > 0.01f;

        if (schema == "Gamepad") // your scheme check
        {
            // Auto-cancel when not moving
            if (!isMoving)
            {
                input.ConsumeSprint();
            }

            return input.SprintPressed;
        }
        else
        {
            // Keyboard = hold behavior
            return input.SprintHeld;
        }
    }

    void HandleRotation(float dt)
    {
        Vector3 flatMove = MoveDirection;
        flatMove.y = 0f;

        if (flatMove.sqrMagnitude < 0.0001f)
            return;

        float targetAngle = Mathf.Atan2(flatMove.x, flatMove.z) * Mathf.Rad2Deg;
        float currentY = visualRoot.eulerAngles.y;

        float smoothAngle = Mathf.SmoothDampAngle(
            currentY,
            targetAngle,
            ref rotationVelocity,
            rotationSmoothTime,
            Mathf.Infinity,
            dt
        );

        Quaternion targetRotation = Quaternion.Euler(0f, smoothAngle, 0f);

        if (visualRoot == transform)
            rb.MoveRotation(targetRotation);
        else
            visualRoot.rotation = targetRotation;
    }

    void HandleVertical(float dt)
    {
        verticalVelocity = rb.linearVelocity.y;

        bool canJump = coyoteCounter > 0f && jumpBufferCounter > 0f;
        if (canJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteCounter = 0f;
            jumpBufferCounter = 0f;
            isGrounded = false;
            return;
        }

        if (isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = groundedStickForce;
        }
        else
        {
            verticalVelocity += gravity * dt;
        }
    }

    void ApplyVelocity()
    {
        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            verticalVelocity,
            horizontalVelocity.z
        );
    }

    void UpdateGroundedState()
    {
        Vector3 origin;
        float castDistance = groundCheckDistance;

        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            origin = new Vector3(
                bounds.center.x,
                bounds.min.y + groundCheckRadius + groundProbeOffset,
                bounds.center.z
            );
            castDistance += groundProbeOffset;
        }
        else
        {
            origin = transform.position + Vector3.up * (groundCheckRadius + groundProbeOffset);
            castDistance += groundProbeOffset;
        }

        bool hitGround = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit hit,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        isGrounded = hitGround && hit.rigidbody != rb;
    }

    void UpdateJumpTimers(float dt)
    {
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter = Mathf.Max(coyoteCounter - dt, 0f);

        if (input.JumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
            input.ConsumeJump();
        }
        else
        {
            jumpBufferCounter = Mathf.Max(jumpBufferCounter - dt, 0f);
        }
    }

    Vector3 GetCameraRelativeDirection(Vector2 moveInput)
    {
        if (cameraTransform == null)
            return new Vector3(moveInput.x, 0f, moveInput.y);

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 move = camForward * moveInput.y + camRight * moveInput.x;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        return move;
    }

    public void SetCamera(Transform cam)
    {
        cameraTransform = cam;
    }

    public void SetSchema(string schema)
    {
        this.schema = schema;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;

        Vector3 origin;
        float castDistance = groundCheckDistance;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Bounds bounds = col.bounds;
            origin = new Vector3(
                bounds.center.x,
                bounds.min.y + groundCheckRadius + groundProbeOffset,
                bounds.center.z
            );
            castDistance += groundProbeOffset;
        }
        else
        {
            origin = transform.position + Vector3.up * (groundCheckRadius + groundProbeOffset);
            castDistance += groundProbeOffset;
        }

        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawLine(origin, origin + Vector3.down * castDistance);
        Gizmos.DrawWireSphere(origin + Vector3.down * castDistance, groundCheckRadius);
    }
}