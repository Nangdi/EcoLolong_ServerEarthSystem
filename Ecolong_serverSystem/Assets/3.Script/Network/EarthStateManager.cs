using System;
using UnityEngine;

public class EarthStateManager : MonoBehaviour
{
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
    // 예를 들어 친환경도 5, 발전도 1이면 "자연낙원",
    // 친환경도 1, 발전도 5이면 "붕괴직전 사회"가 됩니다.
    private static readonly string[,] StateNameTable =
    {
        { "생태붕괴", "환경 재난사회", "공해사회", "디스토피아", "붕괴직전 사회" },
        { "환경악화", "공해 산업사회", "과잉 산업사회", "오염 산업사회", "환경 위기사회" },
        { "저에너지 사회", "전환기 사회", "산업사회", "고도 산업사회", "기술 중심사회" },
        { "자연보존", "저탄소 사회", "균형 발전사회", "친환경 산업사회", "녹색 미래도시" },
        { "자연낙원", "자연중심사회", "친환경 전환사회", "녹색 기술사회", "지속가능 문명" }
    };

    [Header("TCP 연결")]
    [SerializeField] private TcpDataAggregator aggregator;

    [Header("친환경도 보정")]
    [Range(-1, 1)]
    [SerializeField] private int softwareEcoOffset;

    [Header("게임 연동")]
    [SerializeField] private bool resetAggregatorOnGameStart = true;
    [SerializeField] private bool freezeStateWhenGameEnds = true;

    [Header("기후 계산 상수")]
    [SerializeField] private float carbonTokenRate = 0.001f;
    [SerializeField] private float arcticIceFactor = 35f;
    [SerializeField] private float seaLevelFactor = 0.25f;

    // 현재 계산 결과를 항상 들고 있는 런타임 스냅샷입니다.
    // 다른 스크립트는 이 값을 읽거나 StateChanged 이벤트를 구독해서 사용하면 됩니다.
    private readonly EarthStateSnapshot currentState = new EarthStateSnapshot();

    // 상태가 실제로 바뀌었을 때만 외부로 알려주는 이벤트입니다.
    public event Action<EarthStateSnapshot> StateChanged;

    public EarthStateSnapshot CurrentState => currentState;

    private bool isStateTrackingActive = true;
    private bool isSubscribedToGameEvents;
    private GameState lastObservedGameState = GameState.Ready;

    private void Awake()
    {
        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();

        if (GameManager.Instance != null)
            lastObservedGameState = GameManager.Instance.CurrentGameState;
    }

    private void OnEnable()
    {
        // TCP 집계기 값이 바뀔 때마다 지구 상태도 다시 계산되도록 연결합니다.
        SubscribeAggregator();
        TrySubscribeGameEvents();

        // 씬 활성화 직후에도 현재 누적값 기준으로 한 번 계산합니다.
        RefreshState();
    }

    private void OnDisable()
    {
        UnsubscribeAggregator();
        UnsubscribeGameEvents();
    }

    private void OnValidate()
    {
        // 인스펙터에서 보정값을 바꿨을 때 현재 단계가 바로 다시 계산되도록 합니다.
        softwareEcoOffset = Mathf.Clamp(softwareEcoOffset, -1, 1);
        carbonTokenRate = Mathf.Max(0f, carbonTokenRate);
        arcticIceFactor = Mathf.Max(0f, arcticIceFactor);
        seaLevelFactor = Mathf.Max(0f, seaLevelFactor);
        RefreshState();
    }

    private void Update()
    {
        TrySubscribeGameEvents();
        SyncTrackingStateWithGame();

        // 플레이 중에는 시간에 따라 탄소농도와 파생 지표가 계속 변하므로
        // 매 프레임 현재 단계 기준으로 다시 계산합니다.
        if (Application.isPlaying && isStateTrackingActive)
            RefreshState(Time.deltaTime);
    }

    public void SetAggregator(TcpDataAggregator targetAggregator)
    {
        if (aggregator == targetAggregator)
            return;

        // 집계기를 교체할 수 있게 열어둔 메서드입니다.
        // 예: 테스트용 aggregator와 실사용 aggregator를 런타임에 바꿔 끼울 때 사용.
        UnsubscribeAggregator();
        aggregator = targetAggregator;
        SubscribeAggregator();
        RefreshState();
    }

    // 게임 시작 시 지구 상태 계산을 새 게임 기준으로 초기화합니다.
    public void InitializeForGameStart()
    {
        isStateTrackingActive = true;
        lastObservedGameState = GameState.Playing;

        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();

        if (resetAggregatorOnGameStart && aggregator != null)
            aggregator.ClearTotals();

        currentState.ResetToDefaults();
        RefreshState();
    }

    // 게임 종료 후에는 현재 상태가 더 이상 변하지 않도록 계산을 멈춥니다.
    public void FreezeState()
    {
        if (!freezeStateWhenGameEnds)
            return;

        isStateTrackingActive = false;

        if (GameManager.Instance != null)
            lastObservedGameState = GameManager.Instance.CurrentGameState;
    }

    public void SetSoftwareEcoOffset(int offset)
    {
        int clampedOffset = Mathf.Clamp(offset, -1, 1);
        if (softwareEcoOffset == clampedOffset)
            return;

        // 기획서의 "SW 가중치 ±1단계"를 코드로 반영하는 부분입니다.
        // 계산 결과가 1보다 작거나 5보다 커지지 않도록 먼저 -1~1로 제한합니다.
        softwareEcoOffset = clampedOffset;
        RefreshState();
    }

    [ContextMenu("Refresh Earth State")]
    public void RefreshState()
    {
        RefreshState(0f);
    }

    // 단계, 탄소농도, 온도, 북극얼음, 해수면을 한 번에 다시 계산합니다.
    public void RefreshState(float deltaTime)
    {
        // inspector 연결이 비어 있어도 동작하도록 런타임에서 한 번 더 탐색합니다.
        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();

        // TCP 집계기의 누적 탄소/발전 값을 읽습니다.
        EnergyTotals totals = aggregator != null ? aggregator.GetEnergyTotals() : null;
        int carbonCount = totals != null ? totals.Carbon : 0;
        int powerGenerationCount = totals != null ? totals.PowerGeneration : 0;

        // count -> 단계 변환.
        // 탄소가 많을수록 친환경도는 내려가고,
        // 발전량이 많을수록 발전도는 올라갑니다.
        int ecoLevel = CalculateEcoLevel(carbonCount, softwareEcoOffset);
        int developmentLevel = CalculateDevelopmentLevel(powerGenerationCount);
        string stateName = GetStateName(ecoLevel, developmentLevel);
        float carbonRatePerSecond = CalculateCarbonPpmChangePerSecond(ecoLevel, developmentLevel, carbonCount);

        // 플레이 중에는 초당 변화량을 누적하고, 에디터 정지 상태에서는 현재 저장값을 유지합니다.
        float nextCarbonPpm = currentState.CarbonPpm;
        if (!Application.isPlaying)
            nextCarbonPpm = Mathf.Max(PreIndustrialCarbonPpm, currentState.CarbonPpm);
        else
            nextCarbonPpm = Mathf.Max(PreIndustrialCarbonPpm, currentState.CarbonPpm + carbonRatePerSecond * Mathf.Max(0f, deltaTime));

        float temperatureDeltaC = CalculateTemperatureDelta(nextCarbonPpm);
        float arcticIcePercent = CalculateArcticIcePercent(temperatureDeltaC);
        float seaLevelRiseMeters = CalculateSeaLevelRiseMeters(temperatureDeltaC);

        // 같은 값으로 다시 계산된 경우에는 불필요한 이벤트 발행을 막기 위해
        // 이전 상태와 비교해서 실제 변화가 있었는지 확인합니다.
        bool hasChanged =
            currentState.CarbonCount != carbonCount ||
            currentState.PowerGenerationCount != powerGenerationCount ||
            currentState.EcoLevel != ecoLevel ||
            currentState.DevelopmentLevel != developmentLevel ||
            currentState.EcoLevelOffset != softwareEcoOffset ||
            !string.Equals(currentState.StateName, stateName, StringComparison.Ordinal) ||
            !Mathf.Approximately(currentState.CarbonPpm, nextCarbonPpm) ||
            !Mathf.Approximately(currentState.CarbonPpmChangePerSecond, carbonRatePerSecond) ||
            !Mathf.Approximately(currentState.TemperatureDeltaC, temperatureDeltaC) ||
            !Mathf.Approximately(currentState.ArcticIcePercent, arcticIcePercent) ||
            !Mathf.Approximately(currentState.SeaLevelRiseMeters, seaLevelRiseMeters);
        // 최신 계산 결과를 현재 상태 스냅샷에 반영합니다.
        currentState.SetValues(
            carbonCount,
            powerGenerationCount,
            ecoLevel,
            developmentLevel,
            softwareEcoOffset,
            stateName,
            nextCarbonPpm,
            carbonRatePerSecond,
            temperatureDeltaC,
            arcticIcePercent,
            seaLevelRiseMeters);

        // 수치나 상태명이 바뀐 경우에만 외부에 알립니다.
        if (hasChanged)
            StateChanged?.Invoke(currentState);
    }

    public static int CalculateEcoLevel(int carbonCount, int ecoOffset = 0)
    {
        // 사진 기준:
        // 5단계 = 0~14
        // 4단계 = 15~34
        // 3단계 = 35~54
        // 2단계 = 55~79
        // 1단계 = 80 이상
        // 이후 SW 보정값(-1~+1)을 더하고, 최종값은 1~5 범위로 고정합니다.
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
        // 사진 기준:
        // 1단계 = 0~159
        // 2단계 = 160~219
        // 3단계 = 220~279
        // 4단계 = 280~339
        // 5단계 = 340 이상
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
        float carbonTokenContribution = carbonTokenCount * carbonTokenRate;
        return ecoRate + developmentRate + carbonTokenContribution;
    }

    public static float CalculateTemperatureDelta(float carbonPpm)
    {
        float safeCarbonPpm = Mathf.Max(0.0001f, carbonPpm);
        return TemperatureLogFactor * Mathf.Log(safeCarbonPpm / PreIndustrialCarbonPpm);
    }

    public float CalculateArcticIcePercent(float temperatureDeltaC)
    {
        return Mathf.Clamp(100f - arcticIceFactor * temperatureDeltaC, 0f, 100f);
    }

    public float CalculateSeaLevelRiseMeters(float temperatureDeltaC)
    {
        return Mathf.Max(0f, seaLevelFactor * temperatureDeltaC);
    }

    public static string GetStateName(int ecoLevel, int developmentLevel)
    {
        // 단계는 1부터 시작하지만 배열 인덱스는 0부터 시작하므로 -1 해서 맞춥니다.
        int ecoIndex = Mathf.Clamp(ecoLevel, MinLevel, MaxLevel) - 1;
        int developmentIndex = Mathf.Clamp(developmentLevel, MinLevel, MaxLevel) - 1;
        return StateNameTable[ecoIndex, developmentIndex];
    }

    private void SubscribeAggregator()
    {
        if (aggregator == null)
            return;

        // TCP 누적합이 바뀔 때마다 OnTotalsChanged -> RefreshState 흐름으로 이어집니다.
        aggregator.TotalsChanged += OnTotalsChanged;
    }

    private void UnsubscribeAggregator()
    {
        if (aggregator == null)
            return;

        // 씬 비활성화/교체 시 중복 구독을 막기 위해 반드시 해제합니다.
        aggregator.TotalsChanged -= OnTotalsChanged;
    }

    private void TrySubscribeGameEvents()
    {
        if (isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart += OnGameStart;
        GameManager.Instance.OnGameEnd += OnGameEnd;
        isStateTrackingActive = GameManager.Instance.CurrentGameState == GameState.Playing;
        lastObservedGameState = GameManager.Instance.CurrentGameState;
        isSubscribedToGameEvents = true;
    }

    private void UnsubscribeGameEvents()
    {
        if (!isSubscribedToGameEvents || GameManager.Instance == null)
            return;

        GameManager.Instance.OnGameStart -= OnGameStart;
        GameManager.Instance.OnGameEnd -= OnGameEnd;
        isSubscribedToGameEvents = false;
    }

    private void SyncTrackingStateWithGame()
    {
        if (!Application.isPlaying || GameManager.Instance == null)
            return;

        GameState currentGameState = GameManager.Instance.CurrentGameState;
        if (currentGameState == lastObservedGameState)
            return;

        if (currentGameState == GameState.Playing)
            InitializeForGameStart();
        else
            FreezeState();

        lastObservedGameState = currentGameState;
    }

    private void OnTotalsChanged(EnergyTotals totals)
    {
        // 이벤트에서 totals를 바로 계산에 써도 되지만,
        // 계산 진입점을 RefreshState 하나로 모아두면 유지보수가 쉬워집니다.
        if (!isStateTrackingActive && Application.isPlaying)
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
