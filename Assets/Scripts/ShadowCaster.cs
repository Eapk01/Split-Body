using UnityEngine;

public class ShadowProjector : MonoBehaviour
{
    [Header("References")]
    Light spotLight;

    [SerializeField] Transform miniatureObject;
    [SerializeField] Transform shadowObject;

    [Header("Projection")]
    [SerializeField] float projectionDistance = 8f;

    [Tooltip("Locks shadow to ground height")]
    [SerializeField] float groundY = 0f;

    [Header("Scaling")]
    [SerializeField] float scaleMultiplier = 1f;

    [Tooltip("Prevents infinite scaling near light")]
    [SerializeField] float minLightDistance = 0.5f;

    [Header("Light Edge Detection")]
    [SerializeField] float edgeThreshold = 0.9f;

    [Range(0f, 1f)]
    [SerializeField] float edgeFade = 0f;

    [Header("Optional")]
    [SerializeField] bool rotateShadow = true;

    [Header("Edge Fade")]
    [SerializeField] float fadeStart = 0.75f;
    [SerializeField] float fadeEnd = 1f;

    [SerializeField] Material shadowMaterial;

    void Start()
    {
        spotLight = GetComponent<Light>();
    }

    void Update()
    {
        if (!spotLight || !miniatureObject || !shadowObject)
            return;

        if (spotLight.type != LightType.Spot)
        {
            Debug.LogWarning("ShadowProjector requires a Spot Light.");
            return;
        }

        UpdateShadow();
    }

    void UpdateShadow()
    {
        //----------------------------------------
        // LIGHT DATA
        //----------------------------------------

        Vector3 lightPos = spotLight.transform.position;
        Vector3 lightForward = spotLight.transform.forward;

        //----------------------------------------
        // OBJECT DIRECTION FROM LIGHT
        //----------------------------------------

        Vector3 lightToObject =
            miniatureObject.position - lightPos;

        float distanceToObject =
            lightToObject.magnitude;

        Vector3 directionToObject =
            lightToObject.normalized;

        //----------------------------------------
        // RANGE CHECK
        //----------------------------------------

        bool insideRange =
            distanceToObject <= spotLight.range;

        //----------------------------------------
        // SPOT ANGLE CHECK
        //----------------------------------------

        float angleToObject =
            Vector3.Angle(lightForward, directionToObject);

        float halfSpotAngle =
            spotLight.spotAngle * 0.5f;

        float normalizedAngle =
            angleToObject / halfSpotAngle;

        bool insideCone =
            normalizedAngle <= edgeThreshold;

        //----------------------------------------
        /// Calculate visibility
        //----------------------------------------

        float visibility =
            1f - Mathf.InverseLerp(
                fadeStart,
                fadeEnd,
                normalizedAngle
            );

        visibility = Mathf.SmoothStep(0f, 100f, visibility);

        //----------------------------------------
        // ENABLE / DISABLE SHADOW
        //----------------------------------------

        bool validProjection =
            insideRange && insideCone;

        shadowObject.gameObject.SetActive(validProjection);

        if (!validProjection)
            return;

        //----------------------------------------
        // POSITION PROJECTION
        //----------------------------------------

        Vector3 projectionDirection =
            directionToObject;

        Vector3 projectedPosition =
            miniatureObject.position +
            projectionDirection * projectionDistance;

        projectedPosition.y = groundY;

        shadowObject.position = projectedPosition;

        //----------------------------------------
        // SCALE
        //----------------------------------------

        distanceToObject =
            Mathf.Max(distanceToObject, minLightDistance);

        float scale =
            (projectionDistance / distanceToObject)
            * scaleMultiplier;

        shadowObject.localScale =
            Vector3.one * scale;

        //----------------------------------------
        // ROTATION
        //----------------------------------------

        if (rotateShadow)
        {
            Vector3 flatDirection = projectionDirection;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude > 0.001f)
            {
                shadowObject.rotation =
                    Quaternion.LookRotation(flatDirection);
            }
        }

        shadowMaterial.SetFloat("_Health", visibility);
    }

    //----------------------------------------
    // DEBUG GIZMOS
    //----------------------------------------

    void OnDrawGizmos()
    {
        if (!spotLight || !miniatureObject)
            return;

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(
            spotLight.transform.position,
            miniatureObject.position
        );

        Vector3 dir =
            (miniatureObject.position -
             spotLight.transform.position).normalized;

        Vector3 projectedPosition =
            miniatureObject.position +
            dir * projectionDistance;

        projectedPosition.y = groundY;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(projectedPosition, 0.2f);

        Gizmos.DrawLine(
            miniatureObject.position,
            projectedPosition
        );
    }
}