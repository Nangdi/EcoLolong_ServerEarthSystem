using System.Collections;
using UnityEngine;

public class EndPanelController : MonoBehaviour
{
    [Header("동작 여부")]
    [SerializeField] private bool _enableController = true;

    [Header("패널 참조")]
    [Tooltip("시작키를 누르고 게임 시간 경과 시 활성, VIDEO_UPLOAD 수신 시 비활성")]
    [SerializeField] private GameObject _endPanel1;
    [Tooltip("리플레이 종료 후에는 결과 화면을 가리지 않도록 더 이상 자동으로 켜지 않습니다. 종료키/강제 초기화 시 닫는 용도로만 참조합니다.")]
    [SerializeField] private GameObject _endPanel2;
    [Tooltip("VIDEO_UPLOAD 수신 시 활성화할 Scene2 Canvas")]
    [SerializeField] private GameObject _scene2Canvas;

    [Header("Scene2 전환 대기")]
    [Tooltip("영상 업로드(VIDEO_UPLOAD) 수신 후 Scene2 캔버스를 띄우기까지 기다릴 시간(초). 실제 값은 ESC 설정창 관리자 설정의 \"Scene2 전환 대기(초)\"에서 조절하며, 여기 값은 JsonManager가 없을 때만 쓰이는 예비값입니다.")]
    [SerializeField] private float _fallbackScene2TransitionDelay = 3f;

    [Header("리플레이 종료 처리")]
    [Tooltip("리플레이가 끝나면 Scene2 캔버스를 바로 닫아, 종료 직전 상태가 복원된 Scene1을 종료키 입력 때까지 보여줍니다.")]
    [SerializeField] private bool _closeScene2OnReplayEnd = true;

    // 패널을 닫는 키는 GameManager와 동일하게 GameKeyBindings.EndKey(기본 E)를 사용합니다.
    // 값은 ESC 설정창의 "키 설정" 버튼에서 변경합니다.

    private TcpDataAggregator _aggregator;
    private GameTimer _gameTimer;
    private bool _isSubscribed;

    // 업로드 수신 후 Scene2 전환을 기다리는 코루틴입니다. 대기 도중 종료/초기화되면 취소합니다.
    private Coroutine _scene2DelayCoroutine;

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

            CancelScene2Delay();
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
                // 리플레이가 끝났으므로 Scene2(리플레이 영상) 캔버스를 닫아
                // GameManager가 복원해 둔 "종료 직전 상태"의 Scene1이 다시 보이게 합니다.
                // 2_EndPanel은 결과 화면을 가리므로 띄우지 않고, 그대로 종료키 입력을 기다립니다.
                if (_closeScene2OnReplayEnd)
                {
                    CancelScene2Delay();
                    SetActive(_scene2Canvas, false);
                }
                break;
        }
    }

    // VIDEO_UPLOAD|파일명 수신 시 호출됩니다. 파일명 자체는 사용하지 않고, 수신만으로 준비 완료로 간주합니다.
    private void HandleVideoReady(string fileName)
    {
        NotifyVideoReady();
    }

    // 영상 업로드 완료 신호를 받았을 때의 화면 전환입니다.
    // 업로드가 끝나자마자 화면이 바뀌면 너무 급하게 느껴지므로, 설정된 대기 시간만큼 업로드 대기 화면을 더 보여준 뒤 전환합니다.
    // VIDEO_UPLOAD 수신과 강제 영상준비 키(V)가 모두 이 경로를 탑니다.
    public void NotifyVideoReady()
    {
        if (!_enableController)
            return;

        float delay = GetScene2TransitionDelay();
        if (delay <= 0f)
        {
            ShowScene2();
            return;
        }

        CancelScene2Delay();
        _scene2DelayCoroutine = StartCoroutine(ShowScene2AfterDelay(delay));
    }

    // ESC 설정창 관리자 설정의 "Scene2 전환 대기(초)" 값을 읽어옵니다. JsonManager가 없으면 인스펙터 예비값을 씁니다.
    private float GetScene2TransitionDelay()
    {
        float delay = JsonManager.instance != null && JsonManager.instance.gameSettingData != null
            ? JsonManager.instance.gameSettingData.scene2TransitionDelay
            : _fallbackScene2TransitionDelay;

        return Mathf.Max(0f, delay);
    }

    private IEnumerator ShowScene2AfterDelay(float delay)
    {
        Debug.Log($"[EndPanel] 영상 업로드 수신 / {delay:0.##}초 후 Scene2로 전환합니다.");

        // 리플레이 구간에서 Time.timeScale이 바뀌어도 대기 시간이 흔들리지 않도록 실제 시간으로 기다립니다.
        yield return new WaitForSecondsRealtime(delay);

        _scene2DelayCoroutine = null;
        ShowScene2();
    }

    // 대기 중이던 Scene2 전환을 취소합니다. (종료키 / 강제 초기화 / 리플레이 종료 시)
    private void CancelScene2Delay()
    {
        if (_scene2DelayCoroutine == null)
            return;

        StopCoroutine(_scene2DelayCoroutine);
        _scene2DelayCoroutine = null;
    }

    // 업로드 대기 화면(엔드패널1)을 닫고 Scene2 캔버스를 띄웁니다.
    // 업로드 신호를 받고 대기 시간이 지나면 호출됩니다. 대기 없이 곧바로 전환해야 할 때만 외부에서 직접 호출하세요.
    public void ShowScene2()
    {
        if (!_enableController)
            return;

        CancelScene2Delay();
        SetActive(_endPanel1, false);
        SetActive(_scene2Canvas, true);
    }

    // 강제 초기화(F5) 시 GameManager가 호출합니다. 상태 조건 없이 모든 패널을 닫습니다.
    public void ForceCloseAllPanels()
    {
        CancelScene2Delay();
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
