using System;
using UnityEngine;

public class GameTimer : MonoBehaviour
{
    public static GameTimer Instance { get; private set; }
    public bool IsRunning { get; private set; }
    [SerializeField] private float currentTime;

    [Header("Time Range")]
    public float gameTime = 900f; // 15분 = 900초
    public float rePlayTime = 60f; // 1분 = 60초
    public float targetTime = 0;
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
    private void Start()
    {
        GameManager.Instance.OnGameStart += Timer_OnGameStart;
        GameManager.Instance.OnGameEnd += Timer_OnGameEnd;
    }
    private void Update()
    {
        if (!IsRunning) return;

        CurrentTime += Time.deltaTime;
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
                break;
            case GameState.Playing:
                isRePlay = false;
                targetTime = gameTime;

                break;
            case GameState.Ended:
                isRePlay = true;
                targetTime = rePlayTime;
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


    public float GetCurrentTime()
    {
        return CurrentTime;
    }
    private void Timer_OnGameStart()
    {
        StartTimer();
    }
    private void Timer_OnGameEnd()
    {
        ResetTimer();
        StopTimer();
    }
    
}