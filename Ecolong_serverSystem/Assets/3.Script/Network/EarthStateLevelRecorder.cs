using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;
    [FormerlySerializedAs("timer")]
    [SerializeField] private GameTimer _timer;

    [Header("리플레이 표시 텍스트")]
    [FormerlySerializedAs("sustainabilityLevelText")]
    [SerializeField] private TMP_Text _sustainabilityLevelText;
    [FormerlySerializedAs("developmentLevelText")]
    [SerializeField] private TMP_Text _developmentLevelText;
    [FormerlySerializedAs("ecoLevelText")]
    [SerializeField] private TMP_Text _ecoLevelText;
    [SerializeField] private TMP_Text _currentCarbonText;
    [SerializeField] private TMP_Text _currentPowerGenerationText;
    [FormerlySerializedAs("levelFormat")]
    [SerializeField] private string _levelFormat = "LEVEL {0}";
    [SerializeField] private string _tokenFormat = "{0}개";

    [Header("디버그")]
    [FormerlySerializedAs("samples")]
    [SerializeField] private List<LevelSample> _samples = new List<LevelSample>();
    private List<LevelSample> _recordSamples = new List<LevelSample>();

    private bool _isSubscribedToEarthState;
    private bool _isSubscribedToGame;
    private int _lastReplayedIndex = -1;

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

        if (_timer != null && GameManager.Instance.IsReplay && _timer.IsRunning)
            ApplyLevelsForReplayTime(_timer.CurrentTime);
    }

    // 게임 시작마다 이전 기록을 비웁니다.
    private void OnGameStart()
    {
        _samples.Clear();
        _recordSamples.Clear();
        _lastReplayedIndex = -1;
    }

    // 게임 종료 시점에 이번 회차 기록을 리플레이용으로 백업합니다.
    private void OnGameEnd()
    {
        _recordSamples = new List<LevelSample>(_samples);
    }

    // EarthStateSnapshot의 5개 값(지속가능성/발전도/친환경도/현재탄소/현재발전토큰) 중
    // 하나라도 바뀐 경우에만 (time, sustain, dev, eco, currentCarbon, currentPower) 한 줄을 추가합니다.
    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        if (snapshot == null || _timer == null)
            return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameState.Playing)
            return;

        int sustainability = snapshot.EcoLevel + snapshot.DevelopmentLevel;

        if (_samples.Count > 0)
        {
            LevelSample last = _samples[_samples.Count - 1];
            if (last.SustainabilityLevel == sustainability &&
                last.DevelopmentLevel == snapshot.DevelopmentLevel &&
                last.EcoLevel == snapshot.EcoLevel &&
                last.CurrentCarbon == snapshot.CurrentCarbon &&
                last.CurrentPowerGeneration == snapshot.CurrentPowerGeneration)
                return;
        }

        _samples.Add(new LevelSample
        {
            Time = _timer.CurrentTime,
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
        _lastReplayedIndex = -1;
    }

    // currentTime 이전의 마지막 샘플을 골라 세 텍스트에 한 번에 적용합니다.
    private void ApplyLevelsForReplayTime(float currentTime)
    {
        if (_recordSamples.Count == 0)
            return;

        int idx = -1;
        for (int i = 0; i < _recordSamples.Count; i++)
        {
            if (_recordSamples[i].Time <= currentTime)
                idx = i;
            else
                break;
        }

        if (idx < 0 || idx == _lastReplayedIndex)
            return;

        LevelSample sample = _recordSamples[idx];
        SetLevelText(_sustainabilityLevelText, sample.SustainabilityLevel);
        SetLevelText(_developmentLevelText, sample.DevelopmentLevel);
        SetLevelText(_ecoLevelText, sample.EcoLevel);
        SetTokenText(_currentCarbonText, sample.CurrentCarbon);
        SetTokenText(_currentPowerGenerationText, sample.CurrentPowerGeneration);
        _lastReplayedIndex = idx;
    }

    private void SetLevelText(TMP_Text target, int level)
    {
        if (target == null)
            return;
        target.text = string.Format(_levelFormat, level);
    }

    private void SetTokenText(TMP_Text target, int value)
    {
        if (target == null)
            return;
        target.text = string.Format(_tokenFormat, value);
    }

    private void TrySubscribe()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
        if (_timer == null)
            _timer = GameTimer.Instance;

        if (!_isSubscribedToEarthState && _earthStateManager != null)
        {
            _earthStateManager.StateChanged += OnEarthStateChanged;
            _isSubscribedToEarthState = true;
        }

        if (!_isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart += OnGameStart;
            GameManager.Instance.OnGameEnd += OnGameEnd;
            GameManager.Instance.OnReplay += OnReplay;
            _isSubscribedToGame = true;
        }
    }

    private void Unsubscribe()
    {
        if (_isSubscribedToEarthState && _earthStateManager != null)
            _earthStateManager.StateChanged -= OnEarthStateChanged;
        if (_isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart -= OnGameStart;
            GameManager.Instance.OnGameEnd -= OnGameEnd;
            GameManager.Instance.OnReplay -= OnReplay;
        }
        _isSubscribedToEarthState = false;
        _isSubscribedToGame = false;
    }
}
