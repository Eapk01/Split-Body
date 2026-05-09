using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ThirdPersonMotor motor;
    [SerializeField] Animator animator;

    [Header("Parameters")]
    [SerializeField] string moveSpeedParameter = "MoveSpeed";
    [SerializeField] string groundedParameter = "IsGrounded";
    [SerializeField] string verticalSpeedParameter = "VerticalSpeed";

    [Header("Smoothing")]
    [SerializeField] float speedDampTime = 0.1f;

    void Reset()
    {
        motor = GetComponentInParent<ThirdPersonMotor>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (motor == null || animator == null)
            return;

        animator.SetFloat(moveSpeedParameter, motor.NormalizedSpeed, speedDampTime, Time.deltaTime);
        animator.SetBool(groundedParameter, motor.IsGrounded);
        animator.SetFloat(verticalSpeedParameter, motor.Velocity.y);
    }
}