using UnityEngine;

[RequireComponent(typeof(Light))]
public class SubtleLightFlicker : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] private float baseIntensity = 1.2f;

    [Header("Flicker")]
    [SerializeField] private float flickerAmount = 0.08f;
    [SerializeField] private float flickerSpeed = 2f;

    [Header("Random Pulse")]
    [SerializeField] private float pulseChance = 0.02f;
    [SerializeField] private float pulseStrength = 0.2f;
    [SerializeField] private float pulseRecoverSpeed = 8f;

    private Light lightSource;

    private float targetIntensity;
    private float currentPulse;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        targetIntensity = baseIntensity;
    }

    private void Update()
    {
        // Smooth subtle noise flicker
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
        float flicker = (noise - 0.5f) * flickerAmount;

        // Occasional tiny pulse dip
        if (Random.value < pulseChance * Time.deltaTime * 60f)
        {
            currentPulse = pulseStrength;
        }

        currentPulse = Mathf.Lerp(currentPulse, 0f, Time.deltaTime * pulseRecoverSpeed);

        // Final intensity
        targetIntensity = baseIntensity + flicker - currentPulse;

        lightSource.intensity = targetIntensity;
    }
}