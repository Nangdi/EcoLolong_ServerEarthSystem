using TMPro;
using UnityEngine;

// =============================================================================
//  한글 폰트를 한 곳에서 공급하는 헬퍼입니다.
//
//  - TMP(UGUI 텍스트): Assets/Resources/Fonts/NotoSansKR-Regular SDF 를 사용합니다.
//    (Assets/10.Font/defaultFont 의 NotoSansKR과 같은 폰트이며, 런타임/빌드에서
//     불러올 수 있도록 Resources 폴더에 들어 있는 SDF 에셋입니다.)
//    폰트를 지정하지 않은 TMP 텍스트는 TMP 기본값(LiberationSans SDF)을 쓰는데
//    여기에는 한글 글리프가 없어 네모(□)로 깨집니다. 그래서 ESC 설정창처럼
//    한글을 표시하는 UI에는 이 폰트를 직접 지정합니다.
//
//  - IMGUI(OnGUI): 기본 IMGUI 폰트에도 한글이 없어 깨지므로, Resources에 TTF가 있으면
//    그것을, 없으면 시스템에 설치된 한글 폰트(맑은 고딕 등)를 동적으로 만들어 씁니다.
// =============================================================================
public static class KoreanFontProvider
{
    // Resources 폴더 기준 경로입니다. (확장자 없이, 폴더/파일명 그대로)
    public const string TmpFontResourcePath = "Fonts/NotoSansKR-Regular SDF";
    public const string TtfFontResourcePath = "Fonts/NotoSansKR-Regular";

    // IMGUI용 폰트를 만들 때 순서대로 시도할 시스템 한글 폰트 이름입니다.
    private static readonly string[] _osFontNames =
    {
        "Malgun Gothic",
        "맑은 고딕",
        "NanumGothic",
        "나눔고딕",
        "Gulim",
        "Dotum",
        "Batang",
        "Arial Unicode MS",
    };

    private static TMP_FontAsset _tmpFont;
    private static bool _isTmpFontResolved;

    private static Font _guiFont;
    private static bool _isGuiFontResolved;

    // TMP 텍스트에 지정할 한글 폰트 에셋입니다. 찾지 못하면 null을 반환합니다.
    public static TMP_FontAsset TmpFont
    {
        get
        {
            if (_isTmpFontResolved)
                return _tmpFont;

            _isTmpFontResolved = true;
            _tmpFont = Resources.Load<TMP_FontAsset>(TmpFontResourcePath);

            if (_tmpFont == null)
                Debug.LogWarning($"[KoreanFontProvider] TMP 한글 폰트를 찾지 못했습니다: Resources/{TmpFontResourcePath}");

            return _tmpFont;
        }
    }

    // OnGUI(IMGUI)용 한글 폰트입니다. 찾지 못하면 null을 반환합니다.
    public static Font GuiFont
    {
        get
        {
            if (_isGuiFontResolved)
                return _guiFont;

            _isGuiFontResolved = true;

            // 1) Resources에 TTF가 있으면 그대로 사용합니다.
            _guiFont = Resources.Load<Font>(TtfFontResourcePath);
            if (_guiFont != null)
                return _guiFont;

            // 2) 없으면 시스템에 설치된 한글 폰트로 동적 폰트를 만듭니다. (윈도우는 맑은 고딕이 기본 탑재)
            _guiFont = Font.CreateDynamicFontFromOSFont(_osFontNames, 16);

            if (_guiFont == null)
                Debug.LogWarning("[KoreanFontProvider] IMGUI용 한글 폰트를 찾지 못했습니다. 기본 폰트를 사용합니다.");

            return _guiFont;
        }
    }

    // IMGUI 스타일에 한글 폰트를 적용합니다. 폰트를 못 찾으면 아무것도 하지 않습니다.
    public static void ApplyTo(GUIStyle style)
    {
        if (style == null)
            return;

        Font font = GuiFont;
        if (font != null)
            style.font = font;
    }

    // TMP 텍스트 하나에 한글 폰트를 적용합니다.
    public static void ApplyTo(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_FontAsset font = TmpFont;
        if (font == null || text.font == font)
            return;

        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    // 지정한 오브젝트 아래(자기 자신 포함)의 모든 TMP 텍스트에 한글 폰트를 적용합니다.
    // 비활성 오브젝트(설정창의 템플릿 등)도 함께 처리합니다.
    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null)
            return;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
            ApplyTo(texts[i]);
    }
}
