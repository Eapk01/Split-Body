using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public enum PlayerType { Child, Ghost }

    [SerializeField] PlayerType playerType;
    [SerializeField] float interactRadius = 2f;

    [SerializeField] LayerMask childSolidLayer;
    [SerializeField] LayerMask ghostSolidLayer;
    [SerializeField] LayerMask interactableLayer;

    PlayerInputReader input;
    IInteractable current;

    LayerMask interactMask => playerType == PlayerType.Child
        ? childSolidLayer | interactableLayer
        : ghostSolidLayer | interactableLayer;

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();
    }

    void Update()
{
    Collider[] hits = Physics.OverlapSphere(
        transform.position, interactRadius, interactMask,
        QueryTriggerInteraction.Collide);

    current = null;
    float closest = float.MaxValue;

    foreach (var hit in hits)
    {
        var interactable = hit.transform.GetComponentInParent<IInteractable>();
        if (interactable == null) continue;

        float dist = Vector3.Distance(
            transform.position, hit.transform.position);

        if (dist < closest)
        {
            closest = dist;
            current = interactable;
        }
    }
    if (input.InteractPressed && current != null)
{
    Debug.Log("Interacting!");
    current.Interact();
    input.ConsumeInteract();
}
else if (input.InteractPressed)
{
    Debug.Log("Pressed but no current target!");
}

    if (input.InteractPressed && current != null)
    {
        current.Interact();
        input.ConsumeInteract();
    }
}

    void OnDrawGizmosSelected()
    {
        Gizmos.color = playerType == PlayerType.Child
            ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}