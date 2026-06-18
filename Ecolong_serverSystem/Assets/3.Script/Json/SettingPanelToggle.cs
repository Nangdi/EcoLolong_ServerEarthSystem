using UnityEngine;

// TcpDebugPanelToggle과 동일 패턴으로 게임 세팅 패널을 키 한 번에 열고 닫습니다.
// 기본 키는 ESC이며 TcpDebugPanelToggle과 동시에 ESC를 사용하면 두 패널이 함께 토글됩니다.
public class SettingPanelToggle : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableToggle = true;

    [Header("토글 대상")]
    [SerializeField] private GameObject _settingPanel;

    [Header("토글 키")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Escape;

    private void Update()
    {
        if (!_enableToggle)
            return;

        if (Input.GetKeyDown(_toggleKey))
            Toggle();
    }

    // 외부에서도 호출할 수 있도록 토글 로직을 public 메서드로 노출합니다.
    public void Toggle()
    {
        if (_settingPanel == null)
            return;

        bool nowActive = !_settingPanel.activeSelf;
        _settingPanel.SetActive(nowActive);
        UpdateCursorVisibility(nowActive);
    }

    // 설정창이 열리면 마우스를 보이게 하고, 닫히면 게임 기본 상태로 되돌립니다.
    // 기본 상태는 UnityAlwaysOnTop과 동일하게 "빌드 + useUnityOnTop"일 때만 숨김입니다.
    private void UpdateCursorVisibility(bool panelActive)
    {
        Cursor.visible = panelActive || ShouldShowCursorByDefault();
    }

    private static bool ShouldShowCursorByDefault()
    {
        if (Application.isEditor)
            return true;

        JsonManager json = JsonManager.instance;
        bool hideCursor = json != null && json.gameSettingData != null && json.gameSettingData.useUnityOnTop;
        return !hideCursor;
    }
}
