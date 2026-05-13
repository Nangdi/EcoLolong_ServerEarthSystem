using System;
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
    [Range(-1, 1)]
    [FormerlySerializedAs("softwareEcoOffset")]
    [SerializeField] private int _softwareEcoOffset;

    [Header("게임 연동")]
    [FormerlySerializedAs("resetAggregatorOnGameStart")]
    [SerializeField] private bool _resetAggregatorOnGameStart = true;
    [FormerlySerializedAs("freezeStateWhenGameEnds")]
    [SerializeField] private bool _freezeStateWhenGameEnds = true;

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
        _carbonTokenRate = Mathf.Max(0f, _carbonTokenRate);
        _arcticIceFactor = Mathf.Max(0f, _arcticIceFactor);
        _seaLevelFactor = Mathf.Max(0f, _seaLevelFactor);
        RefreshState();
    }

    private void Update()
    {
        TrySubscribeGameEvents();
        SyncTrackingStateWithGame();

        if (Application.isPlaying && _isStateTrackingActive)
            RefreshState(Time.deltaTime);
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

        int ecoLevel = CalculateEcoLevel(carbonCount, _softwareEcoOffset);
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

    public static int CalculateEcoLevel(int carbonCount, int ecoOffset = 0)
    {
        int baseLevel;

        if (carbonCount >= 80)
            baseLevel = 1;
        else if (carbonCount >= 55)
            baseLevel = 2;
        else if (carbonCount >= 35)
            baseLevel = 3;
        else if (carbonCount >= 15)
            baseLevel = 4;
        else
            baseLevel = 5;

        return Mathf.Clamp(baseLevel + ecoOffset, MinLevel, MaxLevel);
    }

    public static int CalculateDevelopmentLevel(int powerGenerationCount)
    {
        if (powerGenerationCount >= 340)
            return 5;
        if (powerGenerationCount >= 280)
            return 4;
        if (powerGenerationCount >= 220)
            return 3;
        if (powerGenerationCount >= 160)
            return 2;

        return 1;
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
