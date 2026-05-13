using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public enum GameState
{
    Ready,
    Playing,
    Ended
}
public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    // 다른 스크립트가 처음 접근하는 시점에 씬에서 한 번 찾아서 보완하는 lazy singleton getter입니다.
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<GameManager>();

            return instance;
        }
        private set
        {
            instance = value;
        }
    }
    public event Action OnGameStart; // 게임 시작 이벤트
    public event Action OnGameEnd; // 게임 종료 이벤트
    public event Action OnReplay; // 리플레이 시작 이벤트 (R 키)

    public float gameTimeScale = 1f; // 게임 시간의 흐름을 조절하는 변수
    public GameState CurrentGameState { get; private set; } = GameState.Ready; // 현재 게임 상태를 나타내는 변수

    public TcpDataAggregator tcpDataAggregator ; // TCP 데이터 집계기 참조

    [Header("Replay")]
    [SerializeField] private KeyCode replayKey = KeyCode.R;
    [SerializeField] private float replayTimerSpeed = 15f;
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

        if (Input.GetKeyDown(replayKey))
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
        GameTimer.Instance.isRePlay = true;
        GameTimer.Instance.StartTimer();
        GameTimer.Instance.SetTimerSpeed(replayTimerSpeed);
        OnReplay?.Invoke();
    }
    public void SetGameState(GameState newState)
    {
        CurrentGameState = newState;
        GameTimer.Instance.SettingTimer(); // 게임 상태 변경 시 GameTimer에도 전달
    }
}
