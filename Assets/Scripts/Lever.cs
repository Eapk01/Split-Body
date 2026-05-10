using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour, IInteractable
{
    [Header("State")]
    public bool IsOn = false;

    [Header("Linked Levers")]
    [SerializeField] Animator[] extraAnimators;

    [Header("Events")]
    public UnityEvent OnActivate;
    public UnityEvent OnDeactivate;

    Animator animator;
    static readonly int IsOnParam = Animator.StringToHash("IsOn");

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Interact()
    {
        IsOn = !IsOn;
        animator.SetBool(IsOnParam, IsOn);

        foreach (var extra in extraAnimators)
            extra.SetBool(IsOnParam, IsOn);

        if (IsOn) OnActivate?.Invoke();
        else      OnDeactivate?.Invoke();
    }

    public string GetPrompt() => IsOn ? "Reset Lever" : "Pull Lever";
}