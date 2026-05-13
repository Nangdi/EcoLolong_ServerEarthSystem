using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(DualMonitorSpanController))]
public class DualMonitorSpanTest : MonoBehaviour
{
    [Header("테스트 UI 색상")]
    [FormerlySerializedAs("leftPanelColor")]
    [SerializeField] private Color _leftPanelColor = new Color(0.12f, 0.22f, 0.45f, 1f);
    [FormerlySerializedAs("rightPanelColor")]
    [SerializeField] private Color _rightPanelColor = new Color(0.2f, 0.42f, 0.18f, 1f);
    [FormerlySerializedAs("centerLineColor")]
    [SerializeField] private Color _centerLineColor = new Color(1f, 0.85f, 0.2f, 1f);

    // 테스트 씬이 열리면 좌우 화면 구분용 UI를 자동으로 구성합니다.
    private void Awake()
    {
        CreateTestLayout();
    }

    // 좌우 화면과 중앙 경계를 바로 확인할 수 있는 테스트용 UI를 생성합니다.
    private void CreateTestLayout()
    {
        if (FindObjectOfType<Canvas>() != null)
            return;

        Canvas canvas = CreateCanvas();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/NotoSansKR-Regular SDF");

        CreatePanel("LeftPanel", canvas.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), _leftPanelColor);
        CreatePanel("RightPanel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), _rightPanelColor);
        CreatePanel("CenterLine", canvas.transform, new Vector2(0.499f, 0f), new Vector2(0.501f, 1f), _centerLineColor);

        CreateText("LeftLabel", canvas.transform, new Vector2(0.25f, 0.5f), "LEFT DISPLAY\n1920 x 1080", font, 64f);
        CreateText("RightLabel", canvas.transform, new Vector2(0.75f, 0.5f), "RIGHT DISPLAY\n1920 x 1080", font, 64f);
        CreateText("TopGuide", canvas.transform, new Vector2(0.5f, 0.92f), "DUAL MONITOR SPAN TEST", font, 72f);
        CreateText("BottomGuide", canvas.transform, new Vector2(0.5f, 0.08f), "중앙 노란선이 모니터 경계와 맞으면 정상입니다.", font, 42f);
    }

    // 테스트용 캔버스를 생성합니다.
    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "DualMonitorTestCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(3840f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    // 화면 확인용 컬러 패널을 생성합니다.
    private void CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
    }

    // 중앙 정렬된 안내 텍스트를 생성합니다.
    private void CreateText(
        string objectName,
        Transform parent,
        Vector2 anchor,
        string message,
        TMP_FontAsset font,
        float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(1200f, 220f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        if (font != null)
            text.font = font;
    }
}
