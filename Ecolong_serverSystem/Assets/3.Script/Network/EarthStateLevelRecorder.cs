using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// EarthStateManager의 친환경도/발전도/지속가능성(친환경도+발전도) 단계가 바뀔 때마다
// 게임 시간과 함께 기록하고, R 키 리플레이 진행 시간에 맞춰 TMP_Text 값을 다시 적용합니다.
public class EarthStateLevelRecorder : MonoBehaviour
{
    [Serializable]
    public struct LevelSample
    {
        public float Time;
        public int SustainabilityLevel;
        public int DevelopmentLevel;
        public int EcoLevel;
        public int CurrentCarbon;
        public int CurrentPowerGeneration;
    }

    [Header("연결")]
    [SerializeField] private EarthStateManager earthStateManager;
    [SerializeField] private GameTimer timer;

    [Header("리플레이 표시 텍스트")]
    [SerializeField] private TMP_Text sustainabilityLevelText;
    [SerializeField] private TMP_Text developmentLevelText;
    [SerializeField] private TMP_Text ecoLevelText;
    [SerializeField] private string levelFormat = "LEVEL {0}";

    [Header("디버그")]
    [SerializeField] private List<LevelSample> samples = new List<LevelSample>();
    private List<LevelSample> recordSamples = new List<LevelSample>();

    private bool isSubscribedToEarthState;
    private bool isSubscribedToGame;
    private int lastReplayedIndex = -1;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        TrySubscribe();

        if (timer != null && timer.isRePlay && timer.IsRunning)
            ApplyLevelsForReplayTime(timer.CurrentTime);
    }

    // 게임 시작마다 이전 기록을 비웁니다.
    private void OnGameStart()
    {
        samples.Clear();
        recordSamples.Clear();
        lastReplayedIndex = -1;
    }

    // 게임 종료 시점에 이번 회차 기록을 리플레이용으로 백업합니다.
    private void OnGameEnd()
    {
        recordSamples = new List<LevelSample>(samples);
    }

    // EarthStateSnapshot의 5개 값(지속가능성/발전도/친환경도/현재탄소/현재발전토큰) 중
    // 하나라도 바뀐 경우에만 (time, sustain, dev, eco, currentCarbon, currentPower) 한 줄을 추가합니다.
    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        if (snapshot == null || timer == null)
            return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameState.Playing)
            return;

        int sustainability = snapshot.EcoLevel + snapshot.DevelopmentLevel;

        if (samples.Count > 0)
        {
            LevelSample last = samples[samples.Count - 1];
            if (last.SustainabilityLevel == sustainability &&
                last.DevelopmentLevel == snapshot.DevelopmentLevel &&
                last.EcoLevel == snapshot.EcoLevel &&
                last.CurrentCarbon == snapshot.CurrentCarbon &&
                last.CurrentPowerGeneration == snapshot.CurrentPowerGeneration)
                return;
        }

        samples.Add(new LevelSample
        {
            Time = timer.CurrentTime,
            SustainabilityLevel = sustainability,
            DevelopmentLevel = snapshot.DevelopmentLevel,
            EcoLevel = snapshot.EcoLevel,
            CurrentCarbon = snapshot.CurrentCarbon,
            CurrentPowerGeneration = snapshot.CurrentPowerGeneration
        });
    }

    // GameManager.OnReplay 신호를 받으면 다음 Update부터 리플레이 시간 기반으로 텍스트를 다시 그립니다.
    private void OnReplay()
    {
        lastReplayedIndex = -1;
    }

    // currentTime 이전의 마지막 샘플을 골라 세 텍스트에 한 번에 적용합니다.
    private void ApplyLevelsForReplayTime(float currentTime)
    {
        if (recordSamples.Count == 0)
            return;

        int idx = -1;
        for (int i = 0; i < recordSamples.Count; i++)
        {
            if (recordSamples[i].Time <= currentTime)
                idx = i;
            else
                break;
        }

        if (idx < 0 || idx == lastReplayedIndex)
            return;

        LevelSample sample = recordSamples[idx];
        SetLevelText(sustainabilityLevelText, sample.SustainabilityLevel);
        SetLevelText(developmentLevelText, sample.DevelopmentLevel);
        SetLevelText(ecoLevelText, sample.EcoLevel);
        lastReplayedIndex = idx;
    }

    private void SetLevelText(TMP_Text target, int level)
    {
        if (target == null)
            return;
        target.text = string.Format(levelFormat, level);
    }

    private void TrySubscribe()
    {
        if (earthStateManager == null)
            earthStateManager = EarthStateManager.Instance;
        if (timer == null)
            timer = GameTimer.Instance;

        if (!isSubscribedToEarthState && earthStateManager != null)
        {
            earthStateManager.StateChanged += OnEarthStateChanged;
            isSubscribedToEarthState = true;
        }

        if (!isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart += OnGameStart;
            GameManager.Instance.OnGameEnd += OnGameEnd;
            GameManager.Instance.OnReplay += OnReplay;
            isSubscribedToGame = true;
        }
    }

    private void Unsubscribe()
    {
        if (isSubscribedToEarthState && earthStateManager != null)
            earthStateManager.StateChanged -= OnEarthStateChanged;
        if (isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart -= OnGameStart;
            GameManager.Instance.OnGameEnd -= OnGameEnd;
            GameManager.Instance.OnReplay -= OnReplay;
        }
        isSubscribedToEarthState = false;
        isSubscribedToGame = false;
    }
}
