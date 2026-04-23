using System;
using UnityEngine;

public class EarthStateManager : MonoBehaviour
{
    private const int MaxLevel = 5;
    private const int MinLevel = 1;

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

    // 현재 계산 결과를 항상 들고 있는 런타임 스냅샷입니다.
    // 다른 스크립트는 이 값을 읽거나 StateChanged 이벤트를 구독해서 사용하면 됩니다.
    private readonly EarthStateSnapshot currentState = new EarthStateSnapshot();

    // 상태가 실제로 바뀌었을 때만 외부로 알려주는 이벤트입니다.
    public event Action<EarthStateSnapshot> StateChanged;

    public EarthStateSnapshot CurrentState => currentState;

    private void Awake()
    {
        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();
    }

    private void OnEnable()
    {
        // TCP 집계기 값이 바뀔 때마다 지구 상태도 다시 계산되도록 연결합니다.
        SubscribeAggregator();

        // 씬 활성화 직후에도 현재 누적값 기준으로 한 번 계산합니다.
        RefreshState();
    }

    private void OnDisable()
    {
        UnsubscribeAggregator();
    }

    private void OnValidate()
    {
        // 인스펙터에서 보정값을 바꿨을 때 현재 단계가 바로 다시 계산되도록 합니다.
        softwareEcoOffset = Mathf.Clamp(softwareEcoOffset, -1, 1);
        RefreshState();
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

        // 같은 값으로 다시 계산된 경우에는 불필요한 이벤트 발행을 막기 위해
        // 이전 상태와 비교해서 실제 변화가 있었는지 확인합니다.
        bool hasChanged =
            currentState.CarbonCount != carbonCount ||
            currentState.PowerGenerationCount != powerGenerationCount ||
            currentState.EcoLevel != ecoLevel ||
            currentState.DevelopmentLevel != developmentLevel ||
            currentState.EcoLevelOffset != softwareEcoOffset ||
            !string.Equals(currentState.StateName, stateName, StringComparison.Ordinal);

        // 최신 계산 결과를 현재 상태 스냅샷에 반영합니다.
        currentState.SetValues(carbonCount, powerGenerationCount, ecoLevel, developmentLevel, softwareEcoOffset, stateName);

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

    private void OnTotalsChanged(EnergyTotals totals)
    {
        // 이벤트에서 totals를 바로 계산에 써도 되지만,
        // 계산 진입점을 RefreshState 하나로 모아두면 유지보수가 쉬워집니다.
        RefreshState();
    }
}
