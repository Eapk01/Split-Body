using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class DustMoteField : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Vector3 boxSize = new Vector3(8f, 4f, 8f);
    [SerializeField] private bool simulateInWorldSpace = true;

    [Header("Dust")]
    [SerializeField, Min(1)] private int maxParticles = 90;
    [SerializeField, Min(0f)] private float particlesPerSecond = 7f;
    [SerializeField] private Vector2 lifetimeRange = new Vector2(10f, 18f);
    [SerializeField] private Vector2 sizeRange = new Vector2(0.018f, 0.055f);
    [SerializeField] private Vector2 speedRange = new Vector2(0.015f, 0.055f);
    [SerializeField, Range(0f, 2f)] private float turbulence = 0.28f;

    [Header("Look")]
    [SerializeField] private Material dustMaterial;
    [SerializeField] private Color color = new Color(1f, 0.86f, 0.55f, 0.22f);
    [SerializeField, Range(0f, 1f)] private float fadeInOut = 0.18f;

    private ParticleSystem particles;
    private Material runtimeDustMaterial;

    private void Reset()
    {
        Configure();
    }

    private void OnEnable()
    {
        Configure();
    }

    private void OnValidate()
    {
        boxSize = new Vector3(
            Mathf.Max(0.01f, boxSize.x),
            Mathf.Max(0.01f, boxSize.y),
            Mathf.Max(0.01f, boxSize.z));

        lifetimeRange = SortPositiveRange(lifetimeRange, 0.1f);
        sizeRange = SortPositiveRange(sizeRange, 0.001f);
        speedRange = SortPositiveRange(speedRange, 0f);
        maxParticles = Mathf.Max(1, maxParticles);
        particlesPerSecond = Mathf.Max(0f, particlesPerSecond);

        Configure();
    }

    [ContextMenu("Configure Dust Field")]
    private void Configure()
    {
        particles = GetComponent<ParticleSystem>();
        if (particles == null)
        {
            return;
        }

        ConfigureMain();
        ConfigureEmission();
        ConfigureShape();
        ConfigureColorOverLifetime();
        ConfigureSizeOverLifetime();
        ConfigureVelocity();
        ConfigureNoise();
        ConfigureRenderer();
    }

    private void ConfigureMain()
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = maxParticles;
        main.simulationSpace = simulateInWorldSpace
            ? ParticleSystemSimulationSpace.World
            : ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startColor = color;
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
    }

    private void ConfigureEmission()
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = particlesPerSecond;
    }

    private void ConfigureShape()
    {
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;
        shape.randomDirectionAmount = 1f;
    }

    private void ConfigureColorOverLifetime()
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        float fade = Mathf.Clamp(fadeInOut, 0f, 0.49f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, fade),
                new GradientAlphaKey(1f, 1f - fade),
                new GradientAlphaKey(0f, 1f)
            });

        colorOverLifetime.color = gradient;
    }

    private void ConfigureSizeOverLifetime()
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;

        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.8f, 1f),
            new Keyframe(1f, 0.35f));

        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private void ConfigureVelocity()
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.018f, 0.018f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.005f, 0.035f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.018f, 0.018f);
    }

    private void ConfigureNoise()
    {
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = turbulence > 0f;
        noise.strength = turbulence;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.08f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.octaveMultiplier = 0.45f;
        noise.octaveScale = 1.8f;
    }

    private void ConfigureRenderer()
    {
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = 0.08f;
        particleRenderer.sharedMaterial = ResolveDustMaterial();
    }

    private Material ResolveDustMaterial()
    {
        if (dustMaterial != null)
        {
            return dustMaterial;
        }

        Shader shader = Shader.Find("SplitBody/Particles/Soft Dust Mote");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        if (runtimeDustMaterial == null || runtimeDustMaterial.shader != shader)
        {
            runtimeDustMaterial = new Material(shader)
            {
                name = "Runtime Soft Dust Mote",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        runtimeDustMaterial.color = color;
        return runtimeDustMaterial;
    }

    private static Vector2 SortPositiveRange(Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
        return new Vector2(min, max);
    }
}
