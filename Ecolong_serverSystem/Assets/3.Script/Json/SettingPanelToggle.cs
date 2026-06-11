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

        _settingPanel.SetActive(!_settingPanel.activeSelf);
    }
}
