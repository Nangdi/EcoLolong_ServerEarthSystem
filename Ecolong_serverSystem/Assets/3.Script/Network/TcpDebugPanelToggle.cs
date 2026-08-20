using UnityEngine;

public class TcpDebugPanelToggle : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableToggle = true;

    [Header("토글 대상")]
    [SerializeField] private GameObject _tcpLogPanel;

    [Header("토글 키")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Escape;

    // 키 겹침 안내(ESC 설정창)에서 읽어갑니다.
    public KeyCode ToggleKey => _toggleKey;

    private void Update()
    {
        if (!_enableToggle)
            return;

        // 키 재지정 중의 ESC는 "취소"로만 쓰이므로(KeyRebindController) 패널을 토글하지 않습니다.
        if (GameKeyBindings.IsRebinding)
            return;

        if (Input.GetKeyDown(_toggleKey))
            Toggle();
    }

    // 외부에서도 호출할 수 있도록 토글 로직을 public 메서드로 노출합니다.
    public void Toggle()
    {
        if (_tcpLogPanel == null)
            return;

        _tcpLogPanel.SetActive(!_tcpLogPanel.activeSelf);
    }
}
