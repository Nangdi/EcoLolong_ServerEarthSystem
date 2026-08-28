using UnityEngine;

public class EndPanelController : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableController = true;

    [Header("패널 참조")]
    [Tooltip("시작키를 누르고 게임 시간 경과 시 활성, VIDEO_UPLOAD 수신 시 비활성")]
    [SerializeField] private GameObject _endPanel1;
    [Tooltip("리플레이키를 누르고 리플레이 시간 종료 시 활성, 종료키 입력 시 비활성")]
    [SerializeField] private GameObject _endPanel2;
    [Tooltip("VIDEO_UPLOAD 수신 시 활성화할 Scene2 Canvas")]
    [SerializeField] private GameObject _scene2Canvas;

    // 패널을 닫는 키는 GameManager와 동일하게 GameKeyBindings.EndKey(기본 E)를 사용합니다.
    // 값은 ESC 설정창의 "키 설정" 버튼에서 변경합니다.

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

        // ESC 설정창에서 키를 재지정하는 중에는 입력을 무시합니다.
        if (GameKeyBindings.IsRebinding)
            return;

        if (Input.GetKeyDown(GameKeyBindings.EndKey))
        {
            // GameManager의 종료키 통제 원칙과 동일하게, Ended 상태이면서 리플레이가 진행 중이지 않을 때만 패널을 닫습니다.
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
        ShowScene2();
    }

    // 업로드 대기 화면(엔드패널1)을 닫고 Scene2 캔버스를 띄웁니다.
    // VIDEO_UPLOAD 수신 시 자동으로 호출되며, 강제 영상준비 키(V)에서 GameManager가 직접 호출하기도 합니다.
    public void ShowScene2()
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
