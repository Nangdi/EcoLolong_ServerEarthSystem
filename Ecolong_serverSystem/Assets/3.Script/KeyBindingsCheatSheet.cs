using UnityEngine;

// =============================================================================
//  프로젝트 단축키 한눈에 보기 (KeyBindingsCheatSheet)
//
//  이 스크립트 하나로 프로젝트 전체에 흩어진 키 입력을 정리해 둡니다.
//  - 아래 주석 표 = 코드만 봐도 단축키를 파악하는 용도
//  - OnGUI 오버레이 = 게임 실행 중 F1 로 화면에서 바로 확인 (기본 토글 키)
//
//  ┌──────────┬───────────────────────────────┬──────────────────────────────┐
//  │   키     │            동작               │   처리 스크립트 / 조건        │
//  ├──────────┼───────────────────────────────┼──────────────────────────────┤
//  │  S(기본) │ 게임 시작 / 재시작            │ GameManager                  │
//  │          │                               │  (Ready 또는 Playing 상태)   │
//  │  R(기본) │ 리플레이 시작                 │ GameManager / EndPanel       │
//  │          │                               │  (TimeOut+영상준비 또는      │
//  │          │                               │   Ended 상태)                │
//  │  E(기본) │ 처음 상태(Ready)로 복귀 /     │ GameManager / EndPanel       │
//  │          │ 엔드패널2 닫기                │  (Ended & 리플레이중 아님)   │
//  ├──────────┼───────────────────────────────┼──────────────────────────────┤
//  │  F5      │ [강제] Ready 송신 후          │ GameManager                  │
//  │          │ 게임 초기상태로 초기화        │  (모든 상태에서 동작)        │
//  │  F6      │ [강제] 즉시 타임아웃          │ GameManager / GameTimer      │
//  │          │ (End 송신+업로드 대기 패널)   │  (Playing 상태에서만)        │
//  ├──────────┼───────────────────────────────┼──────────────────────────────┤
//  │  ESC     │ 설정 패널 토글                │ SettingPanelToggle           │
//  │  ESC     │ TCP 로그 패널 토글            │ TcpDebugPanelToggle          │
//  │          │  (두 패널이 동시에 토글됨)    │                              │
//  ├──────────┼───────────────────────────────┼──────────────────────────────┤
//  │  1       │ [디버그] 화력 누적 +N         │ TcpDataAggregator            │
//  │  2       │ [디버그] 수력 누적 +N         │  (_enableKeyboardTest 켜짐   │
//  │  3       │ [디버그] 태양광 누적 +N       │   상태에서만 동작)           │
//  │  0       │ [디버그] 누적 데이터 초기화   │                              │
//  │  T       │ [디버그] 현재 메시지 전체     │                              │
//  │          │         클라이언트로 전송     │                              │
//  │  V       │ [디버그] VIDEO_UPLOAD 수신    │                              │
//  │          │         시뮬레이션            │                              │
//  ├──────────┼───────────────────────────────┼──────────────────────────────┤
//  │  F1      │ 이 단축키 도움말 표시/숨김    │ KeyBindingsCheatSheet        │
//  └──────────┴───────────────────────────────┴──────────────────────────────┘
//
//  ※ 1/2/3/0 키와 누적량 N(=_testAddCount)·각 테스트 키는
//    TcpDataAggregator 인스펙터에서 변경할 수 있습니다.
//  ※ S/R/E(시작·리플레이·종료) 키는 ESC 설정창의 "키 설정" 버튼에서
//    직접 눌러 지정할 수 있으며 gameSettingData.json에 저장됩니다.
//  ※ 강제 키(F5/F6)는 GameManager 인스펙터(_forceReadyKey,
//    _forceTimeOutKey)에서 변경할 수 있습니다.
// =============================================================================
public class KeyBindingsCheatSheet : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableOverlay = true;

    [Header("토글 키")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

    // 키 겹침 안내(ESC 설정창)에서 읽어갑니다.
    public KeyCode ToggleKey => _toggleKey;

    [Header("시작 시 표시 여부")]
    [SerializeField] private bool _showOnStart = false;

    [Header("패널 크기 배율")]
    [Tooltip("도움말 패널 전체(글자·여백 포함) 크기 배율입니다. 값이 클수록 패널이 커집니다.")]
    [Range(0.5f, 4f)]
    [SerializeField] private float _uiScale = 1.8f;

    // 화면에 그릴 단축키 항목(키, 설명) 목록입니다. 위 주석 표와 같은 내용입니다.
    // 시작/리플레이/종료 키는 ESC 설정창에서 바뀔 수 있어 GameKeyBindings 값으로 매번 다시 만듭니다.
    private (string key, string desc)[] _entries;

    private void BuildEntries()
    {
        _entries = new (string key, string desc)[]
        {
            (GameKeyBindings.StartKey.ToString(), "게임 시작 / 재시작 (Ready·Playing)"),
            (GameKeyBindings.ReplayKey.ToString(), "리플레이 시작 (TimeOut+영상준비·Ended)"),
            (GameKeyBindings.EndKey.ToString(), "처음 상태로 복귀 / 엔드패널2 닫기 (Ended)"),
            ("F5", "[강제] Ready 송신 + 게임 초기상태 복귀 (모든 상태)"),
            ("F6", "[강제] 즉시 타임아웃 → 업로드 대기 패널 (Playing)"),
            ("ESC", "설정 패널 · TCP 로그 패널 토글"),
            ("1 / 2 / 3", "[디버그] 화력 / 수력 / 태양광 누적 +N"),
            ("0", "[디버그] 누적 데이터 초기화"),
            ("T", "[디버그] 현재 메시지 전체 클라이언트 전송"),
            ("V", "[디버그] VIDEO_UPLOAD 수신 시뮬레이션"),
            ("F1", "이 도움말 표시 / 숨김"),
        };
    }

    private bool _visible;
    private GUIStyle _titleStyle;
    private GUIStyle _keyStyle;
    private GUIStyle _descStyle;
    private float _lastStyleScale = -1f;

    private void Awake()
    {
        _visible = _showOnStart;
        BuildEntries();
    }

    private void OnEnable()
    {
        // 설정창에서 시작/리플레이/종료 키가 바뀌면 표도 같이 갱신합니다.
        GameKeyBindings.Changed += BuildEntries;
    }

    private void OnDisable()
    {
        GameKeyBindings.Changed -= BuildEntries;
    }

    private void Update()
    {
        if (!_enableOverlay)
            return;

        // GetSecondaryKeyDown: 시작/리플레이/종료 키와 겹치면 도움말 토글을 무시합니다. (설정 키 우선)
        if (GameKeyBindings.GetSecondaryKeyDown(_toggleKey))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_enableOverlay || !_visible)
            return;

        if (_entries == null)
            BuildEntries();

        EnsureStyles();

        float scale = Mathf.Max(0.5f, _uiScale);
        float width = 520f * scale;
        float lineHeight = 26f * scale;
        float height = (56f + _entries.Length * 26f + 16f) * scale;
        float x = 16f;
        float y = 16f;

        // 반투명 배경 박스
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = prev;

        float cx = x + 16f * scale;
        float cy = y + 12f * scale;

        GUI.Label(new Rect(cx, cy, width - 32f * scale, 30f * scale), "단축키 도움말 (F1: 닫기)", _titleStyle);
        cy += 40f * scale;

        foreach ((string key, string desc) in _entries)
        {
            GUI.Label(new Rect(cx, cy, 110f * scale, lineHeight), key, _keyStyle);
            GUI.Label(new Rect(cx + 120f * scale, cy, width - 152f * scale, lineHeight), desc, _descStyle);
            cy += lineHeight;
        }
    }

    // OnGUI 호출 시점에 한 번만 스타일을 만듭니다.
    private void EnsureStyles()
    {
        float scale = Mathf.Max(0.5f, _uiScale);

        // 배율이 그대로면 기존 스타일을 재사용하고, 바뀌었을 때만 폰트 크기를 다시 만듭니다.
        if (_titleStyle != null && Mathf.Approximately(_lastStyleScale, scale))
            return;

        _lastStyleScale = scale;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(18 * scale),
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        _keyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(15 * scale),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.3f) },
        };

        _descStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(15 * scale),
            normal = { textColor = Color.white },
        };

        // 기본 IMGUI 폰트에는 한글 글리프가 없어 네모로 깨지므로 시스템 한글 폰트로 교체합니다.
        KoreanFontProvider.ApplyTo(_titleStyle);
        KoreanFontProvider.ApplyTo(_keyStyle);
        KoreanFontProvider.ApplyTo(_descStyle);
    }
}
