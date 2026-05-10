using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InstancedTextRenderer))]
[RequireComponent(typeof(InstancedTextStreamer))]
public class FloatingTextFallEffect : MonoBehaviour
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

    [Header("Follow")]
    [SerializeField] Transform followTarget;
    [SerializeField] Vector3 followOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] bool followUntilFall = true;

    [Header("Timing")]
    [SerializeField] float holdDuration = 0.45f;
    [SerializeField] float fallDuration = 0.7f;
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField] float perGlyphFallDelay = 0.025f;

    [Header("Fall")]
    [SerializeField] float gravity = 12f;
    [SerializeField] Vector2 horizontalSpeedRange = new Vector2(0.15f, 0.55f);
    [SerializeField] Vector2 upwardSpeedRange = new Vector2(0.4f, 1.1f);
    [SerializeField] Vector2 tumbleSpeedRange = new Vector2(180f, 420f);
    [SerializeField] float vanishScale = 0.6f;
    [SerializeField] int randomSeed = 12345;

    [Header("Ground")]
    [SerializeField] bool useGroundRaycast = true;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float groundRaycastHeight = 2f;
    [SerializeField] float groundRaycastDistance = 8f;
    [SerializeField] float groundYOffset = 0.02f;
    [SerializeField] float fallbackGroundLocalY = -1.5f;

    [Header("Completion")]
    [SerializeField] bool disableOnComplete;
    [SerializeField] bool destroyOnComplete;

    readonly List<GlyphFallState> glyphStates = new();
    InstancedTextRenderer textRenderer;
    InstancedTextStreamer streamer;
    InstancedTextRenderer.GlyphPoseProvider poseProvider;
    EffectState state;
    float stateStartTime;
    float groundLocalY;

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

    void Update()
    {
        float now = Time.time;

        if (ShouldFollowTarget())
            transform.position = followTarget.position + followOffset;

        switch (state)
        {
            case EffectState.Streaming:
                if (!streamer.IsStreaming)
                    SetState(EffectState.Holding, now);
                break;
            case EffectState.Holding:
                if (now - stateStartTime >= Mathf.Max(0f, holdDuration))
                    SetState(EffectState.Falling, now);
                break;
            case EffectState.Falling:
                if (now - stateStartTime >= Mathf.Max(0f, fallDuration) + Mathf.Max(0.01f, fadeDuration) + GetMaxGlyphDelay())
                    Complete();
                break;
        }
    }

    public void Play(string text)
    {
        EnsureComponents();
        glyphStates.Clear();
        if (followTarget != null)
            transform.position = followTarget.position + followOffset;

        ResolveGround();
        textRenderer.PoseProvider = poseProvider;
        streamer.Stream(text);
        SetState(EffectState.Streaming, Time.time);
    }

    public void Play(string text, Transform target)
    {
        followTarget = target;
        Play(text);
    }

    public void PlayAt(string text, Vector3 worldPosition)
    {
        followTarget = null;
        transform.position = worldPosition;
        Play(text);
    }

    [ContextMenu("Begin Fall Now")]
    public void BeginFallNow()
    {
        EnsureComponents();
        glyphStates.Clear();
        ResolveGround();
        textRenderer.PoseProvider = poseProvider;
        SetState(EffectState.Falling, Time.time);
    }

    [ContextMenu("Test Fall Text")]
    public void TestFallText()
    {
        Play(testText);
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
        float fadeT = Mathf.Clamp01((age - Mathf.Max(0f, fallDuration)) / Mathf.Max(0.01f, fadeDuration));
        float scale = Mathf.Lerp(1f, Mathf.Max(0f, vanishScale), fadeT);

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
        offset.y -= 0.5f * Mathf.Max(0f, gravity) * age * age;

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
                delay = index * Mathf.Max(0f, perGlyphFallDelay)
            });
        }
    }

    void ResolveGround()
    {
        groundLocalY = fallbackGroundLocalY;

        if (!useGroundRaycast)
            return;

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0f, groundRaycastHeight);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(0f, groundRaycastDistance), groundMask, QueryTriggerInteraction.Ignore))
        {
            groundLocalY = transform.InverseTransformPoint(hit.point + Vector3.up * groundYOffset).y;
        }
    }

    void SetState(EffectState nextState, float now)
    {
        state = nextState;
        stateStartTime = now;
    }

    void Complete()
    {
        SetState(EffectState.Done, Time.time);
        streamer.ShowImmediately(string.Empty);

        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
        else if (disableOnComplete)
        {
            gameObject.SetActive(false);
        }
    }

    bool ShouldFollowTarget()
    {
        if (!followUntilFall || followTarget == null)
            return false;

        return state == EffectState.Streaming || state == EffectState.Holding;
    }

    float GetMaxGlyphDelay()
    {
        if (glyphStates.Count == 0)
            return 0f;

        return (glyphStates.Count - 1) * Mathf.Max(0f, perGlyphFallDelay);
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

        if (streamer == null)
            streamer = GetComponent<InstancedTextStreamer>();

        if (poseProvider == null)
            poseProvider = GetGlyphPose;
    }
}
