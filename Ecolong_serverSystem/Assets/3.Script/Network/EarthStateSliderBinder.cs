using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// EarthStateSnapshot의 레벨 값을 3개 슬라이더에 정규화 비율(0~1)로 동기화합니다.
// - Sustainability = EcoLevel + DevelopmentLevel (2~10)
// - Development    = DevelopmentLevel             (1~5)
// - Eco            = EcoLevel                     (1~5)
//
// [기록/리플레이] 리플레이 중에는 EarthStateManager가 멈춰 StateChanged가 오지 않으므로,
// 플레이 중 (시간, eco, dev) 변화를 기록해 두었다가 R 키 리플레이 시 GameTimer.CurrentTime에 맞춰
// 같은 순서/타이밍으로 슬라이더 목표값을 다시 적용합니다.
public class EarthStateSliderBinder : MonoBehaviour
{
    // 레벨 변화를 (게임시간, 친환경도, 발전도)로 기록하는 한 줄입니다.
    [Serializable]
    public struct LevelSample
    {
        public float Time;
        public int Eco;
        public int Dev;
    }

    [Header("상태 연결")]
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;
    [Tooltip("리플레이 진행 시간의 기준이 되는 타이머. 비어 있으면 GameTimer.Instance를 자동으로 사용합니다.")]
    [SerializeField] private GameTimer _timer;

    [Header("슬라이더")]
    [Tooltip("지속가능성 레벨용 슬라이더 (EcoLevel + DevelopmentLevel, 최대 10)")]
    [FormerlySerializedAs("sustainabilitySlider")]
    [SerializeField] private Slider _sustainabilitySlider;
    [Tooltip("발전도 레벨용 슬라이더 (DevelopmentLevel, 최대 5)")]
    [FormerlySerializedAs("developmentSlider")]
    [SerializeField] private Slider _developmentSlider;
    [Tooltip("친환경도 레벨용 슬라이더 (EcoLevel, 최대 5)")]
    [FormerlySerializedAs("ecoSlider")]
    [SerializeField] private Slider _ecoSlider;

    [Header("정규화 분모 (각 레벨의 최대치)")]
    [Min(1)]
    [SerializeField] private int _maxSustainability = 10;
    [Min(1)]
    [SerializeField] private int _maxDevelopment = 5;
    [Min(1)]
    [SerializeField] private int _maxEco = 5;

    [Header("애니메이션")]
    [Tooltip("목표 값에 도달하는 부드러움 시간(초). 작을수록 빨리/뚝 도착, 클수록 더 천천히 자연스럽게. 0이면 즉시 반영.")]
    [Min(0f)]
    [SerializeField] private float _smoothTime = 0.4f;

    [Header("디버그")]
    [SerializeField] private List<LevelSample> _samples = new List<LevelSample>();
    private List<LevelSample> _recordSamples = new List<LevelSample>();

    private bool _isSubscribed;
    private bool _isSubscribedToGame;
    private int _lastReplayedIndex = -1;
    // 슬라이더별 목표값과 SmoothDamp 내부 속도. 매 프레임 현재값을 목표값으로 부드럽게 이동시킵니다.
    private float _sustainabilityTarget;
    private float _developmentTarget;
    private float _ecoTarget;
    private float _sustainabilityVelocity;
    private float _developmentVelocity;
    private float _ecoVelocity;

    private void Awake()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
        if (_timer == null)
            _timer = GameTimer.Instance;
    }

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
        if (!_isSubscribed || !_isSubscribedToGame)
            TrySubscribe();

        // 리플레이 중에는 EarthStateManager가 멈춰 StateChanged가 오지 않으므로,
        // 기록해 둔 샘플을 GameTimer 진행 시간에 맞춰 직접 목표값으로 적용합니다.
        if (IsReplayPlaying())
            ApplyLevelsForReplayTime(_timer.CurrentTime);

        // 매 프레임 현재 슬라이더 값을 목표값으로 자연스럽게 보간합니다.
        StepSlider(_sustainabilitySlider, _sustainabilityTarget, ref _sustainabilityVelocity);
        StepSlider(_developmentSlider,    _developmentTarget,    ref _developmentVelocity);
        StepSlider(_ecoSlider,            _ecoTarget,            ref _ecoVelocity);
    }

    private void StepSlider(Slider slider, float target, ref float velocity)
    {
        if (slider == null)
            return;

        if (_smoothTime <= 0f)
        {
            slider.value = target;
            velocity = 0f;
            return;
        }

        slider.value = Mathf.SmoothDamp(slider.value, target, ref velocity, _smoothTime);
    }

    private void TrySubscribe()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
        if (_timer == null)
            _timer = GameTimer.Instance;

        if (!_isSubscribed && _earthStateManager != null)
        {
            _earthStateManager.StateChanged += OnStateChanged;
            _isSubscribed = true;
            ApplySnapshot(_earthStateManager.CurrentState);
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
        if (_isSubscribed && _earthStateManager != null)
            _earthStateManager.StateChanged -= OnStateChanged;
        if (_isSubscribedToGame && GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart -= OnGameStart;
            GameManager.Instance.OnGameEnd -= OnGameEnd;
            GameManager.Instance.OnReplay -= OnReplay;
        }
        _isSubscribed = false;
        _isSubscribedToGame = false;
    }

    // 새 게임 시작마다 이전 기록을 비웁니다.
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

    // R 키 리플레이 시작 신호. 다음 Update부터 기록 시간 기반으로 슬라이더를 다시 움직입니다.
    private void OnReplay()
    {
        _lastReplayedIndex = -1;
    }

    private void OnStateChanged(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        // 플레이 중에는 (시간, eco, dev) 변화를 기록합니다.
        RecordSample(snapshot);

        // 리플레이가 "재생 중"일 때만 기록 재생(ApplyLevelsForReplayTime)이 슬라이더를 담당하므로
        // 여기서는 실시간 반영을 건너뜁니다.
        // TimeOut(리플레이 대기)처럼 타이머가 멈춘 상태에서는 ResetState 같은 StateChanged를 그대로 반영해
        // 발전도/친환경도 슬라이더가 초기 상태로 되돌아가 대기하도록 합니다.
        if (IsReplayPlaying())
            return;

        ApplySnapshot(snapshot);
    }

    // 친환경도/발전도 단계가 직전 샘플과 달라졌을 때만 (시간, eco, dev) 한 줄을 추가합니다.
    private void RecordSample(EarthStateSnapshot snapshot)
    {
        if (_timer == null || GameManager.Instance == null)
            return;
        if (GameManager.Instance.CurrentGameState != GameState.Playing)
            return;

        int eco = snapshot.EcoLevel;
        int dev = snapshot.DevelopmentLevel;

        if (_samples.Count > 0)
        {
            LevelSample last = _samples[_samples.Count - 1];
            if (last.Eco == eco && last.Dev == dev)
                return;
        }

        _samples.Add(new LevelSample { Time = _timer.CurrentTime, Eco = eco, Dev = dev });
    }

    // 리플레이가 실제로 재생 중인지(IsReplay이면서 타이머가 돌고 있는지) 판정합니다.
    // TimeOut 대기 중에는 타이머가 멈춰 있어 false가 되므로, 이때의 StateChanged(초기화 등)는 그대로 반영됩니다.
    private bool IsReplayPlaying()
    {
        return _timer != null && GameManager.Instance != null && GameManager.Instance.IsReplay && _timer.IsRunning;
    }

    // currentTime 이전의 마지막 샘플을 골라 세 슬라이더의 목표값에 한 번에 적용합니다.
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
        SetTargets(sample.Eco, sample.Dev);
        _lastReplayedIndex = idx;
    }

    private void ApplySnapshot(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        SetTargets(snapshot.EcoLevel, snapshot.DevelopmentLevel);
    }

    // 친환경도/발전도 레벨로부터 세 슬라이더의 목표값을 계산합니다.
    private void SetTargets(int eco, int dev)
    {
        int sustainability = eco + dev;
        _sustainabilityTarget = ComputeSliderTarget(_sustainabilitySlider, sustainability, _maxSustainability);
        _developmentTarget    = ComputeSliderTarget(_developmentSlider, dev, _maxDevelopment);
        _ecoTarget            = ComputeSliderTarget(_ecoSlider, eco, _maxEco);
    }

    // value/max 비율을 슬라이더의 min/max 범위에 매핑합니다 (min/max가 0~1이 아닌 경우에도 안전).
    private static float ComputeSliderTarget(Slider slider, int value, int max)
    {
        if (slider == null)
            return 0f;

        float ratio = Mathf.Clamp01((float)value / Mathf.Max(1, max));
        return Mathf.Lerp(slider.minValue, slider.maxValue, ratio);
    }
}
