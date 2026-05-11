using System.Linq;
using UnityEngine;

[RequireComponent(typeof(FloatingTextEffect))]
public class InfoTextTrigger : MonoBehaviour
{
    public enum PlayerTarget
    {
        ClosestPlayer,
        Child,
        Ghost
    }

    [Header("Text")]
    [TextArea]
    [SerializeField] string text = "Press E";
    [SerializeField] bool clearOnStart = true;

    [Header("Player")]
    [SerializeField] PlayerTarget playerTarget = PlayerTarget.ClosestPlayer;
    [SerializeField] Transform targetOverride;
    [SerializeField] float targetRefreshInterval = 0.35f;

    [Header("Distance")]
    [SerializeField] float showDistance = 3f;
    [SerializeField] float hideDistance = 3.6f;

    [Header("Facing")]
    [SerializeField] bool assignLookAtTarget = true;

    [Header("Arabic Requester (Optional)")]
    WordMeshLoader meshLoader;

    FloatingTextEffect textEffect;
    LookAtTarget lookAtTarget;
    Transform currentTarget;
    float nextTargetRefreshTime;
    bool isShown;

    void Awake()
    {
        textEffect = GetComponent<FloatingTextEffect>();
        lookAtTarget = GetComponent<LookAtTarget>();
        meshLoader = GetComponent<WordMeshLoader>();
    }

    void Start()
    {
        hideDistance = Mathf.Max(hideDistance, showDistance);

        if (clearOnStart)
            textEffect.Clear();
    }

    void Update()
    {
        RefreshTargetIfNeeded();

        if (currentTarget == null)
        {
            HideText();
            return;
        }

        float distanceSqr = (currentTarget.position - transform.position).sqrMagnitude;
        float threshold = isShown ? hideDistance : showDistance;

        if (distanceSqr <= threshold * threshold)
        {
            ShowText();
        }
        else
        {
            HideText();
        }
    }

    void OnValidate()
    {
        showDistance = Mathf.Max(0f, showDistance);
        hideDistance = Mathf.Max(showDistance, hideDistance);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
    }

    [ContextMenu("Refresh Target")]
    public void RefreshTarget()
    {
        currentTarget = targetOverride != null ? targetOverride : FindPlayerTarget();

        if (assignLookAtTarget && lookAtTarget != null)
            lookAtTarget.Target = currentTarget;
    }

    [ContextMenu("Show Text")]
    public void ShowText()
    {
        if (isShown)
            return;

        if (InstancedTextRenderer.IsArabicLine(text))
        {
            string[] words = WordMeshLoader.SplitWords(text);
            meshLoader.Prefetch(words);
        }

        textEffect.PlayPersistent(text);
        isShown = true;
    }

    [ContextMenu("Drop Text")]
    public void HideText()
    {
        if (!isShown)
            return;

        textEffect.BeginFallNow();
        isShown = false;
    }

    void RefreshTargetIfNeeded()
    {
        if (currentTarget != null && Time.time < nextTargetRefreshTime)
            return;

        RefreshTarget();
        nextTargetRefreshTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);
    }

    Transform FindPlayerTarget()
    {
        if (GameManager.Instance != null)
        {
            switch (playerTarget)
            {
                case PlayerTarget.Child:
                    if (GameManager.Instance.TryGetChild(out GameObject child))
                        return child.transform;
                    break;
                case PlayerTarget.Ghost:
                    if (GameManager.Instance.TryGetGhost(out GameObject ghost))
                        return ghost.transform;
                    break;
                default:
                    return FindClosestRegisteredPlayer();
            }
        }

        return FindClosestPlayerComponent();
    }

    Transform FindClosestRegisteredPlayer()
    {
        Transform closest = null;
        float closestDistanceSqr = float.PositiveInfinity;

        if (GameManager.Instance.TryGetChild(out GameObject child))
            TryUseClosest(child.transform, ref closest, ref closestDistanceSqr);

        if (GameManager.Instance.TryGetGhost(out GameObject ghost))
            TryUseClosest(ghost.transform, ref closest, ref closestDistanceSqr);

        return closest != null ? closest : FindClosestPlayerComponent();
    }

    Transform FindClosestPlayerComponent()
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        Transform closest = null;
        float closestDistanceSqr = float.PositiveInfinity;

        foreach (Player player in players)
        {
            if (player == null)
                continue;

            TryUseClosest(player.transform, ref closest, ref closestDistanceSqr);
        }

        return closest;
    }

    void TryUseClosest(Transform candidate, ref Transform closest, ref float closestDistanceSqr)
    {
        if (candidate == null)
            return;

        float distanceSqr = (candidate.position - transform.position).sqrMagnitude;
        if (distanceSqr >= closestDistanceSqr)
            return;

        closest = candidate;
        closestDistanceSqr = distanceSqr;
    }
}
