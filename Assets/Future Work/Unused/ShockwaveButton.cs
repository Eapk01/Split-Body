using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShockwaveButton : MonoBehaviour, IInteractable
{
    [Header("Timing")]
    [SerializeField] float cooldown = 2f;

    [Header("Shake")]
    [SerializeField] float shakeDuration = 0.7f;
    [SerializeField] float shakeAmplitude = 0.22f;
    [SerializeField] float shakeFrequency = 28f;

    [Header("Confetti")]
    [SerializeField] int confettiCount = 140;
    [SerializeField] float confettiLifetime = 1.8f;
    [SerializeField] float confettiSpeed = 7f;

    [Header("Shockwave")]
    [SerializeField] float shockwaveRadius = 7f;
    [SerializeField] float shockwaveDuration = 0.45f;

    ParticleSystem confetti;
    LineRenderer shockwave;
    Transform buttonTop;
    bool coolingDown;

    public void Interact()
    {
        if (!coolingDown)
            StartCoroutine(Fire());
    }

    public string GetPrompt() => coolingDown ? "Recharging" : "Trigger Shockwave";

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        BuildVisuals();
        BuildConfetti();
        BuildShockwave();
    }

    void BuildVisuals()
    {
        if (buttonTop != null)
            return;

        Material baseMaterial = NewMaterial(new Color(0.08f, 0.08f, 0.08f));
        Material buttonMaterial = NewMaterial(new Color(1f, 0.04f, 0.02f));

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = "Button Base";
        baseObject.transform.SetParent(transform, false);
        baseObject.transform.localPosition = Vector3.up * 0.08f;
        baseObject.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);
        baseObject.GetComponent<Renderer>().sharedMaterial = baseMaterial;
        Destroy(baseObject.GetComponent<Collider>());

        GameObject topObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        topObject.name = "Shockwave Button";
        topObject.transform.SetParent(transform, false);
        topObject.transform.localPosition = Vector3.up * 0.28f;
        topObject.transform.localScale = new Vector3(0.7f, 0.22f, 0.7f);
        topObject.GetComponent<Renderer>().sharedMaterial = buttonMaterial;
        Destroy(topObject.GetComponent<Collider>());

        buttonTop = topObject.transform;
    }

    void BuildConfetti()
    {
        GameObject particleObject = new GameObject("Confetti Burst");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = Vector3.up * 0.45f;

        confetti = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = confetti.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.12f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(confettiLifetime * 0.65f, confettiLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(confettiSpeed * 0.65f, confettiSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = confetti.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = confetti.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.18f;

        ParticleSystem.ColorOverLifetimeModule color = confetti.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.yellow, 0.25f),
                new GradientColorKey(Color.cyan, 0.55f),
                new GradientColorKey(Color.magenta, 0.85f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        color.color = gradient;

        ParticleSystemRenderer particleRenderer = confetti.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    void BuildShockwave()
    {
        GameObject waveObject = new GameObject("Shockwave Ring");
        waveObject.transform.SetParent(transform, false);
        waveObject.transform.localPosition = Vector3.up * 0.03f;

        shockwave = waveObject.AddComponent<LineRenderer>();
        shockwave.useWorldSpace = false;
        shockwave.loop = true;
        shockwave.positionCount = 64;
        shockwave.widthMultiplier = 0.08f;
        shockwave.enabled = false;
        shockwave.sharedMaterial = NewMaterial(new Color(1f, 1f, 1f, 0.8f), true);
    }

    IEnumerator Fire()
    {
        coolingDown = true;

        if (buttonTop != null)
            yield return AnimateButtonPress();

        confetti.Emit(confettiCount);
        StartCoroutine(ShakeCameras());
        yield return AnimateShockwave();
        yield return new WaitForSeconds(cooldown);

        coolingDown = false;
    }

    IEnumerator AnimateButtonPress()
    {
        Vector3 start = buttonTop.localPosition;
        Vector3 pressed = start + Vector3.down * 0.08f;

        yield return MoveButton(start, pressed, 0.12f);
        yield return MoveButton(pressed, start, 0.18f);
        buttonTop.localPosition = start;
    }

    IEnumerator MoveButton(Vector3 from, Vector3 to, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            buttonTop.localPosition = Vector3.Lerp(from, to, timer / duration);
            yield return null;
        }
    }

    IEnumerator AnimateShockwave()
    {
        shockwave.enabled = true;
        float timer = 0f;

        while (timer < shockwaveDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / shockwaveDuration);
            float radius = Mathf.Lerp(0.2f, shockwaveRadius, progress);
            float alpha = 1f - progress;

            DrawShockwave(radius);
            shockwave.startColor = new Color(1f, 1f, 1f, alpha);
            shockwave.endColor = new Color(1f, 0.2f, 0.1f, alpha);
            yield return null;
        }

        shockwave.enabled = false;
    }

    IEnumerator ShakeCameras()
    {
        Camera[] cameras = Camera.allCameras;
        Vector3[] originalPositions = new Vector3[cameras.Length];

        for (int i = 0; i < cameras.Length; i++)
            originalPositions[i] = cameras[i] != null ? cameras[i].transform.localPosition : Vector3.zero;

        float timer = 0f;
        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / shakeDuration);
            float strength = shakeAmplitude * (1f - progress) * (1f - progress);
            float sampleTime = Time.time * shakeFrequency;
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(sampleTime, 0.13f) - 0.5f,
                Mathf.PerlinNoise(0.37f, sampleTime) - 0.5f,
                0f
            ) * (strength * 2f);

            yield return new WaitForEndOfFrame();

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                    cameras[i].transform.localPosition = originalPositions[i] + offset;
            }
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
                cameras[i].transform.localPosition = originalPositions[i];
        }
    }

    void DrawShockwave(float radius)
    {
        for (int i = 0; i < shockwave.positionCount; i++)
        {
            float angle = (i / (float)shockwave.positionCount) * Mathf.PI * 2f;
            shockwave.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    Material NewMaterial(Color color, bool unlit = false)
    {
        string shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;
        return material;
    }
}
