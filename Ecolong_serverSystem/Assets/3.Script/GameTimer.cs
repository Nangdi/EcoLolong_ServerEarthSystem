using System;
using UnityEngine;
using UnityEngine.Serialization;

public class GameTimer : MonoBehaviour
{
    private static GameTimer s_instance;

    // 다른 스크립트가 처음 접근하는 시점에 씬에서 한 번 찾아서 보완하는 lazy singleton getter입니다.
    public static GameTimer Instance
    {
        get
        {
            if (s_instance == null)
                s_instance = FindObjectOfType<GameTimer>();

            return s_instance;
        }
        private set
        {
            s_instance = value;
        }
    }
    public bool IsRunning { get; private set; }

    [FormerlySerializedAs("currentTime")]
    [SerializeField] private float _currentTime;

    [Header("Time Range")]
    public float gameTime = 900f; // 15분 = 900초
    public float targetTime = 900;
    public float settingGameScale = 1f; // 게임 시간의 흐름을 조절하는 변수
    public bool isRePlay = false;
    public float CurrentTime
    {
        get { return _currentTime; }
        private set { _currentTime = value; }
    }

    public event Action<float> OnTimeChanged;
    public event Action OnTimeOver;
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

        // 프로젝트의 Time.timeScale(TimeManager.asset)이 1이 아니어도 게임 시간은 settingGameScale 한 가지만 따르도록
        // unscaledDeltaTime을 사용합니다. 그렇지 않으면 m_TimeScale 값이 그대로 곱해져 의도와 다르게 빨라집니다.
        CurrentTime += Time.unscaledDeltaTime * settingGameScale;
        OnTimeChanged?.Invoke(_currentTime);


        if (CurrentTime >= targetTime)
        {
            if (!isRePlay)
            {

                _currentTime = 0;
                GameManager.Instance.SetGameState(GameState.TimeOut);
                Debug.Log("Time Over!");
                OnTimeOver?.Invoke();
            }
            else
            {
                GameManager.Instance.SetGameState(GameState.Ended);
                OnTimeOver?.Invoke();
                //Debug.Log("Replay End! . Game End.");
                // ?.Invoke();
            }
            //게임매니저에 게임종료 알림

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
        switch (GameManager.Instance.CurrentGameState)
        {
            case GameState.Ready:
                isRePlay = false;
                targetTime = gameTime;
                break;
            case GameState.Playing:
                break;
            case GameState.TimeOut:
                isRePlay = true;
                targetTime = gameTime;
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
        OnTimeChanged?.Invoke(_currentTime);
    }

    public void SetTimerSpeed(float scale)
    {
        settingGameScale = scale;
    }

    // 타이머의 정규화 진행도(0~1)를 반환합니다. 리플레이 동영상이 타이머에 맞춰 따라가도록 동기화하는 기준값입니다.
    public float GetNormalizedProgress()
    {
        float baseTime = targetTime > 0f ? targetTime : gameTime;
        if (baseTime <= 0f)
            return 0f;

        return Mathf.Clamp01(CurrentTime / baseTime);
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
