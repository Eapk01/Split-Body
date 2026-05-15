using UnityEngine;

public class LightKiller : MonoBehaviour
{
    public Transform Source;
    public Transform[] Sources;
    public bool AutoFindSceneLights = true;

    Light[] sourceLights;

    void Start()
    {
        RefreshSources();
    }

    void Update()
    {
        if(!GameManager.Instance.TryGetGhost(out var ghost)) return;

        Light nearestLight = GetNearestLight(ghost.transform.position);
        if(nearestLight == null) return;

        Vector3 sourcePosition = nearestLight.transform.position;
        Vector3 direction = ghost.transform.position - sourcePosition;
        float length = nearestLight.range;

        Debug.DrawRay(sourcePosition, direction.normalized * length, Color.red);
        if(Physics.Raycast(sourcePosition, direction, out var hit, length))
        {
            if(hit.transform == ghost.transform)
            {
                ghost.GetComponent<Player>().TakeDamage(Time.deltaTime * 50);
            }
        }
    }

    void RefreshSources()
    {
        if(AutoFindSceneLights)
        {
            sourceLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            return;
        }

        if(Sources != null && Sources.Length > 0)
        {
            sourceLights = new Light[Sources.Length];
            for(int i = 0; i < Sources.Length; i++)
            {
                if(Sources[i] != null)
                {
                    sourceLights[i] = Sources[i].GetComponent<Light>();
                }
            }

            return;
        }

        if(Source != null)
        {
            sourceLights = new[] { Source.GetComponent<Light>() };
            return;
        }

        sourceLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
    }

    Light GetNearestLight(Vector3 position)
    {
        Light nearestLight = null;
        float nearestDistance = float.PositiveInfinity;

        for(int i = 0; i < sourceLights.Length; i++)
        {
            Light lightSource = sourceLights[i];
            if(lightSource == null || !lightSource.enabled || !lightSource.gameObject.activeInHierarchy || lightSource.type == LightType.Directional)
            {
                continue;
            }

            float distance = (position - lightSource.transform.position).sqrMagnitude;
            float range = lightSource.range * lightSource.range;
            if(distance > range || distance >= nearestDistance)
            {
                continue;
            }

            nearestLight = lightSource;
            nearestDistance = distance;
        }

        return nearestLight;
    }
}
