using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour, IInteractable
{
    public enum LeverMode
    {
        Toggle,
        OneShot,
        Timed
    }

    [Header("State")]
    public bool IsOn = false;

    [Header("Behavior")]
    [SerializeField] LeverMode mode = LeverMode.Toggle;
    [SerializeField] float activeDuration = 3f;
    [SerializeField] bool refreshTimerWhenInteracted = true;

    [Header("Prompt")]
    [SerializeField] string activatePrompt = "Pull Lever";
    [SerializeField] string deactivatePrompt = "Reset Lever";
    [SerializeField] string activePrompt = "Lever Pulled";

    [Header("Linked Levers")]
    [SerializeField] Animator[] extraAnimators;

    [Header("Events")]
    public UnityEvent OnActivate;
    public UnityEvent OnDeactivate;

    Animator animator;
    float activeTimer;

    static readonly int IsOnParam = Animator.StringToHash("IsOn");

    void Awake()
    {
        animator = GetComponent<Animator>();
        ApplyVisualState();
        OnDeactivate?.Invoke();
    }

    void Update()
    {
        if (mode != LeverMode.Timed || !IsOn)
            return;

        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f)
            SetState(false);
    }

    public void Interact()
    {
        switch (mode)
        {
            case LeverMode.Toggle:
                SetState(!IsOn);
                break;

            case LeverMode.OneShot:
                if (!IsOn)
                    SetState(true);
                break;

            case LeverMode.Timed:
                if (!IsOn)
                {
                    SetState(true);
                }
                else if (refreshTimerWhenInteracted)
                {
                    activeTimer = Mathf.Max(0f, activeDuration);
                }
                break;
        }
    }

    public void SetState(bool isOn)
    {
        if (IsOn == isOn)
            return;

        IsOn = isOn;
        if (IsOn && mode == LeverMode.Timed)
            activeTimer = Mathf.Max(0f, activeDuration);

        ApplyVisualState();

        if (IsOn) OnActivate?.Invoke();
        else OnDeactivate?.Invoke();
    }

    void ApplyVisualState()
    {
        if (animator != null)
            animator.SetBool(IsOnParam, IsOn);

        print(animator);

        if (extraAnimators == null)
            return;

        foreach (var extra in extraAnimators)
        {
            if (extra != null)
                extra.SetBool(IsOnParam, IsOn);
        }
    }

    public string GetPrompt()
    {
        if (!IsOn)
            return activatePrompt;

        return mode == LeverMode.Toggle ? deactivatePrompt : activePrompt;
    }
}
