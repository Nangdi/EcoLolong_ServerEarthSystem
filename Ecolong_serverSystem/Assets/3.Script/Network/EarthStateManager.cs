using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EarthStateManager : MonoBehaviour
{
    private static EarthStateManager s_instance;

    // 다른 스크립트가 처음 접근하는 시점에 씬에서 한 번 찾아서 보완하는 lazy singleton getter입니다.
    public static EarthStateManager Instance
    {
        get
        {
            if (s_instance == null)
                s_instance = FindObjectOfType<EarthStateManager>();
            return s_instance;
        }
        private set { s_instance = value; }
    }

    private const int MaxLevel = 5;
    private const int MinLevel = 1;
    private const float PreIndustrialCarbonPpm = 280f;
    private const float TemperatureLogFactor = 4.28f;

    // 레벨은 항상 1~5의 5단계이므로 경계가 되는 임계값은 4개입니다.
    private const int LevelThresholdCount = MaxLevel - MinLevel;

    // 친환경도 판정 기본 임계값입니다. 설정창과 동일하게 1→5단계 순으로 정렬합니다.
    // 인덱스 0~3이 "친환경도 1/2/3/4 단계 경계"에 대응합니다(탄소가 인덱스3 미만이면 5단계).
    private static readonly int[] DefaultEcoCarbonThresholds = { 80, 55, 35, 15 };

    // 발전도 판정 기본 임계값입니다. 설정창과 동일하게 1→5단계 순으로 정렬합니다.
    // 인덱스 0~3이 "발전도 2/3/4/5 단계 경계"에 대응합니다(발전이 인덱스0 미만이면 1단계).
    private static readonly int[] DefaultDevelopmentThresholds = { 160, 220, 280, 340 };

    // 사진 표를 그대로 옮긴 친환경도 단계별 탄소농도 변화량입니다.
    // 인덱스 0~4가 친환경도 1~5 단계에 대응합니다.
    private static readonly float[] EcoCarbonRateTable = { 0.12f, 0.06f, 0.01f, -0.04f, -0.08f };

    // 사진 표를 그대로 옮긴 발전도 단계별 탄소농도 변화량입니다.
    // 인덱스 0~4가 발전도 1~5 단계에 대응합니다.
    private static readonly float[] DevelopmentCarbonRateTable = { 0.1f, 0.1f, 0.15f, 0.1f, 0.05f };

    // [행, 열] = [친환경도, 발전도] 로 읽는 5x5 상태표입니다.
    private static readonly string[,] StateNameTable =
    {
        { "생태붕괴", "환경 재난사회", "공해사회", "디스토피아", "붕괴직전 사회" },
        { "환경악화", "공해 산업사회", "과잉 산업사회", "오염 산업사회", "환경 위기사회" },
        { "저에너지 사회", "전환기 사회", "산업사회", "고도 산업사회", "기술 중심사회" },
        { "자연보존", "저탄소 사회", "균형 발전사회", "친환경 산업사회", "녹색 미래도시" },
        { "자연낙원", "자연중심사회", "친환경 전환사회", "녹색 기술사회", "지속가능 문명" }
    };

    [Header("TCP 연결")]
    [FormerlySerializedAs("aggregator")]
    [SerializeField] private TcpDataAggregator _aggregator;

    [Header("친환경도 보정")]
    // 도시친환경도(cityEcoScore)로부터 매 RefreshState마다 자동 계산되는 보정값입니다(-1/0/+1).
    [Range(-1, 1)]
    [FormerlySerializedAs("softwareEcoOffset")]
    [SerializeField] private int _softwareEcoOffset;

    [Tooltip("cityEcoScore가 이 값 이상이면 친환경도 +1 보정.")]
    [SerializeField] private int _cityEcoOffsetUpperThreshold = 20;
    [Tooltip("cityEcoScore가 이 값 이하이면 친환경도 -1 보정.")]
    [SerializeField] private int _cityEcoOffsetLowerThreshold = -20;

    [Header("게임 연동")]
    [FormerlySerializedAs("resetAggregatorOnGameStart")]
    [SerializeField] private bool _resetAggregatorOnGameStart = true;
    [FormerlySerializedAs("freezeStateWhenGameEnds")]
    [SerializeField] private bool _freezeStateWhenGameEnds = true;

    [Header("레벨 판정 기준 (런타임 변경 가능)")]
    [Tooltip("친환경도 임계값(1→5단계 순 4개). [0]=1단계 경계 ... [3]=4단계 경계.")]
    [SerializeField] private int[] _ecoCarbonThresholds = { 80, 55, 35, 15 };
    [Tooltip("발전도 임계값(1→5단계 순 4개). [0]=2단계 경계 ... [3]=5단계 경계.")]
    [SerializeField] private int[] _developmentThresholds = { 160, 220, 280, 340 };

    [Header("리소스 속도 보정")]
    [Tooltip("리소스(탄소 ppm) 변화량 테이블이 튜닝된 기준 게임 길이(초). 기본 900초=15분. " +
             "실제 gameTime이 이와 달라도 게임 종료 시 누적 변화량이 동일하게 유지되도록 속도를 자동 보정합니다.")]
    [SerializeField] private float _balanceReferenceSeconds = 900f;

    [Header("기후 계산 상수")]
    [FormerlySerializedAs("carbonTokenRate")]
    [SerializeField] private float _carbonTokenRate = 0.001f;
    [FormerlySerializedAs("arcticIceFactor")]
    [SerializeField] private float _arcticIceFactor = 35f;
    [FormerlySerializedAs("seaLevelFactor")]
    [SerializeField] private float _seaLevelFactor = 0.25f;

    // 현재 계산 결과를 항상 들고 있는 런타임 스냅샷입니다.
    private readonly EarthStateSnapshot _currentState = new EarthStateSnapshot();

    public event Action<EarthStateSnapshot> StateChanged;

    public EarthStateSnapshot CurrentState => _currentState;

    private bool _isStateTrackingActive = true;
    private bool _isSubscribedToGameEvents;
    private GameState _lastObservedGameState = GameState.Ready;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _ecoCarbonThresholds = NormalizeThresholds(_ecoCarbonThresholds, DefaultEcoCarbonThresholds);
        _developmentThresholds = NormalizeThresholds(_developmentThresholds, DefaultDevelopmentThresholds);

        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;

        if (GameManager.Instance != null)
            _lastObservedGameState = GameManager.Instance.CurrentGameState;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // JsonManager가 디스크에서 불러온 임계값을 시작 시점에 반영합니다.
        // (Awake 순서에 의존하지 않도록 Start에서 적용)
        ApplyFromSettings();
    }

    private void OnEnable()
    {
        SubscribeAggregator();
        TrySubscribeGameEvents();
        RefreshState();
    }

    private void OnDisable()
    {
        UnsubscribeAggregator();
        UnsubscribeGameEvents();
    }

    private void OnValidate()
    {
        _softwareEcoOffset = Mathf.Clamp(_softwareEcoOffset, -1, 1);
        _balanceReferenceSeconds = Mathf.Max(1f, _balanceReferenceSeconds);
        _carbonTokenRate = Mathf.Max(0f, _carbonTokenRate);
        _arcticIceFactor = Mathf.Max(0f, _arcticIceFactor);
        _seaLevelFactor = Mathf.Max(0f, _seaLevelFactor);
        _ecoCarbonThresholds = NormalizeThresholds(_ecoCarbonThresholds, DefaultEcoCarbonThresholds);
        _developmentThresholds = NormalizeThresholds(_developmentThresholds, DefaultDevelopmentThresholds);
        RefreshState();
    }

    private void Update()
    {
        TrySubscribeGameEvents();
        SyncTrackingStateWithGame();

        if (Application.isPlaying && _isStateTrackingActive)
        {
            // 탄소 ppm 변화량은 "게임시간 1초당 N ppm" 단위로 정의돼 있으므로,
            // 누적 시에도 게임시간 델타(=실시간 * gameTimeScale)를 사용해야 합니다.
            // 그렇지 않으면 gameTimeScale=60일 때 타이머는 15초만에 끝나지만 ppm은 15초치만 누적돼 변화가 거의 없습니다.
            float scale = GameTimer.Instance != null ? GameTimer.Instance.settingGameScale : 1f;
            if (scale <= 0f) scale = 1f;

            // 변화량 테이블은 _balanceReferenceSeconds(기본 900초=15분) 길이를 기준으로 튜닝돼 있습니다.
            // gameTime이 기준과 다르면 게임 종료 시점의 누적 변화량이 동일하게 유지되도록
            // (기준 길이 / 실제 길이) 비율로 증가 속도를 동적으로 보정합니다.
            // 예) gameTime=450초(7.5분)이면 보정계수=2배 → 절반의 시간에 15분치 총 변화량에 도달.
            float gameTime = GameTimer.Instance != null ? GameTimer.Instance.gameTime : _balanceReferenceSeconds;
            float lengthCompensation = gameTime > 0f ? _balanceReferenceSeconds / gameTime : 1f;

            RefreshState(Time.unscaledDeltaTime * scale * lengthCompensation);
        }
    }

    public void SetAggregator(TcpDataAggregator targetAggregator)
    {
        if (_aggregator == targetAggregator)
            return;

        UnsubscribeAggregator();
        _aggregator = targetAggregator;
        SubscribeAggregator();
        RefreshState();
    }

    public void InitializeForGameStart()
    {
        _isStateTrackingActive = true;
        _lastObservedGameState = GameState.Playing;

        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;

        if (_resetAggregatorOnGameStart && _aggregator != null)
            _aggregator.ClearTotals();

        _currentState.ResetToDefaults();
        RefreshState();
    }

    public void FreezeState()
    {
        if (!_freezeStateWhenGameEnds)
            return;

        _isStateTrackingActive = false;

        if (GameManager.Instance != null)
            _lastObservedGameState = GameManager.Instance.CurrentGameState;
    }

    // 스냅샷을 초기값으로 되돌리고 StateChanged를 강제로 발행해 UI 텍스트까지 함께 갱신합니다.
    // RefreshState는 동일값일 때 이벤트를 발행하지 않으므로, 외부 리셋 경로에서는 이 메서드를 사용합니다.
    public void ResetState()
    {
        _currentState.ResetToDefaults();
        StateChanged?.Invoke(_currentState);
    }

    public void SetSoftwareEcoOffset(int offset)
    {
        int clampedOffset = Mathf.Clamp(offset, -1, 1);
        if (_softwareEcoOffset == clampedOffset)
            return;

        _softwareEcoOffset = clampedOffset;
        RefreshState();
    }

    [ContextMenu("Refresh Earth State")]
    public void RefreshState()
    {
        RefreshState(0f);
    }

    public void RefreshState(float deltaTime)
    {
        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;

        EnergyTotals totals = _aggregator != null ? _aggregator.GetEnergyTotals() : null;
        int carbonCount = totals != null ? totals.totalCarbon : 0;
        int powerGenerationCount = totals != null ? totals.powerGeneration : 0;
        int electricCount = totals != null ? totals.electricCount : 0;
        int currentCarbon = totals != null ? totals.totalCarbon - totals.captureCarbon : 0;
        int currentPowerGeneration = totals != null ? totals.currentPowerGeneration : 0;
        int cityEcoScore = totals != null ? totals.cityEcoScore : 0;

        // 도시친환경도에 따라 친환경도 보정값(-1/0/+1)을 자동으로 결정합니다.
        _softwareEcoOffset = CalculateEcoOffset(cityEcoScore);

        int ecoLevel = CalculateEcoLevel(currentCarbon, _softwareEcoOffset);
        int developmentLevel = CalculateDevelopmentLevel(powerGenerationCount);
        string stateName = GetStateName(ecoLevel, developmentLevel);
        float carbonRatePerSecond = CalculateCarbonPpmChangePerSecond(ecoLevel, developmentLevel, carbonCount);

        float nextCarbonPpm = _currentState.CarbonPpm;
        if (Application.isPlaying)
        {
            // nextCarbonPpm = Mathf.Max(PreIndustrialCarbonPpm, _currentState.CarbonPpm + carbonRatePerSecond * Mathf.Max(0f, deltaTime));
            nextCarbonPpm =  _currentState.CarbonPpm + carbonRatePerSecond * Mathf.Max(0f, deltaTime);
            
        }
            // nextCarbonPpm = Mathf.Max(PreIndustrialCarbonPpm, _currentState.CarbonPpm);
            // nextCarbonPpm =  _currentState.CarbonPpm;ㄴ

        float temperatureDeltaC = Mathf.Max(-1f, CalculateTemperatureDelta(nextCarbonPpm+280));
        float arcticIcePercent = CalculateArcticIcePercent(temperatureDeltaC);
        float seaLevelRiseMeters = CalculateSeaLevelRiseMeters(temperatureDeltaC);

        bool hasChanged = _currentState.SetValues(
            carbonCount,
            powerGenerationCount,
            electricCount,
            currentCarbon,
            currentPowerGeneration,
            ecoLevel,
            developmentLevel,
            _softwareEcoOffset,
            stateName,
            nextCarbonPpm,
            carbonRatePerSecond,
            temperatureDeltaC,
            arcticIcePercent,
            seaLevelRiseMeters);

        if (hasChanged)
            StateChanged?.Invoke(_currentState);
    }

    public int CalculateEcoLevel(int carbonCount, int ecoOffset = 0)
    {
        // 임계값 배열은 설정창과 동일하게 1→5단계 순으로 저장됩니다([0]=1단계 경계 ... [3]=4단계 경계).
        // 판정은 레벨 5(최고)부터 내려가므로 인덱스를 [3]→[0] 순으로 읽습니다.
        int baseLevel;

        if (carbonCount < _ecoCarbonThresholds[3])
            baseLevel = 5;
        else if (carbonCount < _ecoCarbonThresholds[2])
            baseLevel = 4;
        else if (carbonCount < _ecoCarbonThresholds[1])
            baseLevel = 3;
        else if (carbonCount < _ecoCarbonThresholds[0])
            baseLevel = 2;
        else
            baseLevel = 1;

        return Mathf.Clamp(baseLevel + ecoOffset, MinLevel, MaxLevel);
    }

    // 도시친환경도(cityEcoScore)를 상/하한 임계값과 비교해 친환경도 보정값을 구합니다.
    // 상한 이상이면 +1, 하한 이하이면 -1, 그 사이면 0입니다.
    public int CalculateEcoOffset(int cityEcoScore)
    {
        if (cityEcoScore >= _cityEcoOffsetUpperThreshold)
            return 1;
        if (cityEcoScore <= _cityEcoOffsetLowerThreshold)
            return -1;
        return 0;
    }

    public int CalculateDevelopmentLevel(int powerGenerationCount)
    {
        // 임계값 배열은 설정창과 동일하게 1→5단계 순으로 저장됩니다([0]=2단계 경계 ... [3]=5단계 경계).
        // 판정은 레벨 5(최고)부터 내려가므로 인덱스를 [3]→[0] 순으로 읽습니다.
        if (powerGenerationCount >= _developmentThresholds[3])
            return 5;
        else if (powerGenerationCount >= _developmentThresholds[2])
            return 4;
        else if (powerGenerationCount >= _developmentThresholds[1])
            return 3;
        else if (powerGenerationCount >= _developmentThresholds[0])
            return 2;
        else
            return 1;
    }

    // 현재 적용 중인 친환경도/발전도 임계값을 읽기 전용으로 노출합니다.
    public IReadOnlyList<int> EcoCarbonThresholds => _ecoCarbonThresholds;
    public IReadOnlyList<int> DevelopmentThresholds => _developmentThresholds;

    // JsonManager에 저장된 설정값(ecoCarbonThresholds/developmentThresholds)을 읽어 즉시 적용합니다.
    // 게임 시작 시점과 ESC 설정창 저장 직후에 호출되어 디스크 설정과 런타임 상태를 일치시킵니다.
    public void ApplyFromSettings()
    {
        JsonManager json = JsonManager.instance != null ? JsonManager.instance : FindObjectOfType<JsonManager>();
        if (json == null || json.gameSettingData == null)
            return;

        GameSettingData data = json.gameSettingData;
        _ecoCarbonThresholds = NormalizeThresholds(data.ecoCarbonThresholds, DefaultEcoCarbonThresholds);
        _developmentThresholds = NormalizeThresholds(data.developmentThresholds, DefaultDevelopmentThresholds);
        _cityEcoOffsetUpperThreshold = data.cityEcoOffsetUpperThreshold;
        _cityEcoOffsetLowerThreshold = data.cityEcoOffsetLowerThreshold;

        // 디스크 값에 맞춰 GameSettingData도 보정된 배열로 되돌려, UI/저장 값이 항상 4개를 유지하게 합니다.
        data.ecoCarbonThresholds = (int[])_ecoCarbonThresholds.Clone();
        data.developmentThresholds = (int[])_developmentThresholds.Clone();

        RefreshState();
    }

    // 친환경도 임계값 전체를 런타임에 교체합니다(내림차순 4개). 교체 후 상태를 즉시 재계산합니다.
    public void SetEcoCarbonThresholds(params int[] thresholds)
    {
        if (!TryApplyThresholds(ref _ecoCarbonThresholds, thresholds))
            return;

        RefreshState();
    }

    // 발전도 임계값 전체를 런타임에 교체합니다(내림차순 4개). 교체 후 상태를 즉시 재계산합니다.
    public void SetDevelopmentThresholds(params int[] thresholds)
    {
        if (!TryApplyThresholds(ref _developmentThresholds, thresholds))
            return;

        RefreshState();
    }

    // 친환경도 임계값을 인덱스 단위로 하나만 변경합니다(0=1단계 경계 ... 3=4단계 경계).
    public void SetEcoCarbonThreshold(int index, int value)
    {
        if (!TrySetThresholdAt(_ecoCarbonThresholds, index, value))
            return;

        RefreshState();
    }

    // 발전도 임계값을 인덱스 단위로 하나만 변경합니다(0=5단계 경계 ... 3=2단계 경계).
    public void SetDevelopmentThreshold(int index, int value)
    {
        if (!TrySetThresholdAt(_developmentThresholds, index, value))
            return;

        RefreshState();
    }

    private static bool TryApplyThresholds(ref int[] target, int[] thresholds)
    {
        if (thresholds == null || thresholds.Length != LevelThresholdCount)
        {
            Debug.LogWarning($"[EarthStateManager] 임계값은 정확히 {LevelThresholdCount}개여야 합니다. 변경을 무시합니다.");
            return false;
        }

        target = (int[])thresholds.Clone();
        return true;
    }

    private static bool TrySetThresholdAt(int[] target, int index, int value)
    {
        if (target == null || index < 0 || index >= target.Length)
        {
            Debug.LogWarning($"[EarthStateManager] 임계값 인덱스 {index}가 범위를 벗어났습니다. 변경을 무시합니다.");
            return false;
        }

        target[index] = value;
        return true;
    }

    // 인스펙터에서 배열을 비우거나 길이를 바꿔도 항상 4개를 유지하도록 보정합니다.
    private static int[] NormalizeThresholds(int[] thresholds, int[] defaults)
    {
        if (thresholds != null && thresholds.Length == LevelThresholdCount)
            return thresholds;

        int[] normalized = (int[])defaults.Clone();
        if (thresholds != null)
        {
            int copyCount = Mathf.Min(thresholds.Length, LevelThresholdCount);
            for (int i = 0; i < copyCount; i++)
                normalized[i] = thresholds[i];
        }

        return normalized;
    }

    public float CalculateCarbonPpmChangePerSecond(int ecoLevel, int developmentLevel, int carbonTokenCount)
    {
        float ecoRate = EcoCarbonRateTable[Mathf.Clamp(ecoLevel, MinLevel, MaxLevel) - 1];
        float developmentRate = DevelopmentCarbonRateTable[Mathf.Clamp(developmentLevel, MinLevel, MaxLevel) - 1];
        float carbonTokenContribution = carbonTokenCount * _carbonTokenRate;
        return ecoRate + developmentRate + carbonTokenContribution;
    }

    public static float CalculateTemperatureDelta(float carbonPpm)
    {
        float safeCarbonPpm = Mathf.Max(-1f, carbonPpm);
        return TemperatureLogFactor * Mathf.Log(safeCarbonPpm / PreIndustrialCarbonPpm);
    }

    public float CalculateArcticIcePercent(float temperatureDeltaC)
    {
        return Mathf.Clamp(100f - _arcticIceFactor * temperatureDeltaC, 0f, 100f);
    }

    public float CalculateSeaLevelRiseMeters(float temperatureDeltaC)
    {
        return Mathf.Max(0f, _seaLevelFactor * temperatureDeltaC);
    }

    public static string GetStateName(int ecoLevel, int developmentLevel)
    {
        int ecoIndex = Mathf.Clamp(ecoLevel, MinLevel, MaxLevel) - 1;
        int developmentIndex = Mathf.Clamp(developmentLevel, MinLevel, MaxLevel) - 1;
        return StateNameTable[ecoIndex, developmentIndex];
    }

    private void SubscribeAggregator()
    {
        if (_aggregator == null)
            return;

        _aggregator.TotalsChanged += OnTotalsChanged;
    }

    private void UnsubscribeAggregator()
    {
        if (_aggregator == null)
            return;

        _aggregator.TotalsChanged -= OnTotalsChanged;
    }

    private void TrySubscribeGameEvents()
    {
        if (_isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart += OnGameStart;
        GameManager.Instance.OnGameEnd += OnGameEnd;
        _isStateTrackingActive = GameManager.Instance.CurrentGameState == GameState.Playing;
        _lastObservedGameState = GameManager.Instance.CurrentGameState;
        _isSubscribedToGameEvents = true;
    }

    private void UnsubscribeGameEvents()
    {
        if (!_isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart -= OnGameStart;
        GameManager.Instance.OnGameEnd -= OnGameEnd;
        _isSubscribedToGameEvents = false;
    }

    private void SyncTrackingStateWithGame()
    {
        if (!Application.isPlaying || GameManager.Instance == null)
            return;

        GameState currentGameState = GameManager.Instance.CurrentGameState;
        if (currentGameState == _lastObservedGameState)
            return;

        if (currentGameState == GameState.Playing)
            InitializeForGameStart();
        else
            FreezeState();

        _lastObservedGameState = currentGameState;
    }

    private void OnTotalsChanged(EnergyTotals totals)
    {
        if (!_isStateTrackingActive && Application.isPlaying)
            return;

        RefreshState();
    }

    private void OnGameStart()
    {
        InitializeForGameStart();
    }

    private void OnGameEnd()
    {
        FreezeState();
    }
}
