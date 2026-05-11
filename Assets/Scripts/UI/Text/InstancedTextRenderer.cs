using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders text using GPU instancing. Each glyph (Latin: per character, Arabic: per word)
/// is a mesh instance drawn via Graphics.DrawMeshInstanced.
///
/// Line direction is detected automatically:
///   - A line containing any Arabic character → RTL, word-mesh mode
///   - Otherwise → LTR, character-mesh mode
///
/// Arabic word meshes are supplied at runtime via RegisterWordMesh().
/// Missing Arabic word meshes are skipped until they are registered; ready words still draw.
/// </summary>
public class InstancedTextRenderer : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Types
    // -------------------------------------------------------------------------

    public enum Align { Left, Center, Right }

    [System.Serializable]
    public class GlyphEntry
    {
        public char  character;
        public Mesh  mesh;
    }

    /// Per-glyph pose override supplied by external code (animations, effects).
    public struct GlyphPose
    {
        public Vector3    localOffset;
        public Quaternion localRotation;
        public float      scale;
        public float      alpha;

        public static GlyphPose Identity => new GlyphPose
        {
            localOffset   = Vector3.zero,
            localRotation = Quaternion.identity,
            scale         = 1f,
            alpha         = 1f,
        };
    }

    public delegate GlyphPose GlyphPoseProvider(int glyphIndex, char character, Vector3 baseLocalPosition);

    // -------------------------------------------------------------------------
    // Internal batch bookkeeping
    // -------------------------------------------------------------------------

    class BatchData
    {
        public readonly List<Matrix4x4> matrices  = new();
        public readonly List<float>     introAges = new();
        public readonly List<float>     alphas    = new();

        public void Clear() { matrices.Clear(); introAges.Clear(); alphas.Clear(); }
    }

    // -------------------------------------------------------------------------
    // Shader property IDs
    // -------------------------------------------------------------------------

    static readonly int RiseDistanceId  = Shader.PropertyToID("_RiseDistance");
    static readonly int RiseDurationId  = Shader.PropertyToID("_RiseDuration");
    static readonly int SettleStrengthId = Shader.PropertyToID("_SettleStrength");
    static readonly int IntroAgeId      = Shader.PropertyToID("_IntroAge");
    static readonly int TextAlphaId     = Shader.PropertyToID("_TextAlpha");

    const int MaxInstancesPerBatch = 1023;

    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Text")]
    [TextArea]
    [SerializeField] string text = "Hello";

    [Header("Glyphs — Latin Characters")]
    [SerializeField] GlyphEntry[] glyphEntries;
    [SerializeField] Mesh         fallbackMesh;

    [Header("Rendering")]
    [SerializeField] Material material;

    [Header("Layout")]
    [SerializeField] Align align        = Align.Left;
    [SerializeField] float letterSpacing = 0.1f;
    [SerializeField] float wordSpacing   = 0.5f;   // space between words (LTR) or Arabic word gap (RTL)
    [SerializeField] float lineHeight    = 1.2f;

    [Header("Glyph Transform")]
    [SerializeField] Vector3 glyphScale = Vector3.one;

    [Header("Intro Animation")]
    [SerializeField] bool  playIntroAnimation = true;
    [SerializeField] float riseDistance       = 0.35f;
    [SerializeField] float riseDuration       = 0.35f;
    [SerializeField] float settleStrength     = 0.15f;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // Latin character → mesh
    readonly Dictionary<char, Mesh>   charMeshLookup   = new();

    // Arabic word string → mesh (populated by WordMeshLoader)
    readonly Dictionary<string, Mesh> wordMeshRegistry = new();

    // Batching
    readonly Dictionary<Mesh, BatchData> batches     = new();
    readonly Matrix4x4[]                 matrixChunk = new Matrix4x4[MaxInstancesPerBatch];
    readonly float[]                     ageChunk    = new float[MaxInstancesPerBatch];
    readonly float[]                     alphaChunk  = new float[MaxInstancesPerBatch];

    // Spawn times for intro animation — indexed per logical glyph slot
    readonly List<float>  glyphSpawnTimes       = new();
    readonly List<string> previousVisibleGlyphs = new();
    readonly List<string> visibleGlyphs         = new();

    MaterialPropertyBlock properties;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;
            text = value;
            RefreshSpawnTimes(forceAll: false);
        }
    }

    public GlyphPoseProvider PoseProvider { get; set; }

    /// Called by WordMeshLoader when a mesh arrives from the server.
    public void RegisterWordMesh(string word, Mesh mesh)
    {
        if (string.IsNullOrEmpty(word) || mesh == null) return;
        bool wasMissing = !wordMeshRegistry.ContainsKey(word);
        wordMeshRegistry[word] = mesh;

        if (wasMissing)
            RestartVisibleWordIntro(word);
    }

    /// Returns true only if every Arabic word in the current text has a mesh ready.
    public bool IsFullyLoaded()
    {
        if (string.IsNullOrEmpty(text)) return true;
        foreach (string line in text.Split('\n'))
        {
            if (!IsArabicLine(line)) continue;
            foreach (string word in SplitWords(line))
                if (!wordMeshRegistry.ContainsKey(word)) return false;
        }
        return true;
    }

    public void RestartIntro() => RefreshSpawnTimes(forceAll: true);

    // -------------------------------------------------------------------------
    // Unity messages
    // -------------------------------------------------------------------------

    void Awake()
    {
        properties = new MaterialPropertyBlock();
        BuildCharLookup();
    }

    void OnEnable()  => RestartIntro();
    void OnValidate() => BuildCharLookup();

    void LateUpdate() => DrawText();

    // -------------------------------------------------------------------------
    // Lookup build
    // -------------------------------------------------------------------------

    void BuildCharLookup()
    {
        charMeshLookup.Clear();
        if (glyphEntries == null) return;
        foreach (GlyphEntry e in glyphEntries)
            if (e?.mesh != null)
                charMeshLookup[e.character] = e.mesh;
    }

    // -------------------------------------------------------------------------
    // Main draw
    // -------------------------------------------------------------------------

    void DrawText()
    {
        if (material == null || string.IsNullOrEmpty(text)) return;

        RefreshSpawnTimes(forceAll: false);

        foreach (BatchData b in batches.Values) b.Clear();

        string[] lines     = text.Split('\n');
        int      glyphIdx  = 0;

        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (IsArabicLine(line))
                DrawLineRTL(line, li, ref glyphIdx);
            else
                DrawLineLTR(line, li, ref glyphIdx);
        }

        foreach (KeyValuePair<Mesh, BatchData> kv in batches)
            FlushBatch(kv.Key, kv.Value);
    }

    // -------------------------------------------------------------------------
    // LTR — Latin character-by-character
    // -------------------------------------------------------------------------

    void DrawLineLTR(string line, int lineIndex, ref int glyphIdx)
    {
        string[] words  = SplitWords(line);
        float    startX = GetAlignedStartX(MeasureLineLTR(line));
        float    curX   = startX;
        float    curY   = -lineIndex * lineHeight;

        // Front facing fix:
        Vector3 temp = glyphScale;
        float correction = -Mathf.Abs(glyphScale.x);
        glyphScale = new Vector3(correction, temp.y, temp.z);

        for (int wi = 0; wi < words.Length; wi++)
        {
            foreach (char ch in words[wi])
            {
                Mesh m = GetCharMesh(ch);
                if (m == null) continue;
                PlaceGlyph(m, curX, curY, glyphIdx, ch);
                curX += GlyphAdvanceX(m) + letterSpacing;
                glyphIdx++;
            }
            if (wi < words.Length - 1) curX += wordSpacing;
        }
    }

    float MeasureLineLTR(string line)
    {
        float w = 0f;
        bool  hasGlyph = false;
        foreach (char ch in line)
        {
            if (ch == ' ') { w += wordSpacing; continue; }
            Mesh m = GetCharMesh(ch);
            if (m == null) continue;
            w += GlyphAdvanceX(m) + letterSpacing;
            hasGlyph = true;
        }
        if (hasGlyph) w -= letterSpacing;
        return w;
    }

    // -------------------------------------------------------------------------
    // RTL — Arabic word-by-word
    // -------------------------------------------------------------------------

    void DrawLineRTL(string line, int lineIndex, ref int glyphIdx)
    {
        string[] words = SplitWords(line);

        // Front facing fix:
        Vector3 temp = glyphScale;
        float correction = Mathf.Abs(glyphScale.x);
        glyphScale = new Vector3(correction, temp.y, temp.z);

        // Measure total line width
        float lineWidth = 0f;
        bool  hasMesh   = false;
        for (int wi = 0; wi < words.Length; wi++)
        {
            if (!wordMeshRegistry.TryGetValue(words[wi], out Mesh mesh))
                continue;

            if (hasMesh) lineWidth += wordSpacing;
            lineWidth += GlyphAdvanceX(mesh);
            hasMesh = true;
        }

        if (!hasMesh)
        {
            glyphIdx += words.Length;
            return;
        }

        // RTL: start from the right-most position and move left
        // GetAlignedStartX gives left edge; for RTL the first word's
        // right edge sits at (startX + lineWidth).
        float startX = GetAlignedStartX(lineWidth);
        float curX   = startX + lineWidth;   // cursor = right edge of current word
        float curY   = -lineIndex * lineHeight;

        foreach (string word in words)
        {
            if (!wordMeshRegistry.TryGetValue(word, out Mesh m))
            {
                glyphIdx++;
                continue;
            }

            float wordWidth = GlyphAdvanceX(m);

            // Place so the word's right edge is at curX
            float placeX = curX - wordWidth;
            PlaceGlyph(m, placeX, curY, glyphIdx, '\uFFFF');
            curX -= wordWidth + wordSpacing;
            glyphIdx++;
        }
    }

    // -------------------------------------------------------------------------
    // Glyph placement
    // -------------------------------------------------------------------------

    void PlaceGlyph(Mesh mesh, float cursorX, float cursorY, int glyphIdx, char ch)
    {
        Bounds  bounds        = mesh.bounds;
        Vector3 localPosition = new Vector3(
            cursorX,
            cursorY - bounds.min.y * glyphScale.y,
            0f
        );

        GlyphPose pose      = PoseProvider?.Invoke(glyphIdx, ch, localPosition) ?? GlyphPose.Identity;
        float     poseScale = Mathf.Max(0f, pose.scale);
        localPosition      += pose.localOffset;

        Matrix4x4 local = Matrix4x4.TRS(localPosition, pose.localRotation, glyphScale * poseScale);

        if (!batches.TryGetValue(mesh, out BatchData batch))
        {
            batch = new BatchData();
            batches.Add(mesh, batch);
        }

        batch.matrices.Add(transform.localToWorldMatrix * local);
        batch.introAges.Add(GetGlyphIntroAge(glyphIdx));
        batch.alphas.Add(Mathf.Clamp01(pose.alpha));
    }

    // -------------------------------------------------------------------------
    // Batch flush
    // -------------------------------------------------------------------------

    void FlushBatch(Mesh mesh, BatchData batch)
    {
        properties ??= new MaterialPropertyBlock();
        List<Matrix4x4> mats   = batch.matrices;
        List<float>     ages   = batch.introAges;
        List<float>     alphas = batch.alphas;

        for (int start = 0; start < mats.Count; start += MaxInstancesPerBatch)
        {
            int count = Mathf.Min(MaxInstancesPerBatch, mats.Count - start);
            mats.CopyTo(start,   matrixChunk, 0, count);
            ages.CopyTo(start,   ageChunk,    0, count);
            alphas.CopyTo(start, alphaChunk,  0, count);

            properties.Clear();
            properties.SetFloat(RiseDistanceId,   playIntroAnimation ? Mathf.Max(0f, riseDistance) : 0f);
            properties.SetFloat(RiseDurationId,   Mathf.Max(0.0001f, riseDuration));
            properties.SetFloat(SettleStrengthId, Mathf.Max(0f, settleStrength));
            properties.SetFloatArray(IntroAgeId,  ageChunk);
            properties.SetFloatArray(TextAlphaId, alphaChunk);

            Graphics.DrawMeshInstanced(mesh, 0, material, matrixChunk, count, properties);
        }
    }

    // -------------------------------------------------------------------------
    // Intro animation spawn times
    // -------------------------------------------------------------------------

    void RefreshSpawnTimes(bool forceAll)
    {
        BuildVisibleGlyphList(text, visibleGlyphs);

        if (!forceAll && ListsMatch(visibleGlyphs, previousVisibleGlyphs)) return;

        float       now           = AnimTime();
        List<float> previousTimes = new List<float>(glyphSpawnTimes);
        int         prevCount     = previousVisibleGlyphs.Count;
        int         currCount     = visibleGlyphs.Count;

        // Find unchanged prefix / suffix
        int prefix = 0, suffix = 0;
        if (!forceAll)
        {
            while (prefix < prevCount && prefix < currCount &&
                   previousVisibleGlyphs[prefix] == visibleGlyphs[prefix]) prefix++;

            while (suffix + prefix < prevCount && suffix + prefix < currCount &&
                   previousVisibleGlyphs[prevCount - 1 - suffix] == visibleGlyphs[currCount - 1 - suffix]) suffix++;
        }

        glyphSpawnTimes.Clear();
        for (int i = 0; i < currCount; i++)
        {
            if (i < prefix)
                glyphSpawnTimes.Add(SafeSpawnTime(previousTimes, i, now));
            else if (i >= currCount - suffix)
                glyphSpawnTimes.Add(SafeSpawnTime(previousTimes, prevCount - (currCount - i), now));
            else
                glyphSpawnTimes.Add(now);
        }

        previousVisibleGlyphs.Clear();
        previousVisibleGlyphs.AddRange(visibleGlyphs);
    }

    void BuildVisibleGlyphList(string src, List<string> target)
    {
        target.Clear();
        if (string.IsNullOrEmpty(src)) return;

        string[] lines = src.Split('\n');

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (IsArabicLine(line))
            {
                string[] words = SplitWords(line);

                foreach (string word in words)
                {
                    // One visible glyph slot per Arabic word mesh. Use the word
                    // itself as the animation identity so a growing streamed word
                    // refreshes its intro timer instead of looking unchanged.
                    target.Add(word);
                }
            }
            else
            {
                foreach (char ch in line)
                {
                    if (ch == ' ' || ch == '\n' || ch == '\r')
                        continue;

                    target.Add(ch.ToString());
                }
            }
        }
    }

    float GetGlyphIntroAge(int idx)
    {
        if (!playIntroAnimation || idx < 0 || idx >= glyphSpawnTimes.Count)
            return riseDuration;
        return Mathf.Max(0f, AnimTime() - glyphSpawnTimes[idx]);
    }

    float SafeSpawnTime(List<float> times, int idx, float fallback)
        => (idx >= 0 && idx < times.Count) ? times[idx] : fallback;

    void RestartVisibleWordIntro(string word)
    {
        RefreshSpawnTimes(forceAll: false);

        float now = AnimTime();
        for (int i = 0; i < visibleGlyphs.Count && i < glyphSpawnTimes.Count; i++)
        {
            if (visibleGlyphs[i] == word)
                glyphSpawnTimes[i] = now;
        }
    }

    bool ListsMatch(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    float AnimTime() => Application.isPlaying ? Time.time : 0f;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    Mesh GetCharMesh(char ch)
    {
        if (charMeshLookup.TryGetValue(ch, out Mesh m)) return m;
        return fallbackMesh;
    }

    float GlyphAdvanceX(Mesh mesh)
        => Mathf.Abs(mesh.bounds.size.x * glyphScale.x);

    float GetAlignedStartX(float lineWidth) => align switch
    {
        Align.Center => -lineWidth * 0.5f,
        Align.Right  => -lineWidth,
        _            => 0f,
    };

    /// Splits a line into non-empty words (splits on whitespace).
    static string[] SplitWords(string line)
        => line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

    /// A line is Arabic if it contains at least one Arabic Unicode character (U+0600–U+06FF).
    public static bool IsArabicLine(string line)
    {
        foreach (char ch in line)
            if (ch >= '\u0600' && ch <= '\u06FF') return true;
        return false;
    }
}
