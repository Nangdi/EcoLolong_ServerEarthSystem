using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System;
public enum GameState
{
    Ready,
    Playing,
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

    public float gameTimeScale = 1f; // 게임 시간의 흐름을 조절하는 변수
    public GameState CurrentGameState { get; private set; } = GameState.Ready; // 현재 게임 상태를 나타내는 변수
    public bool IsReplay { get; private set; } = false; // 리플레이 진행 여부. GameTimer 등 외부에서 읽어 사용합니다.

    public TcpDataAggregator tcpDataAggregator ; // TCP 데이터 집계기 참조

    [Header("Replay")]
    [FormerlySerializedAs("replayKey")]
    [SerializeField] private KeyCode _replayKey = KeyCode.R;
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
        // 현재 싱글톤이 파괴될 때는 정적 참조도 같이 비워서 다음 탐색이 가능하게 합니다.
        if (Instance == this)
            Instance = null;
    }
    void Start()
    {
        Time.timeScale = gameTimeScale; // 게임 시작 시 시간 흐름을 설정
        GameTimer.Instance.OnTimeOver += GameManager_OnGameEnd;
    }
    private void GameManager_OnGameStart()
    {
        SetGameState(GameState.Playing);
        tcpDataAggregator.SendStringToAllClients("Start");
        OnGameStart?.Invoke(); // 게임 시작 이벤트 호출
    }
    //시간종료시 게임 종료 이벤트 호출
    private void GameManager_OnGameEnd()
    {
        OnGameEnd?.Invoke(); // 게임 종료 이벤트 호출
        if (CurrentGameState.Equals(GameState.Ready))
        {
            //최초 상태로 리셋
        }
        //SetGameState(GameState.Ended);
        tcpDataAggregator.SendStringToAllClients("End");
    }

    // Update is called once per frame
    void Update()
    {

        Time.timeScale = gameTimeScale; // 게임 시작 시 시간 흐름을 설정
        if(Input.GetKeyDown(KeyCode.S) )
        {
            Debug.Log("Game Started!");
            GameManager_OnGameStart(); // 게임 시작 이벤트 호출
        }

        if (Input.GetKeyDown(_replayKey))
            TriggerReplay();
        //if(Input.GetKeyDown(KeyCode.E) && CurrentGameState.Equals(GameState.Playing))
        //{
        //    Debug.Log("Game Ended!");
        //    GameManager_OnGameEnd(); // 게임 종료 이벤트 호출
        //}
    }

    // R 키 입력에서 호출됩니다. GameTimer를 리플레이 모드로 일괄 세팅한 뒤
    // OnReplay 이벤트로 모든 구독자(텍스트 recorder, 그래프 등)에게 알립니다.
    private void TriggerReplay()
    {
        if (GameTimer.Instance == null)
            return;

        gameTimeScale = 1f;
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
                break;
            case GameState.Ended:
                IsReplay = true;
                break;
        }
        GameTimer.Instance.SettingTimer(); // 게임 상태 변경 시 GameTimer에도 전달
    }
}
