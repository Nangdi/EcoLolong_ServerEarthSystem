using UnityEngine;

public class EndPanelController : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableController = true;

    [Header("패널 참조")]
    [Tooltip("S 누르고 15분 경과 시 활성, VIDEO_UPLOAD 수신 시 비활성")]
    [SerializeField] private GameObject _endPanel1;
    [Tooltip("R 누르고 리플레이 시간 종료 시 활성, E 키 입력 시 비활성")]
    [SerializeField] private GameObject _endPanel2;
    [Tooltip("VIDEO_UPLOAD 수신 시 활성화할 Scene2 Canvas")]
    [SerializeField] private GameObject _scene2Canvas;

    [Header("입력 키")]
    [SerializeField] private KeyCode _closePanel2Key = KeyCode.E;

    private TcpDataAggregator _aggregator;
    private GameTimer _gameTimer;
    private bool _isSubscribed;

    private void Start()
    {
        if (!_enableController)
            return;

        SetActive(_endPanel1, false);
        SetActive(_endPanel2, false);
        SetActive(_scene2Canvas, false);
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (!_enableController)
            return;

        if (Input.GetKeyDown(_closePanel2Key))
        {
            // GameManager의 E 키 통제 원칙과 동일하게, Ended 상태이면서 리플레이가 진행 중이지 않을 때만 패널을 닫습니다.
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.CurrentGameState != GameState.Ended || gameManager.IsReplay)
                return;

            SetActive(_endPanel2, false);
            SetActive(_scene2Canvas, false);
        }
    }

    private void SubscribeEvents()
    {
        if (_isSubscribed)
            return;

        _gameTimer = GameTimer.Instance;
        _aggregator = TcpDataAggregator.Instance;

        if (_gameTimer != null)
            _gameTimer.OnTimeOver += HandleTimeOver;

        if (_aggregator != null)
            _aggregator.VideoReadyReceived += HandleVideoReady;

        _isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_isSubscribed)
            return;

        if (_gameTimer != null)
            _gameTimer.OnTimeOver -= HandleTimeOver;

        if (_aggregator != null)
            _aggregator.VideoReadyReceived -= HandleVideoReady;

        _isSubscribed = false;
    }

    // 15분 종료(TimeOut) → 1_EndPanel ON, 리플레이 종료(Ended) → 2_EndPanel ON
    private void HandleTimeOver()
    {
        if (!_enableController)
            return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        switch (gameManager.CurrentGameState)
        {
            case GameState.TimeOut:
                SetActive(_endPanel1, true);
                break;
            case GameState.Ended:
                SetActive(_endPanel2, true);
                break;
        }
    }

    // VIDEO_UPLOAD|파일명 수신 시 호출됩니다. 파일명 자체는 사용하지 않고, 수신만으로 준비 완료로 간주합니다.
    private void HandleVideoReady(string fileName)
    {
        if (!_enableController)
            return;

        SetActive(_endPanel1, false);
        SetActive(_scene2Canvas, true);
    }

    // 강제 초기화(F5) 시 GameManager가 호출합니다. 상태 조건 없이 모든 패널을 닫습니다.
    public void ForceCloseAllPanels()
    {
        SetActive(_endPanel1, false);
        SetActive(_endPanel2, false);
        SetActive(_scene2Canvas, false);
    }

    private static void SetActive(GameObject panel, bool isActive)
    {
        if (panel != null && panel.activeSelf != isActive)
            panel.SetActive(isActive);
    }
}
