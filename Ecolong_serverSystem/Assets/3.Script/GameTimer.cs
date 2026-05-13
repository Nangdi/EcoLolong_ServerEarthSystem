using System;
using UnityEngine;

public class GameTimer : MonoBehaviour
{
    private static GameTimer instance;

    // 다른 스크립트가 처음 접근하는 시점에 씬에서 한 번 찾아서 보완하는 lazy singleton getter입니다.
    public static GameTimer Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<GameTimer>();

            return instance;
        }
        private set
        {
            instance = value;
        }
    }
    public bool IsRunning { get; private set; }
    [SerializeField] private float currentTime;

    [Header("Time Range")]
    public float gameTime = 900f; // 15분 = 900초
    public float targetTime = 900;
    public float settingGameScale = 1f; // 게임 시간의 흐름을 조절하는 변수
    public bool isRePlay = false;
    public float CurrentTime
    {
        get { return currentTime; }
        private set { currentTime = value; }
    }

    public event Action<float> OnTimeChanged;
    public event Action OnTimeOver;
    public event Action OnReplayEnd;
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
    private void Start()
    {
        ResetTimer();
        GameManager.Instance.OnGameStart += Timer_OnGameStart;
        GameManager.Instance.OnGameEnd += Timer_OnGameEnd;
    }
    private void Update()
    {
        if (!IsRunning) return;

        CurrentTime += Time.deltaTime * settingGameScale;
        OnTimeChanged?.Invoke(currentTime);


        if (CurrentTime >= targetTime)
        {
            if (!isRePlay)
            {

                GameManager.Instance.SetGameState(GameState.Ended);

            }
            else
            {
                GameManager.Instance.SetGameState(GameState.Ready);
                //Debug.Log("Replay End! . Game End.");
                //OnReplayEnd?.Invoke();
            }
            //게임매니저에 게임종료 알림
            Debug.Log("Time Over!");
            currentTime = 0;
            OnTimeOver?.Invoke();
        }
    }
    public void StartTimer()
    {
        SettingTimer();
        CurrentTime = 0f;
        IsRunning = true;
    }
    public void SettingTimer()
    {
        switch ((GameManager.Instance.CurrentGameState)
)
        {
            case GameState.Ready:
                isRePlay = false;
                targetTime = gameTime;

                break;
            case GameState.Playing:

                break;
            case GameState.Ended:
                isRePlay = true;
                targetTime = gameTime;
                break;
        }
    }
    public void StopTimer()
    {
        IsRunning = false;
    }

    public void ResetTimer()
    {
        CurrentTime = 0f;
        OnTimeChanged?.Invoke(currentTime);
    }
    public void SetGameTime(float time)
    {
        gameTime = time;
    }
    public void SetTimerSpeed(float scale)
    {
        settingGameScale = scale;
    }

    public float GetCurrentTime()
    {
        return CurrentTime;
    }

    // 타이머 시작 전이거나 targetTime이 0이면 gameTime을 기준으로 잔여 시간을 계산합니다.
    public float GetRemainingTime()
    {
        float baseTime = targetTime > 0f ? targetTime : gameTime;
        float remaining = baseTime - CurrentTime;
        return remaining > 0f ? remaining : 0f;
    }
    private void Timer_OnGameStart()
    {
        ResetTimer();
        StartTimer();
    }
    private void Timer_OnGameEnd()
    {
        StopTimer();
    }

}