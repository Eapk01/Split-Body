using UnityEngine;

public class ThirdPersonCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;

    [Header("Input")]
    [SerializeField] PlayerInputReader input;

    [Header("Orbit")]
    [SerializeField] float distance = 4.5f;
    [SerializeField] float targetHeightOffset = 1.6f;

    [Header("Look")]
    float mouseSensitivity = 0.15f;
    float controllerSensitivity = 1;
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 70f;

    [Header("Smoothing")]
    [SerializeField] float followSmoothTime = 0.08f;

    float yaw;
    float pitch;

    Vector3 followVelocity;
    Vector3 smoothedTargetPosition;

    float appliedSensitivity = 0.15f;

    void Start()
    {
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);

            smoothedTargetPosition = target.position + Vector3.up * targetHeightOffset;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null || input == null || PauseMenu.IsPaused)
            return;

        HandleLook();
        UpdateCameraPosition();
    }

    void HandleLook()
    {
        Vector2 look = input.Look;

        // Mouse delta is already per-frame delta, so do NOT multiply by deltaTime.
        // Controller sticks are usually rate-based, so those normally would use deltaTime.
        // For now, this setup assumes mouse input for look.
        yaw += look.x * appliedSensitivity;
        pitch -= look.y * appliedSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void UpdateCameraPosition()
    {
        Vector3 targetPosition = target.position + Vector3.up * targetHeightOffset;

        smoothedTargetPosition = Vector3.SmoothDamp(
            smoothedTargetPosition,
            targetPosition,
            ref followVelocity,
            followSmoothTime
        );

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 cameraOffset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = smoothedTargetPosition + cameraOffset;
        transform.rotation = rotation;
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            smoothedTargetPosition = target.position + Vector3.up * targetHeightOffset;
    }

    public void SetInput(PlayerInputReader newInput, string type)
    {
        input = newInput;
        if(type == "Gamepad")
        {
            appliedSensitivity = controllerSensitivity;
        }
    }
}