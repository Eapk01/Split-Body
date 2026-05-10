using System.Collections.Generic;
using UnityEngine;

public class InstancedTextRenderer : MonoBehaviour
{
    const int MaxInstancesPerDraw = 1023;

    public enum Align
    {
        Left,
        Center,
        Right
    }

    [System.Serializable]
    public class GlyphEntry
    {
        public char character;
        public Mesh mesh;
    }

    public struct GlyphPose
    {
        public Vector3 localOffset;
        public Quaternion localRotation;
        public float scale;
        public float alpha;

        public static GlyphPose Identity => new GlyphPose
        {
            localOffset = Vector3.zero,
            localRotation = Quaternion.identity,
            scale = 1f,
            alpha = 1f
        };
    }

    public delegate GlyphPose GlyphPoseProvider(int glyphIndex, char character, Vector3 baseLocalPosition);

    class BatchData
    {
        public readonly List<Matrix4x4> matrices = new();
        public readonly List<float> introAges = new();
        public readonly List<float> alphas = new();

        public void Clear()
        {
            matrices.Clear();
            introAges.Clear();
            alphas.Clear();
        }
    }

    static readonly int RiseDistanceId = Shader.PropertyToID("_RiseDistance");
    static readonly int RiseDurationId = Shader.PropertyToID("_RiseDuration");
    static readonly int SettleStrengthId = Shader.PropertyToID("_SettleStrength");
    static readonly int IntroAgeId = Shader.PropertyToID("_IntroAge");
    static readonly int TextAlphaId = Shader.PropertyToID("_TextAlpha");

    [Header("Text")]
    [TextArea]
    [SerializeField] string text = "ABC";

    [Header("Glyphs")]
    [SerializeField] GlyphEntry[] glyphEntries;
    [SerializeField] Mesh fallbackMesh;

    [Header("Rendering")]
    [SerializeField] Material material;

    [Header("Layout")]
    [SerializeField] Align align = Align.Left;
    [SerializeField] float letterSpacing = 0.1f;
    [SerializeField] float spaceAdvance = 0.75f;
    [SerializeField] float lineHeight = 1.2f;

    [Header("Transform")]
    [SerializeField] Vector3 glyphScale = Vector3.one;

    [Header("Intro Animation")]
    [SerializeField] bool playIntroAnimation = true;
    [SerializeField] float riseDistance = 0.35f;
    [SerializeField] float riseDuration = 0.35f;
    [SerializeField] float settleStrength = 0.15f;

    readonly Dictionary<char, Mesh> glyphLookup = new();
    readonly Dictionary<Mesh, BatchData> batches = new();
    readonly List<char> visibleGlyphs = new();
    readonly List<char> previousVisibleGlyphs = new();
    readonly List<float> glyphSpawnTimes = new();
    readonly Matrix4x4[] matrixChunk = new Matrix4x4[MaxInstancesPerDraw];
    readonly float[] introAgeChunk = new float[MaxInstancesPerDraw];
    readonly float[] alphaChunk = new float[MaxInstancesPerDraw];
    MaterialPropertyBlock properties;

    public string Text
    {
        get => text;
        set
        {
            if (text == value)
                return;

            text = value;
            RefreshGlyphSpawnTimes(false);
        }
    }

    public GlyphPoseProvider PoseProvider { get; set; }

    void Awake()
    {
        properties = new MaterialPropertyBlock();
        BuildGlyphLookup();
    }

    void OnEnable()
    {
        RestartIntro();
    }

    void OnValidate()
    {
        BuildGlyphLookup();
    }

    void LateUpdate()
    {
        DrawText();
    }

    void BuildGlyphLookup()
    {
        glyphLookup.Clear();

        if (glyphEntries == null)
            return;

        foreach (GlyphEntry entry in glyphEntries)
        {
            if (entry == null || entry.mesh == null)
                continue;

            glyphLookup[entry.character] = entry.mesh;
        }
    }

    void DrawText()
    {
        if (material == null)
            return;

        RefreshGlyphSpawnTimes(false);

        if (string.IsNullOrEmpty(text))
            return;

        foreach (BatchData batch in batches.Values)
        {
            batch.Clear();
        }

        string[] lines = text.Split('\n');
        int glyphIndex = 0;
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            DrawLine(lines[lineIndex], lineIndex, ref glyphIndex);
        }

        foreach (KeyValuePair<Mesh, BatchData> batch in batches)
        {
            DrawBatch(batch.Key, batch.Value);
        }
    }

    void DrawLine(string line, int lineIndex, ref int glyphIndex)
    {
        float cursorX = GetAlignedStartX(GetLineWidth(line));
        float cursorY = -lineIndex * lineHeight;

        foreach (char character in line)
        {
            if (character == ' ')
            {
                cursorX += spaceAdvance;
                continue;
            }

            Mesh mesh = GetMesh(character);
            if (mesh == null)
                continue;

            AddGlyph(mesh, cursorX, cursorY, glyphIndex, character);
            cursorX += GetGlyphAdvance(mesh) + letterSpacing;
            glyphIndex++;
        }
    }

    Mesh GetMesh(char character)
    {
        if (glyphLookup.TryGetValue(character, out Mesh mesh))
            return mesh;

        return fallbackMesh;
    }

    void AddGlyph(Mesh mesh, float cursorX, float cursorY, int glyphIndex, char character)
    {
        Bounds bounds = mesh.bounds;
        Vector3 localPosition = new Vector3(
            cursorX,
            cursorY - bounds.min.y * glyphScale.y,
            0f
        );
        GlyphPose glyphPose = PoseProvider?.Invoke(glyphIndex, character, localPosition) ?? GlyphPose.Identity;
        float glyphPoseScale = Mathf.Max(0f, glyphPose.scale);
        localPosition += glyphPose.localOffset;

        Matrix4x4 localMatrix = Matrix4x4.TRS(
            localPosition,
            glyphPose.localRotation,
            glyphScale * glyphPoseScale
        );

        if (!batches.TryGetValue(mesh, out BatchData batch))
        {
            batch = new BatchData();
            batches.Add(mesh, batch);
        }

        batch.matrices.Add(transform.localToWorldMatrix * localMatrix);
        batch.introAges.Add(GetGlyphIntroAge(glyphIndex));
        batch.alphas.Add(Mathf.Clamp01(glyphPose.alpha));
    }

    float GetLineWidth(string line)
    {
        if (string.IsNullOrEmpty(line))
            return 0f;

        float width = 0f;
        bool hasGlyph = false;

        foreach (char character in line)
        {
            if (character == ' ')
            {
                width += spaceAdvance;
                continue;
            }

            Mesh mesh = GetMesh(character);
            if (mesh == null)
                continue;

            width += GetGlyphAdvance(mesh) + letterSpacing;
            hasGlyph = true;
        }

        if (hasGlyph)
            width -= letterSpacing;

        return width;
    }

    float GetGlyphAdvance(Mesh mesh)
    {
        return Mathf.Abs(mesh.bounds.max.x * glyphScale.x);
    }

    float GetAlignedStartX(float lineWidth)
    {
        switch (align)
        {
            case Align.Center:
                return -lineWidth * 0.5f;
            case Align.Right:
                return -lineWidth;
            default:
                return 0f;
        }
    }

    void DrawBatch(Mesh mesh, BatchData batch)
    {
        properties ??= new MaterialPropertyBlock();

        List<Matrix4x4> matrices = batch.matrices;
        List<float> introAges = batch.introAges;
        List<float> alphas = batch.alphas;

        for (int start = 0; start < matrices.Count; start += MaxInstancesPerDraw)
        {
            int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - start);
            matrices.CopyTo(start, matrixChunk, 0, count);
            introAges.CopyTo(start, introAgeChunk, 0, count);
            alphas.CopyTo(start, alphaChunk, 0, count);

            properties.Clear();
            properties.SetFloat(RiseDistanceId, playIntroAnimation ? Mathf.Max(0f, riseDistance) : 0f);
            properties.SetFloat(RiseDurationId, Mathf.Max(0.0001f, riseDuration));
            properties.SetFloat(SettleStrengthId, Mathf.Max(0f, settleStrength));
            properties.SetFloatArray(IntroAgeId, introAgeChunk);
            properties.SetFloatArray(TextAlphaId, alphaChunk);

            Graphics.DrawMeshInstanced(mesh, 0, material, matrixChunk, count, properties);
        }
    }

    public void RestartIntro()
    {
        RefreshGlyphSpawnTimes(true);
    }

    void RefreshGlyphSpawnTimes(bool forceAll)
    {
        BuildVisibleGlyphs(text, visibleGlyphs);

        if (!forceAll && ListsMatch(visibleGlyphs, previousVisibleGlyphs))
            return;

        float now = GetAnimationTime();
        List<float> previousSpawnTimes = new List<float>(glyphSpawnTimes);
        int previousCount = previousVisibleGlyphs.Count;
        int currentCount = visibleGlyphs.Count;
        int prefixCount = 0;

        if (!forceAll)
        {
            while (prefixCount < previousCount &&
                   prefixCount < currentCount &&
                   previousVisibleGlyphs[prefixCount] == visibleGlyphs[prefixCount])
            {
                prefixCount++;
            }
        }

        int suffixCount = 0;
        if (!forceAll)
        {
            while (suffixCount + prefixCount < previousCount &&
                   suffixCount + prefixCount < currentCount &&
                   previousVisibleGlyphs[previousCount - 1 - suffixCount] == visibleGlyphs[currentCount - 1 - suffixCount])
            {
                suffixCount++;
            }
        }

        glyphSpawnTimes.Clear();
        for (int index = 0; index < currentCount; index++)
        {
            bool isPrefix = index < prefixCount;
            bool isSuffix = index >= currentCount - suffixCount;

            if (isPrefix)
            {
                glyphSpawnTimes.Add(GetPreviousSpawnTime(previousSpawnTimes, index, now));
            }
            else if (isSuffix)
            {
                int previousIndex = previousCount - (currentCount - index);
                glyphSpawnTimes.Add(GetPreviousSpawnTime(previousSpawnTimes, previousIndex, now));
            }
            else
            {
                glyphSpawnTimes.Add(now);
            }
        }

        previousVisibleGlyphs.Clear();
        previousVisibleGlyphs.AddRange(visibleGlyphs);
    }

    void BuildVisibleGlyphs(string sourceText, List<char> target)
    {
        target.Clear();

        if (string.IsNullOrEmpty(sourceText))
            return;

        foreach (char character in sourceText)
        {
            if (character == ' ' || character == '\n' || character == '\r')
                continue;

            if (GetMesh(character) != null)
                target.Add(character);
        }
    }

    bool ListsMatch(List<char> left, List<char> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index])
                return false;
        }

        return true;
    }

    float GetGlyphIntroAge(int glyphIndex)
    {
        if (!playIntroAnimation || glyphIndex < 0 || glyphIndex >= glyphSpawnTimes.Count)
            return riseDuration;

        return Mathf.Max(0f, GetAnimationTime() - glyphSpawnTimes[glyphIndex]);
    }

    float GetPreviousSpawnTime(List<float> previousSpawnTimes, int index, float fallback)
    {
        if (index < 0 || index >= previousSpawnTimes.Count)
            return fallback;

        return previousSpawnTimes[index];
    }

    float GetAnimationTime()
    {
        return Application.isPlaying ? Time.time : 0f;
    }
}
