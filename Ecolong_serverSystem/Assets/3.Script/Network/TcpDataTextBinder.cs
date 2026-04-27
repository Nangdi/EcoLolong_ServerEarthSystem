using TMPro;
using UnityEngine;

public class TcpDataTextBinder : MonoBehaviour
{
    [Header("TCP 연결")]
    [SerializeField] private TcpDataAggregator aggregator;

    [Header("EarthState 연결")]
    [SerializeField] private EarthStateManager earthStateManager;

    [Header("TCP 표시 형식")]
    [SerializeField] private bool showLabel = false;
    [SerializeField] private string valueOnlyFormat = "{0}";
    [SerializeField] private string labeledValueFormat = "{0} : {1}";

    [Header("EarthState 표시 형식")]
    [SerializeField] private string levelFormat = "LEVEL {0}";
    [SerializeField] private string carbonPpmFormat = "{0:0}ppm";
    [SerializeField] private string temperatureFormat = "{0:0.#}°C";
    [SerializeField] private string arcticIceFormat = "{0:0}%";
    [SerializeField] private string seaLevelFormat = "{0:0}cm";

    [Header("TCP 데이터별 TMP_Text 연결")]
    [SerializeField] private TMP_Text thermalPowerText;
    [SerializeField] private TMP_Text hydroPowerText;
    [SerializeField] private TMP_Text solarPowerText;
    [SerializeField] private TMP_Text windPowerText;
    [SerializeField] private TMP_Text hydrogenText;
    [SerializeField] private TMP_Text electricEnergyText;
    [SerializeField] private TMP_Text carbonText;
    [SerializeField] private TMP_Text powerGenerationText;
    [SerializeField] private TMP_Text cityEcoScoreText;
    [SerializeField] private TMP_Text cityBuildingCountText;

    [Header("EarthState 단계/상태 표시")]
    // 친환경도 + 발전도 합으로 계산된 지속가능성 표시용입니다.
    [SerializeField] private TMP_Text sustainabilityLevelText;
    [SerializeField] private TMP_Text developmentLevelText;
    [SerializeField] private TMP_Text ecoLevelText;
    // 25개 상태 중 현재 지구 상태 이름을 표시합니다.
    [SerializeField] private TMP_Text stateNameText;

    [Header("EarthState 파생 지표")]
    [SerializeField] private TMP_Text carbonPpmText;
    [SerializeField] private TMP_Text temperatureText;
    [SerializeField] private TMP_Text arcticIceText;
    [SerializeField] private TMP_Text seaLevelText;

    // 씬에 직접 연결하지 않았을 때도 자동으로 집계기와 상태 매니저를 찾아 연결합니다.
    private void Awake()
    {
        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();
        if (earthStateManager == null)
            earthStateManager = FindObjectOfType<EarthStateManager>();
    }

    // 두 이벤트 소스(TCP 집계기, 지구 상태 매니저)를 함께 구독합니다.
    private void OnEnable()
    {
        SubscribeAggregator();
        SubscribeEarthState();
        RefreshAllTexts();
    }

    // 오브젝트가 비활성화되거나 제거될 때 두 이벤트를 모두 정리합니다.
    private void OnDisable()
    {
        UnsubscribeAggregator();
        UnsubscribeEarthState();
    }

    // 인스펙터에서 참조가 바뀌었을 때도 최신 값으로 다시 갱신합니다.
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        RefreshAllTexts();
    }

    // 외부에서 집계기를 다시 지정할 수 있도록 공개 메서드를 제공합니다.
    public void SetAggregator(TcpDataAggregator targetAggregator)
    {
        if (aggregator == targetAggregator)
            return;

        UnsubscribeAggregator();
        aggregator = targetAggregator;
        SubscribeAggregator();
        RefreshAllTexts();
    }

    // TCP 집계기의 변경 이벤트를 구독합니다.
    private void SubscribeAggregator()
    {
        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();

        if (aggregator != null)
            aggregator.TotalsChanged += OnTotalsChanged;
    }

    // 중복 구독이나 파괴된 참조가 남지 않도록 이벤트를 해제합니다.
    private void UnsubscribeAggregator()
    {
        if (aggregator != null)
            aggregator.TotalsChanged -= OnTotalsChanged;
    }

    // 지구 상태 매니저의 StateChanged 이벤트를 구독합니다.
    private void SubscribeEarthState()
    {
        if (earthStateManager == null)
            earthStateManager = FindObjectOfType<EarthStateManager>();

        if (earthStateManager != null)
            earthStateManager.StateChanged += OnEarthStateChanged;
    }

    private void UnsubscribeEarthState()
    {
        if (earthStateManager != null)
            earthStateManager.StateChanged -= OnEarthStateChanged;
    }

    // TCP 10종과 EarthState 10종을 한 번에 최신 값으로 반영합니다.
    private void RefreshAllTexts()
    {
        RefreshTcpTexts();
        RefreshEarthStateTexts(earthStateManager != null ? earthStateManager.CurrentState : null);
    }

    // TCP 집계기의 누적 수치를 각 TMP_Text에 반영합니다.
    private void RefreshTcpTexts()
    {
        EnergyTotals totals = aggregator != null ? aggregator.GetEnergyTotals() : null;

        UpdateTargetText(thermalPowerText, "화력", totals != null ? totals.ThermalPower : 0);
        UpdateTargetText(hydroPowerText, "수력", totals != null ? totals.HydroPower : 0);
        UpdateTargetText(solarPowerText, "태양광", totals != null ? totals.SolarPower : 0);
        UpdateTargetText(windPowerText, "풍력", totals != null ? totals.WindPower : 0);
        UpdateTargetText(hydrogenText, "수소", totals != null ? totals.Hydrogen : 0);
        UpdateTargetText(electricEnergyText, "전기에너지", totals != null ? totals.ElectricEnergy : 0);
        UpdateTargetText(carbonText, "탄소", totals != null ? totals.Carbon : 0);
        UpdateTargetText(powerGenerationText, "발전", totals != null ? totals.PowerGeneration : 0);
        UpdateTargetText(cityEcoScoreText, "도시친환경도", totals != null ? totals.CityEcoScore : 0);
        UpdateTargetText(cityBuildingCountText, "도시 건물수", totals != null ? totals.CityBuildingCount : 0);
    }

    // EarthStateSnapshot의 필드들을 각 TMP_Text에 반영합니다.
    private void RefreshEarthStateTexts(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        // 지속가능성 단계는 BG 디자인의 "지속가능성 LEVEL"에 대응하는 값으로,
        // 현재는 친환경도 + 발전도 합을 사용합니다. 기획이 확정되면
        // EarthStateSnapshot에 전용 필드를 추가하고 여기만 교체하세요.
        int sustainability = snapshot.EcoLevel + snapshot.DevelopmentLevel;
        SetText(sustainabilityLevelText, string.Format(levelFormat, sustainability));
        SetText(developmentLevelText, string.Format(levelFormat, snapshot.DevelopmentLevel));
        SetText(ecoLevelText, string.Format(levelFormat, snapshot.EcoLevel));
        SetText(stateNameText, snapshot.StateName);

        SetText(carbonPpmText, string.Format(carbonPpmFormat, snapshot.CarbonPpm));
        SetText(temperatureText, string.Format(temperatureFormat, snapshot.TemperatureDeltaC));
        SetText(arcticIceText, string.Format(arcticIceFormat, snapshot.ArcticIcePercent));
        SetText(seaLevelText, string.Format(seaLevelFormat, snapshot.SeaLevelRiseMeters*100)); // 미터 단위를 센티미터로 변환
    }

    // 집계기에서 총합 변경 이벤트를 받으면 각 TMP_Text를 다시 갱신합니다.
    private void OnTotalsChanged(EnergyTotals totals)
    {
        RefreshTcpTexts();
    }

    // 지구 상태가 바뀔 때마다 EarthState Text만 갱신합니다.
    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        RefreshEarthStateTexts(snapshot);
    }

    // TMP_Text 하나를 지정한 데이터 키의 최신 합계로 갱신합니다.
    private void UpdateTargetText(TMP_Text targetText, string label, int value)
    {
        if (targetText == null)
            return;

        targetText.text = FormatValue(label, value);
    }

    // 라벨 포함 여부에 따라 TMP에 들어갈 최종 문자열 형식을 결정합니다.
    private string FormatValue(string label, int value)
    {
        if (showLabel)
            return string.Format(labeledValueFormat, label, value);

        return string.Format(valueOnlyFormat, value);
    }

    // null 가드가 포함된 단순 TMP_Text 대입 헬퍼입니다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;
        target.text = value;
    }
}
