using System;
using TMPro;
using UnityEngine;

public class TcpDataTextBinder : MonoBehaviour
{
    [Header("TCP 연결")]
    [SerializeField] private TcpDataAggregator aggregator;

    [Header("표시 형식")]
    [SerializeField] private bool showLabel = false;
    [SerializeField] private string valueOnlyFormat = "{0}";
    [SerializeField] private string labeledValueFormat = "{0} : {1}";

    [Header("데이터별 TMP_Text 연결")]
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

    // 씬에 직접 연결하지 않았을 때도 자동으로 TCP 집계기를 찾아 연결합니다.
    private void Awake()
    {
        if (aggregator == null)
            aggregator = FindObjectOfType<TcpDataAggregator>();
    }

    // TCP 집계기의 값이 바뀔 때마다 각 TMP_Text를 갱신하도록 이벤트를 연결합니다.
    private void OnEnable()
    {
        SubscribeAggregator();
        RefreshAllTexts();
    }

    // 오브젝트가 비활성화되거나 제거될 때 이벤트를 정리합니다.
    private void OnDisable()
    {
        UnsubscribeAggregator();
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

    // 지원하는 10개 데이터의 현재 합계를 각 TMP_Text에 한 번에 반영합니다.
    private void RefreshAllTexts()
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

    // 집계기에서 총합 변경 이벤트를 받으면 각 TMP_Text를 다시 갱신합니다.
    private void OnTotalsChanged(EnergyTotals totals)
    {
        RefreshAllTexts();
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
}
