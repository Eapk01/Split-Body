using UnityEngine;

[RequireComponent(typeof(InstancedTextRenderer))]
public class InstancedTextStreamer : MonoBehaviour
{
    [Header("Streaming")]
    [SerializeField] float charactersPerSecond = 24f;
    [SerializeField] bool useUnscaledTime;
    [SerializeField] bool clearOnEnable = true;
    [SerializeField] string testText = "PERFECT";

    InstancedTextRenderer textRenderer;
    string fullText = string.Empty;
    int visibleCharacterCount;
    float nextCharacterTime;
    bool isStreaming;

    public bool IsStreaming => isStreaming;

    void Awake()
    {
        EnsureRenderer();
    }

    void OnEnable()
    {
        if (clearOnEnable)
            SetVisibleText(string.Empty);
    }

    void Update()
    {
        if (!isStreaming)
            return;

        float interval = 1f / Mathf.Max(1f, charactersPerSecond);
        float now = GetTime();

        while (isStreaming && now >= nextCharacterTime)
        {
            visibleCharacterCount++;
            SetVisibleText(fullText.Substring(0, visibleCharacterCount));

            if (visibleCharacterCount >= fullText.Length)
            {
                isStreaming = false;
                break;
            }

            nextCharacterTime += interval;
        }
    }

    public void Stream(string text)
    {
        EnsureRenderer();

        fullText = text ?? string.Empty;
        visibleCharacterCount = 0;
        isStreaming = fullText.Length > 0;
        nextCharacterTime = GetTime();

        SetVisibleText(string.Empty);
    }

    public void ShowImmediately(string text)
    {
        EnsureRenderer();

        fullText = text ?? string.Empty;
        visibleCharacterCount = fullText.Length;
        isStreaming = false;

        SetVisibleText(fullText);
    }

    public void Stop(bool showFullText = true)
    {
        if (showFullText)
        {
            ShowImmediately(fullText);
            return;
        }

        isStreaming = false;
    }

    [ContextMenu("Test Stream")]
    public void TestStream()
    {
        Stream(testText);
    }

    void SetVisibleText(string value)
    {
        EnsureRenderer();
        textRenderer.Text = value;
    }

    void EnsureRenderer()
    {
        if (textRenderer == null)
            textRenderer = GetComponent<InstancedTextRenderer>();
    }

    float GetTime()
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }
}
