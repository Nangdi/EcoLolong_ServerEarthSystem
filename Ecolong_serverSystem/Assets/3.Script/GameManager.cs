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
    public static GameManager Instance { get; private set; }
    public event Action OnGameStart; // 게임 시작 이벤트
    public event Action OnGameEnd; // 게임 시작 이벤트

    public float gameTimeScale = 1f; // 게임 시간의 흐름을 조절하는 변수
    public GameState CurrentGameState { get; private set; } = GameState.Ready; // 현재 게임 상태를 나타내는 변수
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    void Start()
    {
        Time.timeScale = gameTimeScale; // 게임 시작 시 시간 흐름을 설정
        GameTimer.Instance.OnTimeOver += GameManager_OnGameEnd;
    }
    //시간종료시 게임 종료 이벤트 호출
    private void GameManager_OnGameStart()
    {
        SetGameState(GameState.Playing);
        OnGameStart?.Invoke(); // 게임 시작 이벤트 호출
    }
    private void GameManager_OnGameEnd()
    {
        OnGameEnd?.Invoke(); // 게임 종료 이벤트 호출
        if (CurrentGameState.Equals(GameState.Ready))
        {
            //최초 상태로 리셋
        }
        //SetGameState(GameState.Ended);
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
        //if(Input.GetKeyDown(KeyCode.E) && CurrentGameState.Equals(GameState.Playing))
        //{
        //    Debug.Log("Game Ended!");
        //    GameManager_OnGameEnd(); // 게임 종료 이벤트 호출
        //}
    }
    public void SetGameState(GameState newState)
    {
        CurrentGameState = newState;
        GameTimer.Instance.SettingTimer(); // 게임 상태 변경 시 GameTimer에도 전달
    }
}
