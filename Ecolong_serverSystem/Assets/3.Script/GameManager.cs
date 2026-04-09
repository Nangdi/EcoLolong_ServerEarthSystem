using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public event Action OnGameStart; // 게임 시작 이벤트
    public event Action OnGameEnd; // 게임 시작 이벤트

    public float gameTimeScale = 1f; // 게임 시간의 흐름을 조절하는 변수
    // Start is called before the first frame update
    void Start()
    {
        OnGameEnd += GameManager_OnGameEnd;
        Time.timeScale = gameTimeScale; // 게임 시작 시 시간 흐름을 설정
    }

    private void GameManager_OnGameEnd()
    {
        throw new NotImplementedException();
    }

    // Update is called once per frame
    void Update()
    {
        Time.timeScale = gameTimeScale; // 게임 시작 시 시간 흐름을 설정
    }
}
