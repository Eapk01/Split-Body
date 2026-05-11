using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InstancedTextRenderer))]
public class FloatingTextEffect : MonoBehaviour
{
    enum EffectState
    {
        Idle,
        Streaming,
        Holding,
        Falling,
        Done
    }

    struct GlyphFallState
    {
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float delay;
    }

    [Header("Text")]
    [SerializeField] string testText = "PERFECT";
    [SerializeField] bool playOnEnable;
    [SerializeField] float charactersPerSecond = 24f;

    [Header("Placement")]
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 2f, 0f);

    [Header("Timing")]
    [SerializeField] float holdDuration = 0.45f;

    [Header("Completion")]
    [SerializeField] bool disableOnComplete;
    [SerializeField] bool destroyOnComplete;

    [HideInInspector] [SerializeField] float fallDuration = 0.7f;
    [HideInInspector] [SerializeField] float fadeDuration = 0.35f;
    float perGlyphFallDelay = 0.15f;
    [HideInInspector] [SerializeField] float gravity = 12f;
    [HideInInspector] [SerializeField] Vector2 horizontalSpeedRange = new Vector2(0.15f, 0.55f);
    [HideInInspector] [SerializeField] Vector2 upwardSpeedRange = new Vector2(0.4f, 1.1f);
    [HideInInspector] [SerializeField] Vector2 tumbleSpeedRange = new Vector2(180f, 420f);
    [HideInInspector] [SerializeField] float vanishScale = 0.6f;
    [HideInInspector] [SerializeField] int randomSeed = 12345;
    [HideInInspector] [SerializeField] bool useGroundRaycast = true;
    [HideInInspector] [SerializeField] LayerMask groundMask = ~0;
    [HideInInspector] [SerializeField] float groundRaycastHeight = 2f;
    [HideInInspector] [SerializeField] float groundRaycastDistance = 8f;
    [HideInInspector] [SerializeField] float groundYOffset = 0.02f;
    [HideInInspector] [SerializeField] float fallbackGroundLocalY = -1.5f;

    readonly List<GlyphFallState> glyphStates = new();
    InstancedTextRenderer textRenderer;
    InstancedTextRenderer.GlyphPoseProvider poseProvider;
    EffectState state;
    string fullText = string.Empty;
    int visibleCharacterCount;
    float nextCharacterTime;
    float stateStartTime;
    float groundLocalY;
    bool fallAfterHold;

    public bool IsPlaying => state != EffectState.Idle && state != EffectState.Done;
    public bool IsStreaming => state == EffectState.Streaming;
    public float CharactersPerSecond
    {
        get => charactersPerSecond;
        set => charactersPerSecond = Mathf.Max(1f, value);
    }

    void Awake()
    {
        EnsureComponents();
    }

    void OnEnable()
    {
        EnsureComponents();
        textRenderer.PoseProvider = poseProvider;

        if (playOnEnable)
            Play(testText);
    }

    void OnDisable()
    {
        if (textRenderer != null && textRenderer.PoseProvider == poseProvider)
            textRenderer.PoseProvider = null;
    }

    void OnValidate()
    {
        charactersPerSecond = Mathf.Max(1f, charactersPerSecond);
        holdDuration = Mathf.Max(0f, holdDuration);
        fallDuration = Mathf.Max(0.01f, fallDuration);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        perGlyphFallDelay = Mathf.Max(0f, perGlyphFallDelay);
        gravity = Mathf.Max(0f, gravity);
        vanishScale = Mathf.Max(0f, vanishScale);

        if (destroyOnComplete)
            disableOnComplete = false;
    }

    void Update()
    {
        float now = Time.time;

        switch (state)
        {
            case EffectState.Streaming:
                UpdateStreaming(now);
                break;
            case EffectState.Holding:
                if (fallAfterHold && now - stateStartTime >= holdDuration)
                    BeginFall(now);
                break;
            case EffectState.Falling:
                if (now - stateStartTime >= fallDuration + fadeDuration + GetMaxGlyphDelay())
                    Complete();
                break;
        }
    }

    public void Play(string text)
    {
        PlayInternal(text, true);
    }

    public void PlayPersistent(string text)
    {
        PlayInternal(text, false);
    }

    void PlayInternal(string text, bool shouldFallAfterHold)
    {
        EnsureComponents();
        glyphStates.Clear();
        fullText = text ?? string.Empty;
        visibleCharacterCount = 0;
        nextCharacterTime = Time.time;
        fallAfterHold = shouldFallAfterHold;

        ResolveGround();
        textRenderer.PoseProvider = poseProvider;
        textRenderer.Text = string.Empty;

        if (fullText.Length == 0)
        {
            Complete();
            return;
        }

        SetState(EffectState.Streaming, Time.time);
    }

    public void Play(string text, Transform target)
    {
        if (target != null)
            transform.position = target.position + targetOffset;

        Play(text);
    }

    public void PlayAt(string text, Vector3 worldPosition)
    {
        transform.position = worldPosition;
        Play(text);
    }

    public void ShowImmediately(string text)
    {
        EnsureComponents();
        fullText = text ?? string.Empty;
        visibleCharacterCount = fullText.Length;
        fallAfterHold = true;
        textRenderer.Text = fullText;
        SetState(EffectState.Holding, Time.time);
    }

    public void ShowPersistentImmediately(string text)
    {
        EnsureComponents();
        fullText = text ?? string.Empty;
        visibleCharacterCount = fullText.Length;
        fallAfterHold = false;
        textRenderer.Text = fullText;
        SetState(EffectState.Holding, Time.time);
    }

    public void Clear()
    {
        EnsureComponents();
        glyphStates.Clear();
        fullText = string.Empty;
        visibleCharacterCount = 0;
        fallAfterHold = false;
        textRenderer.Text = string.Empty;
        SetState(EffectState.Idle, Time.time);
    }

    [ContextMenu("Begin Fall Now")]
    public void BeginFallNow()
    {
        EnsureComponents();
        glyphStates.Clear();
        ResolveGround();
        textRenderer.PoseProvider = poseProvider;

        // if (!string.IsNullOrEmpty(fullText))
        //     textRenderer.Text = fullText;

        BeginFall(Time.time);
    }

    [ContextMenu("Test Fall Text")]
    public void TestFallText()
    {
        Play(testText);
    }

    void UpdateStreaming(float now)
    {
        float interval = 1f / charactersPerSecond;

        while (state == EffectState.Streaming && now >= nextCharacterTime)
        {
            visibleCharacterCount++;
            textRenderer.Text = fullText.Substring(0, visibleCharacterCount);

            if (visibleCharacterCount >= fullText.Length)
            {
                SetState(EffectState.Holding, now);
                break;
            }

            nextCharacterTime += interval;
        }
    }

    void BeginFall(float now)
    {
        glyphStates.Clear();
        ResolveGround();
        textRenderer.PoseProvider = poseProvider;
        SetState(EffectState.Falling, now);
    }

    InstancedTextRenderer.GlyphPose GetGlyphPose(int glyphIndex, char character, Vector3 baseLocalPosition)
    {
        if (state != EffectState.Falling)
            return InstancedTextRenderer.GlyphPose.Identity;

        EnsureGlyphState(glyphIndex);

        GlyphFallState glyphState = glyphStates[glyphIndex];
        float age = Time.time - stateStartTime - glyphState.delay;

        if (age <= 0f)
            return InstancedTextRenderer.GlyphPose.Identity;

        Vector3 offset = CalculateFallOffset(glyphState, age, baseLocalPosition);
        Vector3 rotation = glyphState.angularVelocity * age;
        float fadeT = Mathf.Clamp01((age - fallDuration) / fadeDuration);
        float scale = Mathf.Lerp(1f, vanishScale, fadeT);

        return new InstancedTextRenderer.GlyphPose
        {
            localOffset = offset,
            localRotation = Quaternion.Euler(rotation),
            scale = scale,
            alpha = 1f - fadeT
        };
    }

    Vector3 CalculateFallOffset(GlyphFallState glyphState, float age, Vector3 baseLocalPosition)
    {
        Vector3 offset = glyphState.velocity * age;
        offset.y -= 0.5f * gravity * age * age;

        float groundOffsetY = groundLocalY - baseLocalPosition.y;
        if (offset.y < groundOffsetY)
        {
            float slideAge = Mathf.Max(0f, age - GetTimeToGround(glyphState.velocity.y, baseLocalPosition.y));
            offset.y = groundOffsetY;
            offset.x += glyphState.velocity.x * slideAge * 0.35f;
            offset.z += glyphState.velocity.z * slideAge * 0.35f;
        }

        return offset;
    }

    float GetTimeToGround(float initialVelocityY, float baseLocalY)
    {
        float distanceToGround = Mathf.Max(0f, baseLocalY - groundLocalY);
        float gravityValue = Mathf.Max(0.0001f, gravity);
        return (initialVelocityY + Mathf.Sqrt(initialVelocityY * initialVelocityY + 2f * gravityValue * distanceToGround)) / gravityValue;
    }

    void EnsureGlyphState(int glyphIndex)
    {
        while (glyphStates.Count <= glyphIndex)
        {
            int index = glyphStates.Count;
            System.Random random = new System.Random(randomSeed + index * 7919);
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float horizontalSpeed = RandomRange(random, horizontalSpeedRange.x, horizontalSpeedRange.y);
            float upwardSpeed = RandomRange(random, upwardSpeedRange.x, upwardSpeedRange.y);
            float tumbleSpeed = RandomRange(random, tumbleSpeedRange.x, tumbleSpeedRange.y);
            float tumbleSign = random.Next(0, 2) == 0 ? -1f : 1f;

            glyphStates.Add(new GlyphFallState
            {
                velocity = new Vector3(Mathf.Cos(angle) * horizontalSpeed, upwardSpeed, Mathf.Sin(angle) * horizontalSpeed),
                angularVelocity = new Vector3(tumbleSpeed * tumbleSign, tumbleSpeed * 0.25f, tumbleSpeed * -tumbleSign * 0.35f),
                delay = index * perGlyphFallDelay
            });
        }
    }

    void ResolveGround()
    {
        groundLocalY = fallbackGroundLocalY;

        if (!useGroundRaycast)
            return;

        Vector3 origin = transform.position + Vector3.up * groundRaycastHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundLocalY = transform.InverseTransformPoint(hit.point + Vector3.up * groundYOffset).y;
        }
    }

    void Complete()
    {
        SetState(EffectState.Done, Time.time);
        textRenderer.Text = string.Empty;

        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
        else if (disableOnComplete)
        {
            gameObject.SetActive(false);
        }
    }

    void SetState(EffectState nextState, float now)
    {
        state = nextState;
        stateStartTime = now;
    }

    float GetMaxGlyphDelay()
    {
        if (glyphStates.Count == 0)
            return 0f;

        return (glyphStates.Count - 1) * perGlyphFallDelay;
    }

    float RandomRange(System.Random random, float min, float max)
    {
        if (max < min)
        {
            float temp = min;
            min = max;
            max = temp;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    void EnsureComponents()
    {
        if (textRenderer == null)
            textRenderer = GetComponent<InstancedTextRenderer>();

        if (poseProvider == null)
            poseProvider = GetGlyphPose;
    }
}
