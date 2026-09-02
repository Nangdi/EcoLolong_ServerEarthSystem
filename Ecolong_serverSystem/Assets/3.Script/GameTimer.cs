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
    public float gameTime = 900f; // 15분 = 900초. 타이머의 목표 시간(타임아웃 기준)으로도 그대로 사용됩니다.
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


        if (CurrentTime >= gameTime)
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
                break;
            case GameState.Playing:
                break;
            case GameState.TimeOut:
                isRePlay = true;
                break;
            case GameState.Ended:
                isRePlay = true;
                break;
        }
    }
    public void StopTimer()
    {
        IsRunning = false;
    }

    // 강제 타임아웃 키(F6)에서 호출됩니다. CurrentTime을 gameTime으로 끌어올려
    // 다음 Update에서 자연 타임아웃과 완전히 동일한 경로(TimeOut 전환 → End 송신 → OnTimeOver)를 타게 합니다.
    public void ForceTimeOver()
    {
        if (!IsRunning)
            return;

        CurrentTime = gameTime;
    }

    // 리플레이가 끝난 결과 화면에서 남은 시간을 00:00으로 고정 표시하기 위해 사용합니다.
    // 타이머가 멈춘 상태(IsRunning=false)에서만 호출되므로 타임아웃 경로가 다시 타지는 않습니다.
    public void ShowTimeOverState()
    {
        CurrentTime = gameTime;
        OnTimeChanged?.Invoke(_currentTime);
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

    // 게임 총시간(타임아웃 기준)을 변경하고, 멈춰 있을 때도 남은 시간 텍스트가 즉시 갱신되도록 OnTimeChanged를 발행합니다.
    public void SetGameTime(float totalTime)
    {
        if (totalTime <= 0f)
            return;

        gameTime = totalTime;
        OnTimeChanged?.Invoke(_currentTime);
    }

    // 타이머의 정규화 진행도(0~1)를 반환합니다. 리플레이 동영상이 타이머에 맞춰 따라가도록 동기화하는 기준값입니다.
    public float GetNormalizedProgress()
    {
        if (gameTime <= 0f)
            return 0f;

        return Mathf.Clamp01(CurrentTime / gameTime);
    }

    public float GetCurrentTime()
    {
        return CurrentTime;
    }

    // gameTime을 기준으로 잔여 시간을 계산합니다.
    public float GetRemainingTime()
    {
        float remaining = gameTime - CurrentTime;
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
