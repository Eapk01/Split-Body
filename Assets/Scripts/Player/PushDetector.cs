using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PushDetector : MonoBehaviour
{
    const string ChildPlayerLayerName = "ChildPlayer";
    static readonly int IsPushingParam = Animator.StringToHash("IsPushing");

    [Header("Weight")]
    [SerializeField] bool applyHeavyRigidbodySettings = true;
    [SerializeField] float pushedMass = 8f;
    [SerializeField] float pushedLinearDamping = 0.5f;
    [SerializeField] float pushedAngularDamping = 2f;

    [Header("Detection")]
    [SerializeField] float pushDirectionThreshold = 0.35f;
    [SerializeField] float pushGraceTime = 0.08f;

    [Header("Player Feel")]
    [SerializeField, Range(0.1f, 1f)] float childPushSpeedMultiplier = 0.45f;

    bool isPushing;
    float lastPushTime = -1f;
    int childPlayerLayer;
    Rigidbody body;
    ThirdPersonMotor childMotor;
    Animator childAnimator;

    void Awake()
    {
        childPlayerLayer = LayerMask.NameToLayer(ChildPlayerLayerName);
        body = GetComponent<Rigidbody>();

        ApplyHeavyRigidbodySettings();
    }

    void LateUpdate()
    {
        SetPushing(Time.time - lastPushTime <= pushGraceTime);
    }

    void OnDisable()
    {
        SetPushing(false);
    }

    void OnCollisionStay(Collision collision)
    {
        if (!IsChildCollision(collision))
            return;

        if (!ResolveChildReferences(collision.transform))
            return;

        Vector3 moveDirection = childMotor.MoveDirection;
        moveDirection.y = 0f;

        Vector3 directionToBox = transform.position - childMotor.transform.position;
        directionToBox.y = 0f;

        bool childIsPushing =
            moveDirection.sqrMagnitude > 0.01f &&
            directionToBox.sqrMagnitude > 0.01f &&
            Vector3.Dot(moveDirection.normalized, directionToBox.normalized) >= pushDirectionThreshold;

        if (childIsPushing)
            lastPushTime = Time.time;
    }

    void SetPushing(bool pushing)
    {
        isPushing = pushing;

        if (childMotor != null)
            childMotor.SetPushSpeedMultiplier(isPushing ? childPushSpeedMultiplier : 1f);

        if (childAnimator != null)
            childAnimator.SetBool(IsPushingParam, isPushing);
    }

    bool IsChildCollision(Collision collision)
    {
        return childPlayerLayer >= 0 && collision.gameObject.layer == childPlayerLayer;
    }

    bool ResolveChildReferences(Transform collisionTransform)
    {
        if (childMotor == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.TryGetChild(out GameObject child))
                childMotor = child.GetComponent<ThirdPersonMotor>();

            if (childMotor == null)
                childMotor = collisionTransform.GetComponentInParent<ThirdPersonMotor>();
        }

        if (childAnimator == null && childMotor != null)
            childAnimator = childMotor.GetComponentInChildren<Animator>(true);

        return childMotor != null && childAnimator != null;
    }

    void ApplyHeavyRigidbodySettings()
    {
        if (!applyHeavyRigidbodySettings || body == null)
            return;

        body.mass = Mathf.Max(0.01f, pushedMass);
        body.linearDamping = Mathf.Max(0f, pushedLinearDamping);
        body.angularDamping = Mathf.Max(0f, pushedAngularDamping);
    }
}
