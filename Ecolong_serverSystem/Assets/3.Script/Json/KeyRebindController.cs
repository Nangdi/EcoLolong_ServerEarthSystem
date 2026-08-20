using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =============================================================================
//  ESC 설정창에서 시작 / 리플레이 / 종료 키를 관리자가 직접 지정하는 기능입니다.
//
//  [사용 방법]
//   1) ESC로 설정창을 연 뒤 "키 설정 (S / R / E)" 버튼을 누릅니다.
//   2) "시작키로 지정할 버튼을 눌러주세요."      → 원하는 키를 누릅니다.
//   3) "리플레이 키로 지정할 버튼을 눌러주세요." → 원하는 키를 누릅니다.
//   4) "종료키로 지정할 버튼을 눌러주세요."      → 원하는 키를 누릅니다.
//   5) 세 키가 GameKeyBindings에 적용되고 gameSettingData.json에 즉시 저장됩니다.
//
//  - 진행 중 ESC를 누르면 취소되고 이전 키 설정이 그대로 유지됩니다.
//  - 이미 앞 단계에서 고른 키는 중복으로 지정할 수 없습니다.
//  - 안내 문구는 _promptText(TMP)가 있으면 거기에, 없으면 화면 중앙 오버레이로 표시됩니다.
//  - 버튼(_rebindButton)을 비워두면 GameSettingsPanelUI가 저장 버튼을 복제해 자동 생성합니다.
// =============================================================================
public class KeyRebindController : MonoBehaviour
{
    [Header("UI (비워두면 자동 생성/자동 표시)")]
    [Tooltip("키 설정을 시작하는 버튼. 비워두면 저장 버튼을 복제해 자동으로 만듭니다.")]
    [SerializeField] private Button _rebindButton;
    [Tooltip("안내 문구를 표시할 텍스트. 비워두면 화면 중앙 오버레이(OnGUI)로 표시합니다.")]
    [SerializeField] private TextMeshProUGUI _promptText;

    [Header("표시 설정")]
    [Tooltip("오버레이 안내 문구의 크기 배율입니다.")]
    [Range(0.5f, 4f)]
    [SerializeField] private float _uiScale = 1.8f;
    [Tooltip("설정 완료 후 결과 문구를 몇 초간 보여줄지입니다.")]
    [SerializeField] private float _resultMessageSeconds = 3f;

    // 단계별 안내 문구입니다. 순서대로 시작 → 리플레이 → 종료 키를 받습니다.
    private static readonly string[] _stepPrompts =
    {
        "시작키로 지정할 버튼을 눌러주세요.",
        "리플레이 키로 지정할 버튼을 눌러주세요.",
        "종료키로 지정할 버튼을 눌러주세요.",
    };

    // 매 프레임 Enum.GetValues를 호출하지 않도록 한 번만 만들어 재사용합니다.
    private static readonly KeyCode[] _allKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    private readonly KeyCode[] _capturedKeys = new KeyCode[3];

    private bool _isCapturing;
    private int _stepIndex;
    private int _startFrame = -1;      // 버튼 클릭과 같은 프레임의 입력은 무시하기 위한 기준 프레임
    private string _message;           // 오버레이에 표시할 문구 (안내 / 경고 / 결과)
    private float _messageHideTime;    // 결과 문구 자동 숨김 시각 (0이면 계속 표시)
    private GUIStyle _messageStyle;
    private float _lastStyleScale = -1f;

    private void OnEnable()
    {
        GameKeyBindings.Changed += UpdateButtonLabel;
        UpdateButtonLabel();
    }

    private void OnDisable()
    {
        GameKeyBindings.Changed -= UpdateButtonLabel;

        // 설정창이 닫히면 Update가 돌지 않으므로, 캡처 상태로 남아 게임 키가 막히지 않도록 정리합니다.
        if (_isCapturing)
        {
            CancelCapture(false);
            return;
        }

        // 남아 있던 결과 문구도 함께 지워 다음에 설정창을 열었을 때 깨끗하게 보이도록 합니다.
        _messageHideTime = 0f;
        SetMessage(string.Empty);
    }

    private void OnDestroy()
    {
        if (_rebindButton != null)
            _rebindButton.onClick.RemoveListener(BeginRebind);
    }

    // GameSettingsPanelUI가 호출합니다.
    // 버튼/안내문구가 비어 있으면 설정창의 저장 버튼과 텍스트 템플릿을 복제해 만들어 둡니다.
    public void EnsureUI(Transform uiParent, Button buttonTemplate, TextMeshProUGUI textTemplate)
    {
        if (_rebindButton == null)
            _rebindButton = CreateButton(uiParent, buttonTemplate);

        if (_rebindButton != null)
        {
            _rebindButton.onClick.RemoveListener(BeginRebind);
            _rebindButton.onClick.AddListener(BeginRebind);
        }

        if (_promptText == null)
            _promptText = CreatePromptText(uiParent, textTemplate);

        // 버튼 라벨과 안내 문구가 한글이므로 한글 폰트를 지정합니다. (기본 TMP 폰트에는 한글이 없습니다)
        if (_rebindButton != null)
            KoreanFontProvider.ApplyToHierarchy(_rebindButton.transform);

        KoreanFontProvider.ApplyTo(_promptText);

        UpdateButtonLabel();
    }

    // 안내 문구용 TMP 텍스트를 설정창 텍스트 템플릿에서 복제합니다.
    // 템플릿이 없으면 null을 반환하고, 이 경우 안내는 OnGUI 오버레이로 표시됩니다.
    private TextMeshProUGUI CreatePromptText(Transform uiParent, TextMeshProUGUI textTemplate)
    {
        if (textTemplate == null || uiParent == null)
            return null;

        TextMeshProUGUI created = Instantiate(textTemplate, uiParent);
        created.name = "KeyRebindPromptText";
        created.color = new Color(1f, 0.85f, 0.3f);
        created.text = string.Empty;

        if (_rebindButton != null)
            created.transform.SetSiblingIndex(_rebindButton.transform.GetSiblingIndex() + 1);

        created.gameObject.SetActive(false);
        return created;
    }

    // 저장 버튼을 복제해 "키 설정" 버튼을 만듭니다. 템플릿이 없으면 버튼 없이 동작합니다.
    private Button CreateButton(Transform buttonParent, Button buttonTemplate)
    {
        if (buttonTemplate == null || buttonParent == null)
            return null;

        Button created = Instantiate(buttonTemplate, buttonParent);
        created.name = "KeyRebindButton";
        created.onClick.RemoveAllListeners();

        // 템플릿 버튼(저장 버튼)에 인스펙터로 연결된 이벤트가 있다면 복제본에서는 꺼 둡니다.
        for (int i = 0; i < created.onClick.GetPersistentEventCount(); i++)
            created.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

        created.transform.SetSiblingIndex(buttonTemplate.transform.GetSiblingIndex() + 1);
        created.gameObject.SetActive(true);
        return created;
    }

    // 버튼 라벨에 현재 지정된 키를 함께 보여줍니다.
    private void UpdateButtonLabel()
    {
        if (_rebindButton == null)
            return;

        string label = $"키 설정 ({GameKeyBindings.Describe()})";

        TextMeshProUGUI tmpLabel = _rebindButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = label;
            return;
        }

        Text uguiLabel = _rebindButton.GetComponentInChildren<Text>(true);
        if (uguiLabel != null)
            uguiLabel.text = label;
    }

    // 버튼 클릭 진입점입니다. 첫 단계(시작키) 안내부터 시작합니다.
    public void BeginRebind()
    {
        _isCapturing = true;
        _stepIndex = 0;
        _startFrame = Time.frameCount;
        _messageHideTime = 0f;

        for (int i = 0; i < _capturedKeys.Length; i++)
            _capturedKeys[i] = KeyCode.None;

        // 입력필드에 포커스가 남아 있으면 눌린 키가 글자로 입력되므로 선택을 해제합니다.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // 캡처 중에는 게임 단축키(시작/리플레이/종료, 디버그 키 등)가 반응하지 않도록 잠급니다.
        GameKeyBindings.SetRebinding(true);

        ShowStepPrompt();
    }

    private void Update()
    {
        if (!_isCapturing)
        {
            // 결과 문구는 지정한 시간이 지나면 자동으로 지웁니다.
            if (_messageHideTime > 0f && Time.unscaledTime >= _messageHideTime)
            {
                _messageHideTime = 0f;
                SetMessage(string.Empty);
            }

            return;
        }

        // 버튼을 클릭한 바로 그 프레임의 입력은 무시합니다. (클릭과 동시에 눌린 키가 잡히는 것 방지)
        if (Time.frameCount == _startFrame)
            return;

        // ESC: 지금까지의 선택을 버리고 취소합니다. 기존 키 설정은 그대로 유지됩니다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCapture(true);
            return;
        }

        KeyCode pressed = ReadPressedKey();
        if (pressed == KeyCode.None)
            return;

        // 같은 키를 두 기능에 겹쳐 지정할 수 없습니다. 현재 단계를 유지하고 다시 입력받습니다.
        if (IsAlreadyCaptured(pressed))
        {
            SetMessage($"{_stepPrompts[_stepIndex]}\n\n[{pressed}] 키는 이미 지정했습니다. 다른 키를 눌러주세요.");
            return;
        }

        _capturedKeys[_stepIndex] = pressed;
        _stepIndex++;

        if (_stepIndex < _stepPrompts.Length)
        {
            ShowStepPrompt();
            return;
        }

        CompleteCapture();
    }

    // 이번 프레임에 새로 눌린 키를 하나 찾아 돌려줍니다. 마우스 버튼은 UI 클릭과 겹치므로 제외합니다.
    private static KeyCode ReadPressedKey()
    {
        for (int i = 0; i < _allKeyCodes.Length; i++)
        {
            KeyCode key = _allKeyCodes[i];

            if (key == KeyCode.None || IsMouseButton(key))
                continue;

            if (Input.GetKeyDown(key))
                return key;
        }

        return KeyCode.None;
    }

    private static bool IsMouseButton(KeyCode key)
    {
        return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;
    }

    private bool IsAlreadyCaptured(KeyCode key)
    {
        for (int i = 0; i < _stepIndex; i++)
        {
            if (_capturedKeys[i] == key)
                return true;
        }

        return false;
    }

    private void ShowStepPrompt()
    {
        string progress = $"({_stepIndex + 1}/{_stepPrompts.Length})";
        SetMessage($"{_stepPrompts[_stepIndex]}\n\n{progress}   ESC: 취소");
    }

    // 세 키를 모두 받은 뒤 실제 적용 + JSON 저장까지 처리합니다.
    private void CompleteCapture()
    {
        _isCapturing = false;
        GameKeyBindings.SetRebinding(false);

        GameKeyBindings.Apply(_capturedKeys[0], _capturedKeys[1], _capturedKeys[2], true);

        SetMessage($"키 설정을 저장했습니다.\n\n시작: {_capturedKeys[0]}   리플레이: {_capturedKeys[1]}   종료: {_capturedKeys[2]}");
        _messageHideTime = Time.unscaledTime + Mathf.Max(0.5f, _resultMessageSeconds);
    }

    // ESC 취소 또는 설정창이 닫힐 때 호출됩니다. 캡처 상태만 정리하고 키 설정은 건드리지 않습니다.
    private void CancelCapture(bool showMessage)
    {
        _isCapturing = false;
        GameKeyBindings.SetRebinding(false);

        if (showMessage)
        {
            SetMessage($"키 설정을 취소했습니다. (현재: {GameKeyBindings.Describe()})");
            _messageHideTime = Time.unscaledTime + Mathf.Max(0.5f, _resultMessageSeconds);
        }
        else
        {
            _messageHideTime = 0f;
            SetMessage(string.Empty);
        }
    }

    // 안내 문구를 TMP 텍스트(있으면)와 오버레이용 버퍼에 함께 반영합니다.
    private void SetMessage(string message)
    {
        _message = message;

        if (_promptText == null)
            return;

        _promptText.text = message;
        _promptText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    // _promptText가 지정되지 않은 경우를 위한 화면 중앙 오버레이입니다. (KeyBindingsCheatSheet와 동일 방식)
    private void OnGUI()
    {
        if (_promptText != null || string.IsNullOrEmpty(_message))
            return;

        EnsureStyles();

        float scale = Mathf.Max(0.5f, _uiScale);
        float width = 460f * scale;
        float height = 150f * scale;
        float x = (Screen.width - width) * 0.5f;
        float y = (Screen.height - height) * 0.5f;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = prev;

        GUI.Label(new Rect(x + 16f * scale, y + 16f * scale, width - 32f * scale, height - 32f * scale), _message, _messageStyle);
    }

    // OnGUI 호출 시점에 한 번만 스타일을 만듭니다. 배율이 바뀌면 다시 만듭니다.
    private void EnsureStyles()
    {
        float scale = Mathf.Max(0.5f, _uiScale);

        if (_messageStyle != null && Mathf.Approximately(_lastStyleScale, scale))
            return;

        _lastStyleScale = scale;
        _messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(17 * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = Color.white },
        };

        // 기본 IMGUI 폰트에는 한글 글리프가 없어 네모로 깨지므로 한글 폰트로 교체합니다.
        KoreanFontProvider.ApplyTo(_messageStyle);
    }
}
