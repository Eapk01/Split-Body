using UnityEngine;

public class LookAtTarget : MonoBehaviour
{
    public enum AxisMode
    {
        Full,
        YawOnly
    }

    [Header("Target")]
    [SerializeField] Transform target;

    [Header("Rotation")]
    [SerializeField] AxisMode axisMode = AxisMode.YawOnly;
    [SerializeField] Vector3 localForwardAxis = Vector3.forward;
    [SerializeField] Vector3 localUpAxis = Vector3.up;
    [SerializeField] bool flipForwardAxis;
    [HideInInspector] [SerializeField] Vector3 localEulerOffset;
    [SerializeField] bool smooth = true;
    [SerializeField] float turnSpeed = 12f;

    [Header("Options")]
    [SerializeField] bool useLateUpdate = true;
    [SerializeField] bool ignoreInactiveTarget = true;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    void Update()
    {
        if (!useLateUpdate)
            RotateTowardsTarget();
    }

    void LateUpdate()
    {
        if (useLateUpdate)
            RotateTowardsTarget();
    }

    public void RotateTowardsTarget()
    {
        if (!CanLookAtTarget())
            return;

        Vector3 direction = target.position - transform.position;

        if (axisMode == AxisMode.YawOnly)
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);

        if (direction.sqrMagnitude <= 0.000001f)
            return;

        Vector3 forwardAxis = localForwardAxis.sqrMagnitude > 0.000001f
            ? localForwardAxis.normalized
            : Vector3.forward;
        Vector3 upAxis = localUpAxis.sqrMagnitude > 0.000001f
            ? localUpAxis.normalized
            : Vector3.up;

        if (flipForwardAxis)
            forwardAxis = -forwardAxis;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion localAxisCorrection = Quaternion.Inverse(Quaternion.LookRotation(forwardAxis, upAxis));
        Quaternion desiredRotation = targetRotation * localAxisCorrection * Quaternion.Euler(localEulerOffset);

        if (!smooth)
        {
            transform.rotation = desiredRotation;
            return;
        }

        float t = 1f - Mathf.Exp(-Mathf.Max(0f, turnSpeed) * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
    }

    bool CanLookAtTarget()
    {
        if (target == null)
            return false;

        return !ignoreInactiveTarget || target.gameObject.activeInHierarchy;
    }
}
