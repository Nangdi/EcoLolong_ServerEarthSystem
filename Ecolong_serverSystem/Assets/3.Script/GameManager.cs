using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System;
public enum GameState
{
    Ready,
    Playing,
    TimeOut,
    Ended
}
public class GameManager : MonoBehaviour
{
    private static GameManager s_instance;

    // 다른 스크립트가 처음 접근하는 시점에 씬에서 한 번 찾아서 보완하는 lazy singleton getter입니다.
    public static GameManager Instance
    {
        get
        {
            if (s_instance == null)
                s_instance = FindObjectOfType<GameManager>();

            return s_instance;
        }
        private set
        {
            s_instance = value;
        }
    }
    public event Action OnGameStart; // 게임 시작 이벤트
    public event Action OnGameEnd; // 게임 종료 이벤트
    public event Action OnReplay; // 리플레이 시작 이벤트 (R 키)

    [Tooltip("플레이 중 게임 시간이 흐르는 속도. 1=실시간, 60=1초가 1분.")]
    [Min(0.01f)]
    public float gameTimeScale = 1f; // 게임 시간의 흐름을 조절하는 변수 (GameTimer/그래프 갱신의 단일 진실원)
    public GameState CurrentGameState = GameState.Ready; // 현재 게임 상태를 나타내는 변수
    public bool IsReplay { get; private set; } = false; // 리플레이 진행 여부. GameTimer 등 외부에서 읽어 사용합니다.

    public TcpDataAggregator tcpDataAggregator; // TCP 데이터 집계기 참조

    // TimeOut 상태에서 R 키로 리플레이를 시작하려면 클라이언트가 VIDEO_UPLOAD를 한 번 이상 보내야 합니다.
    private bool _isVideoReady;
    private bool _isVideoReadySubscribed;

    [Header("Replay")]
    [FormerlySerializedAs("replayKey")]
    [SerializeField] private KeyCode _replayKey = KeyCode.R;

    [Header("강제 상태이동 키")]
    [Tooltip("현재 상태와 무관하게 클라이언트에 Ready를 송신하고 게임을 초기 상태로 되돌립니다.")]
    [SerializeField] private KeyCode _forceReadyKey = KeyCode.F5;
    [Tooltip("Playing 상태에서만 동작. 15분 경과와 동일한 경로로 즉시 TimeOut(End 송신 + 업로드 대기 패널)으로 이동합니다.")]
    [SerializeField] private KeyCode _forceTimeOutKey = KeyCode.F6;
    [FormerlySerializedAs("replayTimerSpeed")]
    [SerializeField] private float _replayTimerSpeed = 15f;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    private void OnDestroy()
    {
        // 비디오 업로드 이벤트 구독을 정리합니다.
        if (_isVideoReadySubscribed && tcpDataAggregator != null)
        {
            tcpDataAggregator.VideoReadyReceived -= OnVideoReady;
            _isVideoReadySubscribed = false;
        }

        // 현재 싱글톤이 파괴될 때는 정적 참조도 같이 비워서 다음 탐색이 가능하게 합니다.
        if (Instance == this)
            Instance = null;
    }
    void Start()
    {
        // 게임 시간 속도는 gameTimeScale 하나만으로 통제하기 위해 Unity의 전역 시간배율은 항상 1로 정규화합니다.
        // (TimeManager.asset의 m_TimeScale이 1이 아니면 GameTimer/그래프가 의도와 다르게 빨라집니다.)
        Time.timeScale = 1f;
        // gameSettingData.json에 저장된 사용자 설정 속도/총시간을 우선 적용합니다. JsonManager가 아직 없으면 인스펙터 값을 그대로 사용합니다.
        ApplyGameTimeScaleFromSettings();
        ApplyGameTotalTimeFromSettings();
        // 시작 시점에는 Ready 상태이므로 GameTimer에도 동일 스케일을 푸시해 둡니다.
        if (GameTimer.Instance != null)
            GameTimer.Instance.SetTimerSpeed(gameTimeScale);
        GameTimer.Instance.OnTimeOver += GameManager_OnGameEnd;
        TrySubscribeVideoReady();
    }

    // 사용자가 게임세팅 패널에서 수정한 값을 즉시 GameTimer에 반영합니다.
    // 리플레이 중에는 _replayTimerSpeed가 우선이므로 GameTimer 쪽 푸시는 건너뜁니다.
    public void SetGameTimeScale(float scale)
    {
        if (scale <= 0f)
            scale = 0.01f;

        gameTimeScale = scale;

        if (IsReplay)
            return;

        if (GameTimer.Instance != null)
            GameTimer.Instance.SetTimerSpeed(gameTimeScale);
    }

    // 사용자가 게임세팅 패널에서 수정한 총시간(초)을 즉시 GameTimer.gameTime에 반영합니다.
    public void SetGameTotalTime(float totalTime)
    {
        if (totalTime <= 0f)
            return;

        if (GameTimer.Instance != null)
            GameTimer.Instance.SetGameTime(totalTime);
    }

    private void ApplyGameTimeScaleFromSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        if (jsonManager == null || jsonManager.gameSettingData == null)
            return;

        float saved = jsonManager.gameSettingData.gameTimeScale;
        if (saved > 0f)
            gameTimeScale = saved;
    }

    // gameSettingData.json에 저장된 게임 총시간(초)을 GameTimer.gameTime에 반영합니다.
    private void ApplyGameTotalTimeFromSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        if (jsonManager == null || jsonManager.gameSettingData == null)
            return;

        SetGameTotalTime(jsonManager.gameSettingData.gameTotalTime);
    }

    // TcpDataAggregator가 늦게 초기화되는 케이스를 위해 Start와 Update에서 반복적으로 구독을 시도합니다.
    private void TrySubscribeVideoReady()
    {
        if (_isVideoReadySubscribed)
            return;

        if (tcpDataAggregator == null)
            tcpDataAggregator = TcpDataAggregator.Instance;

        if (tcpDataAggregator == null)
            return;

        tcpDataAggregator.VideoReadyReceived += OnVideoReady;
        _isVideoReadySubscribed = true;
    }

    // VIDEO_UPLOAD를 한 번이라도 수신하면 TimeOut 상태에서 R 키 입력이 허용됩니다.
    private void OnVideoReady(string fileName)
    {
        _isVideoReady = true;
    }

    private void GameManager_OnGameStart()
    {
        _isVideoReady = false; // 새 게임 사이클이 시작되므로 비디오 업로드 수신 대기로 초기화
        tcpDataAggregator.SendStringToAllClients("Start");

        // 게임 시작 직후, 클라이언트가 진행도/리소스 속도를 맞출 수 있도록 게임 길이(초)를 별도 라인으로 송신합니다.
        // "GAMETIME:900" 형식으로 보냅니다.
        if (GameTimer.Instance != null)
            tcpDataAggregator.SendStringToAllClients($"GAMETIME:{Mathf.RoundToInt(GameTimer.Instance.gameTime)}");

        OnGameStart?.Invoke(); // 게임 시작 이벤트 호출
    }
    // 게임 타이머의 자동 종료(Playing→TimeOut, 리플레이 종료→Ended) 시점에 호출됩니다.
    // 키 입력 처리에서는 직접 호출하지 않습니다.
    private void GameManager_OnGameEnd()
    {
        OnGameEnd?.Invoke(); // 게임 종료 이벤트 호출

        if (CurrentGameState.Equals(GameState.TimeOut))
        {
            // 정상 플레이 시간이 종료된 시점입니다. 클라이언트에 종료를 알리고 리플레이 직전 UI를 초기화합니다.
            tcpDataAggregator.SendStringToAllClients("End");
            Debug.Log("Time Out!");
            // 그래프 _points는 보존되어 리플레이에서 그대로 사용됩니다.
            ResetGameDataForReplay();
        }
        else if (CurrentGameState.Equals(GameState.Ended))
        {
            // 리플레이가 끝난 시점입니다. "Ready" 송신과 전체 초기화는 E 키 처리에서 담당합니다.
            Debug.Log("Replay Ended! (E 키 대기)");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Time.timeScale = gameTimeScale; // 게임 시작 시 시간 흐름을 설정
        TrySubscribeVideoReady();

        // S: Ready(첫 시작) 또는 Playing(재시작) 상태에서만 받습니다.
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (CurrentGameState == GameState.Ready || CurrentGameState == GameState.Playing)
            {
                Debug.Log("Game Started!");
                SetGameState(GameState.Playing);
                GameManager_OnGameStart(); // OnGameStart 이벤트가 GameTimer/EarthState/그래프를 일괄 재초기화합니다.
            }
        }

        // R: TimeOut(비디오 업로드 완료 후) 상태에서만 리플레이를 시작합니다.
        // 리플레이가 끝난 Ended 상태에서는 R을 눌러도 다시 재생되지 않습니다. (E 키로 Ready 복귀)
        if (Input.GetKeyDown(_replayKey))
        {
            if (CurrentGameState == GameState.TimeOut && _isVideoReady)
            {
                TriggerReplay();
            }
        }

        // E: Ended 상태이면서 리플레이가 진행 중이지 않을 때만 처음 상태(Ready)로 복귀합니다.
        // (리플레이 도중 R 재입력 등으로 TimeOut으로 되돌아간 경우에도 E가 작동하지 않도록 IsReplay 가드를 함께 둡니다.)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (CurrentGameState == GameState.Ended && !IsReplay)
            {
                SetGameState(GameState.Ready);
                Debug.Log("Game Reset to Ready!");
                GameDataReset(); // 모든 UI 데이터 텍스트와 그래프 초기화
                tcpDataAggregator.SendStringToAllClients("Ready");
            }
        }

        // F5(강제 초기화): 현재 상태와 무관하게 클라이언트에 Ready를 송신하고 게임을 초기 상태로 되돌립니다.
        if (Input.GetKeyDown(_forceReadyKey))
        {
            ForceResetToReady();
        }

        // F6(강제 타임아웃): Playing 상태에서만, 타이머를 즉시 종료 지점으로 보내
        // 자연 타임아웃과 동일한 경로(End 송신 + 엔드패널1 표시 + VIDEO_UPLOAD 대기)를 타게 합니다.
        if (Input.GetKeyDown(_forceTimeOutKey))
        {
            if (CurrentGameState == GameState.Playing && GameTimer.Instance != null)
            {
                Debug.Log($"[ForceKey] 강제 타임아웃 ({_forceTimeOutKey})");
                GameTimer.Instance.ForceTimeOver();
            }
        }
    }

    // 강제 초기화 키(F5)에서 호출됩니다. E 키의 Ready 복귀 동작을 상태 조건 없이 수행하되,
    // 어떤 상태에서 눌려도 안전하도록 비디오 정지와 엔드패널 정리까지 함께 처리합니다.
    private void ForceResetToReady()
    {
        Debug.Log($"[ForceKey] 강제 초기화 → Ready ({_forceReadyKey})");

        // 1) 클라이언트 PC들에게 Ready 신호를 먼저 송신합니다.
        if (tcpDataAggregator != null)
            tcpDataAggregator.SendStringToAllClients("Ready");

        // 2) 재생 중인 리플레이 비디오와 준비 코루틴을 중단합니다.
        VideoPlaybackController[] videoControllers = FindObjectsOfType<VideoPlaybackController>();
        for (int i = 0; i < videoControllers.Length; i++)
            videoControllers[i].StopPlayback();

        // 3) 타이머를 멈춘 뒤 Ready로 전환합니다. (SetGameState가 IsReplay 해제와 타이머 속도 복원을 담당)
        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        SetGameState(GameState.Ready);
        _isVideoReady = false;

        // 4) 모든 UI 텍스트/타이머/그래프를 초기화합니다.
        GameDataReset();

        // 5) 엔드패널1/2와 Scene2 캔버스를 상태 조건 없이 모두 닫습니다.
        EndPanelController[] endPanels = FindObjectsOfType<EndPanelController>();
        for (int i = 0; i < endPanels.Length; i++)
            endPanels[i].ForceCloseAllPanels();
    }

    // 게임 타이머가 종료된 직후, 리플레이가 시작되기 전 단계에서 호출합니다.
    // DefaultTextGroup(남은 시간/친환경도/발전도/지속가능성/상태명/현재 탄소/현재 발전토큰 등)을
    // 처음 상태로 되돌리되, 리플레이가 사용하는 그래프 _points는 보존합니다.
    public void ResetGameDataForReplay()
    {
        // GameTimer → GameTimerTextBinder가 OnTimeChanged로 15:00:00(남은 시간) 표시
        if (GameTimer.Instance != null)
            GameTimer.Instance.ResetTimer();

        // TcpDataAggregator → TotalsChanged로 TcpDataTextBinder의 발전/탄소/도시 텍스트 0으로 복귀
        if (tcpDataAggregator != null)
            tcpDataAggregator.ClearTotals();

        // EarthStateManager → StateChanged로 친환경도/발전도/상태명/탄소ppm/온도/얼음/해수면 텍스트 초기값으로 복귀
        if (EarthStateManager.Instance != null)
            EarthStateManager.Instance.ResetState();

        // GameEventLogUI → 누적된 이벤트 로그 라인을 모두 비웁니다.
        // TimeOut(게임 종료) 시점에도 호출되므로, 리플레이는 깨끗한 초기화 상태에서 대기하게 됩니다.
        GameEventLogUI[] eventLogs = FindObjectsOfType<GameEventLogUI>();
        for (int i = 0; i < eventLogs.Length; i++)
            eventLogs[i].Clear();
    }

    // 모든 UI 데이터 텍스트와 그래프까지 처음 상태로 되돌리는 단일 진입점입니다.
    // 각 데이터 소스를 리셋하면 구독 중인 텍스트 바인더가 이벤트로 자동 갱신됩니다.
    public void GameDataReset()
    {
        // 텍스트/타이머/누적값 초기화는 리플레이 직전과 동일한 흐름을 재사용합니다.
        ResetGameDataForReplay();

        // 추가로 씬에 존재하는 모든 그래프를 초기화합니다. (LineRenderer + Fill 메시 모두 비움)
        ResourceGraphs[] graphs = FindObjectsOfType<ResourceGraphs>();
        for (int i = 0; i < graphs.Length; i++)
            graphs[i].HardClearGraph();
    }

    // R 키 입력에서 호출됩니다. GameTimer를 리플레이 모드로 일괄 세팅한 뒤
    // OnReplay 이벤트로 모든 구독자(텍스트 recorder, 그래프 등)에게 알립니다.
    private void TriggerReplay()
    {
        if (GameTimer.Instance == null)
            return;

        // gameTimeScale은 사용자 설정값이므로 덮어쓰지 않고, 리플레이 동안만 _replayTimerSpeed로 일시 전환합니다.
        Time.timeScale = 1f;
        IsReplay = true;
        GameTimer.Instance.StartTimer();
        GameTimer.Instance.SetTimerSpeed(_replayTimerSpeed);
        OnReplay?.Invoke();
    }
    public void SetGameState(GameState newState)
    {
        CurrentGameState = newState;
        // 상태 전환에 맞춰 리플레이 여부도 함께 갱신합니다. Ready는 일반 플레이, Ended는 다음 입력 시 리플레이로 진입합니다.
        switch (newState)
        {
            case GameState.Ready:
                IsReplay = false;
                // Ready로 돌아갈 때도 사용자 설정 속도를 유지합니다.
                GameTimer.Instance.SetTimerSpeed(gameTimeScale);
                break;
            case GameState.TimeOut:
                IsReplay = true;
                break;
            case GameState.Ended:
                IsReplay = false;
                break;
        }
        GameTimer.Instance.SettingTimer(); // 게임 상태 변경 시 GameTimer에도 전달
    }
}
