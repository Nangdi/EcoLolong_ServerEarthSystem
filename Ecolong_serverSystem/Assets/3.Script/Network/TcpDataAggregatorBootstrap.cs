using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TcpDataAggregatorBootstrap
{
    private const string KoreanFontResourcePath = "Fonts/NotoSansKR-Regular SDF";

    // 씬 로드 후 기본 TCP 수신기와 출력 UI를 생성합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateDefaultAggregator()
    {
        if (Object.FindObjectOfType<TcpDataAggregator>() != null)
            return;

        Canvas canvas = FindOrCreateCanvas();

        TextMeshProUGUI outputText = CreateOutputText(canvas.transform);
        GameObject aggregatorObject = new GameObject("TcpDataAggregator");
        TcpDataAggregator aggregator = aggregatorObject.AddComponent<TcpDataAggregator>();
        aggregator.SetResultText(outputText);
    }

    // TCP 전용 Canvas를 찾고, 없으면 새로 생성합니다.
    private static Canvas FindOrCreateCanvas()
    {
        GameObject existingCanvas = GameObject.Find("TcpDataCanvas");
        if (existingCanvas != null && existingCanvas.TryGetComponent(out Canvas canvas))
            return canvas;

        return CreateCanvas();
    }

    // TCP 상태 출력용 Screen Space Canvas를 생성합니다.
    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "TcpDataCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    // TCP 서버 상태와 누적 데이터를 표시할 TMP 텍스트를 생성합니다.
    private static TextMeshProUGUI CreateOutputText(Transform parent)
    {
        GameObject textObject = new GameObject("TcpDataOutputText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(40f, -40f);
        rectTransform.sizeDelta = new Vector2(620f, 420f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = "TCP/IP Data Aggregate";

        // 런타임에도 한글이 깨지지 않도록 Resources 폴더의 TMP 폰트를 우선 적용합니다.
        TMP_FontAsset koreanFont = LoadKoreanFont();
        if (koreanFont != null)
            text.font = koreanFont;

        return text;
    }

    // 자동 생성된 TMP 텍스트가 한글을 표시할 수 있도록 리소스 폰트를 불러옵니다.
    private static TMP_FontAsset LoadKoreanFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(KoreanFontResourcePath);
        if (font == null)
            Debug.LogWarning($"한글 TMP 폰트를 찾지 못했습니다. 경로: Resources/{KoreanFontResourcePath}");

        return font;
    }
}
