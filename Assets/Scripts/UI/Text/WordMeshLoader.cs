using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Fetches 3-D word meshes for Arabic text from the Python generation server,
/// caches them in memory, and registers them with InstancedTextRenderer.
///
/// Usage:
///   1. Attach to the same GameObject as InstancedTextRenderer (or assign via Inspector).
///   2. Set serverUrl to point at your Flask server.
///   3. Call SetText(str) instead of writing renderer.Text directly —
///      this triggers prefetch and only commits the text once all meshes are ready.
/// </summary>
public class WordMeshLoader : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("References")]
    [SerializeField] InstancedTextRenderer textRenderer;

    [Header("Server")]
    [SerializeField] string serverUrl = "http://localhost:5000";

    [Header("Debug")]
    [SerializeField] bool logFetches = true;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // Persistent in-memory mesh cache: word → mesh
    readonly Dictionary<string, Mesh> meshCache = new();

    // Words currently being fetched — prevents duplicate in-flight requests
    readonly HashSet<string> inFlight = new();

    // Pending SetText request — we hold it until all its Arabic words are ready
    string pendingText;
    bool   hasPendingText;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// Set text on the renderer.
    /// Arabic words are prefetched; the text is committed once all are ready.
    /// Latin text is committed immediately (no meshes to wait for).
    public void SetText(string newText)
    {
        if (string.IsNullOrEmpty(newText))
        {
            CommitText(newText);
            return;
        }

        List<string> missing = GetMissingArabicWords(newText);

        if (missing.Count == 0)
        {
            // Everything already in cache — commit right away
            CommitText(newText);
            return;
        }

        // Store pending and start fetching
        pendingText    = newText;
        hasPendingText = true;

        foreach (string word in missing)
            StartFetch(word);
    }

    /// Pre-warm the cache for a set of words without displaying anything.
    public void Prefetch(IEnumerable<string> words)
    {
        foreach (string word in words)
            if (!meshCache.ContainsKey(word) && !inFlight.Contains(word))
                StartFetch(word);
    }

    /// Returns true if every Arabic word in the given text has a cached mesh.
    public bool IsCached(string textToCheck)
        => GetMissingArabicWords(textToCheck).Count == 0;

    // -------------------------------------------------------------------------
    // Unity messages
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (textRenderer == null)
            textRenderer = GetComponent<InstancedTextRenderer>();
    }

    // -------------------------------------------------------------------------
    // Fetch pipeline
    // -------------------------------------------------------------------------

    void StartFetch(string word)
    {
        if (inFlight.Contains(word) || meshCache.ContainsKey(word)) return;
        inFlight.Add(word);
        StartCoroutine(FetchCoroutine(word));
    }

    IEnumerator FetchCoroutine(string word)
    {
        string url = $"{serverUrl}/glyph?word={UnityWebRequest.EscapeURL(word)}";

        if (logFetches)
            Debug.Log($"[WordMeshLoader] Fetching '{word}' → {url}");

        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        inFlight.Remove(word);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[WordMeshLoader] Failed to fetch '{word}': {req.error}");
            yield break;
        }

        Mesh mesh = ParseObj(req.downloadHandler.text, word);
        if (mesh == null)
        {
            Debug.LogError($"[WordMeshLoader] Empty geometry for '{word}'");
            yield break;
        }

        meshCache[word] = mesh;
        textRenderer.RegisterWordMesh(word, mesh);

        if (logFetches)
            Debug.Log($"[WordMeshLoader] Registered '{word}' ({mesh.vertexCount} verts)");

        // Check if this arrival completes a pending text request
        TryCommitPending();
    }

    void TryCommitPending()
    {
        if (!hasPendingText) return;
        if (GetMissingArabicWords(pendingText).Count > 0) return;

        CommitText(pendingText);
        hasPendingText = false;
        pendingText    = null;
    }

    void CommitText(string t)
    {
        textRenderer.Text = t;
    }

    [ContextMenu("Test Fall Text")]
    public void TestText()
    {
        SetText("السلام عليكم");
    }

    // -------------------------------------------------------------------------
    // OBJ parser — positions + triangles only
    // -------------------------------------------------------------------------

    static Mesh ParseObj(string obj, string debugName)
    {
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        foreach (string rawLine in obj.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                string[] t = line.Split(' ');
                if (t.Length < 4) continue;
                verts.Add(new Vector3(
                    float.Parse(t[1], CultureInfo.InvariantCulture),
                    float.Parse(t[2], CultureInfo.InvariantCulture),
                    float.Parse(t[3], CultureInfo.InvariantCulture)
                ));
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                string[] t = line.Split(' ');
                if (t.Length < 4) continue;
                // OBJ face indices are 1-based; may contain "v/vt/vn" — take first component
                tris.Add(ParseFaceIndex(t[1]) - 1);
                tris.Add(ParseFaceIndex(t[2]) - 1);
                tris.Add(ParseFaceIndex(t[3]) - 1);
            }
        }

        if (verts.Count == 0) return null;

        var mesh = new Mesh
        {
            name        = $"word_{debugName}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static int ParseFaceIndex(string token)
    {
        // "12", "12/34", "12/34/56", "12//56" — we only need the first number
        int slash = token.IndexOf('/');
        string s  = slash >= 0 ? token.Substring(0, slash) : token;
        return int.Parse(s, CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    List<string> GetMissingArabicWords(string src)
    {
        var missing = new List<string>();
        foreach (string line in src.Split('\n'))
        {
            if (!IsArabicLine(line)) continue;
            foreach (string word in SplitWords(line))
                if (!meshCache.ContainsKey(word))
                    missing.Add(word);
        }
        return missing;
    }

    static bool IsArabicLine(string line)
    {
        foreach (char ch in line)
            if (ch >= '\u0600' && ch <= '\u06FF') return true;
        return false;
    }

    public static string[] SplitWords(string line)
        => line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
}