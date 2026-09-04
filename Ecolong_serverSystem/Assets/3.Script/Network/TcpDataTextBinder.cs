using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class TcpDataTextBinder : MonoBehaviour
{
    [Header("TCP 연결")]
    [FormerlySerializedAs("aggregator")]
    [SerializeField] private TcpDataAggregator _aggregator;

    [Header("EarthState 연결")]
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;

    [Header("TCP 표시 형식")]
    [FormerlySerializedAs("showLabel")]
    [SerializeField] private bool _showLabel = false;
    [FormerlySerializedAs("valueOnlyFormat")]
    [SerializeField] private string _valueOnlyFormat = "{0}개";
    [FormerlySerializedAs("labeledValueFormat")]
    [SerializeField] private string _labeledValueFormat = "{0} : {1}";
    [Tooltip("좌측 상단 \"현재 탄소 / 현재 발전토큰\" 표시 형식입니다. 누적 수치 텍스트와 달리 단위(개)를 붙여 표시합니다. " +
             "리플레이 재생용 EarthStateLevelRecorder의 토큰 형식과 동일하게 맞춰 두세요.")]
    [SerializeField] private string _tokenCountFormat = "{0}개";

    [Header("EarthState 표시 형식")]
    [FormerlySerializedAs("levelFormat")]
    [SerializeField] private string _levelFormat = "LEVEL {0}";
    [FormerlySerializedAs("carbonPpmFormat")]
    [SerializeField] private string _carbonPpmFormat = "{0:0}ppm";
    [FormerlySerializedAs("temperatureFormat")]
    [SerializeField] private string _temperatureFormat = "{0:0.#}℃";
    [FormerlySerializedAs("arcticIceFormat")]
    [SerializeField] private string _arcticIceFormat = "{0:0}%";
    [FormerlySerializedAs("seaLevelFormat")]
    [SerializeField] private string _seaLevelFormat = "{0:0}cm";

    [Header("TCP 데이터별 TMP_Text 연결")]
    [FormerlySerializedAs("electricEnergyText")]
    [SerializeField] private TMP_Text _electricEnergyText;
    [FormerlySerializedAs("carbonText")]
    [SerializeField] private TMP_Text _carbonText;
    [FormerlySerializedAs("powerGenerationText")]
    [SerializeField] private TMP_Text _powerGenerationText;
    [FormerlySerializedAs("cityEcoScoreText")]
    [SerializeField] private TMP_Text _cityEcoScoreText;
    [FormerlySerializedAs("cityBuildingCountText")]
    [SerializeField] private TMP_Text _cityBuildingCountText;
    [FormerlySerializedAs("currentCarbonText")]
    [SerializeField] private TMP_Text _currentCarbonText;
    [FormerlySerializedAs("currentPowerGenerationText")]
    [SerializeField] private TMP_Text _currentPowerGenerationText;

    [Header("발전량(count × 효율) 표시")]
    [Tooltip("발전량 표시 형식. {0}=값, {1}=효율(%)")]
    [FormerlySerializedAs("productionFormat")]
    [SerializeField] private string _productionFormat = "{0}";
    [Range(0f, 100f)]
    [FormerlySerializedAs("thermalEfficiency")]
    [SerializeField] private float _thermalEfficiency = 40f;
    [Range(0f, 100f)]
    [FormerlySerializedAs("hydroEfficiency")]
    [SerializeField] private float _hydroEfficiency = 85f;
    [Range(0f, 100f)]
    [FormerlySerializedAs("solarEfficiency")]
    [SerializeField] private float _solarEfficiency = 20f;
    [Range(0f, 100f)]
    [FormerlySerializedAs("windEfficiency")]
    [SerializeField] private float _windEfficiency = 35f;
    [Range(0f, 100f)]
    [FormerlySerializedAs("hydrogenEfficiency")]
    [SerializeField] private float _hydrogenEfficiency = 60f;
    [FormerlySerializedAs("thermalPowerText")]
    [SerializeField] private TMP_Text _thermalPowerText;
    [FormerlySerializedAs("hydroPowerText")]
    [SerializeField] private TMP_Text _hydroPowerText;
    [FormerlySerializedAs("solarPowerText")]
    [SerializeField] private TMP_Text _solarPowerText;
    [FormerlySerializedAs("windPowerText")]
    [SerializeField] private TMP_Text _windPowerText;
    [FormerlySerializedAs("hydrogenText")]
    [SerializeField] private TMP_Text _hydrogenText;
    [Tooltip("화력+수력+태양광+풍력+수소의 (count × efficiency) 합계를 표시할 텍스트")]
    [SerializeField] private TMP_Text _totalEnergyProductionText;
    [Tooltip("누적 에너지 생산량 표시 형식. 기본은 000000 (6자리 0패딩, 500이면 000500)")]
    [SerializeField] private string _totalEnergyFormat = "{0:000000}";


    [Header("EarthState 단계/상태 표시")]
    [FormerlySerializedAs("sustainabilityLevelText")]
    [SerializeField] private TMP_Text _sustainabilityLevelText;
    [FormerlySerializedAs("developmentLevelText")]
    [SerializeField] private TMP_Text _developmentLevelText;
    [FormerlySerializedAs("ecoLevelText")]
    [SerializeField] private TMP_Text _ecoLevelText;
    [FormerlySerializedAs("stateNameText")]
    [SerializeField] private TMP_Text _stateNameText;

    [Header("EarthState 파생 지표")]
    [FormerlySerializedAs("carbonPpmText")]
    [SerializeField] private TMP_Text _carbonPpmText;
    [FormerlySerializedAs("temperatureText")]
    [SerializeField] private TMP_Text _temperatureText;
    [FormerlySerializedAs("arcticIceText")]
    [SerializeField] private TMP_Text _arcticIceText;
    [FormerlySerializedAs("seaLevelText")]
    [SerializeField] private TMP_Text _seaLevelText;

    [Header("슬롯 증가 연출")]
    [Tooltip("화력/수력/태양광/풍력/수소/전기/탄소/발전/친환경도/발전도 값이 증가할 때 목표값까지 슬롯처럼 점차 올라갑니다.")]
    [SerializeField] private bool _useSlotRolling = true;
    [Tooltip("증가 연출이 목표값에 도달하는 데 걸리는 시간(초). 0에 가까울수록 즉시 반영됩니다.")]
    [SerializeField] private float _slotRollDuration = 0.6f;

    // 슬롯 연출 대상별 현재 표시값/목표값을 추적합니다. 값 증가 시에만 점차 올라가고, 감소/초기화 시에는 즉시 반영합니다.
    private readonly List<SlotEntry> _slotEntries = new List<SlotEntry>();

    private class SlotEntry
    {
        public TMP_Text Text;
        public Func<float, string> Formatter;
        public float Displayed;
        public float Target;
        public float Speed;
        public bool Initialized;
    }

    private void Awake()
    {
        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
    }

    private void OnEnable()
    {
        SubscribeAggregator();
        SubscribeEarthState();
        RefreshAllTexts();
    }

    private void OnDisable()
    {
        UnsubscribeAggregator();
        UnsubscribeEarthState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        RefreshAllTexts();
    }

    // 슬롯 연출 대상들을 매 프레임 목표값 쪽으로 점차 이동시킵니다.
    private void Update()
    {
        if (!_useSlotRolling || !Application.isPlaying)
            return;

        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < _slotEntries.Count; i++)
        {
            SlotEntry entry = _slotEntries[i];
            if (!entry.Initialized || entry.Displayed >= entry.Target)
                continue;

            entry.Displayed = Mathf.MoveTowards(entry.Displayed, entry.Target, entry.Speed * deltaTime);

            if (entry.Text != null && entry.Formatter != null)
                entry.Text.text = entry.Formatter(entry.Displayed);
        }
    }

    // 슬롯 연출 대상의 목표값을 갱신합니다. 값이 증가하면 _slotRollDuration 동안 점차 올라가고,
    // 감소하거나 첫 적용일 때는 즉시 목표값으로 반영합니다.
    private void ApplySlotValue(TMP_Text targetText, float value, Func<float, string> formatter)
    {
        if (targetText == null)
            return;

        // 에디트 모드이거나 연출을 끈 경우 즉시 반영합니다.
        if (!_useSlotRolling || !Application.isPlaying)
        {
            targetText.text = formatter(value);
            return;
        }

        SlotEntry entry = GetOrCreateSlotEntry(targetText);
        entry.Formatter = formatter;
        entry.Target = value;

        if (!entry.Initialized || value <= entry.Displayed)
        {
            // 첫 적용/감소/동일값은 슬롯 연출 없이 즉시 반영합니다.
            entry.Displayed = value;
            entry.Initialized = true;
            entry.Speed = 0f;
            targetText.text = formatter(value);
            return;
        }

        // 증가: 남은 거리를 목표 시간으로 나눠 일정 속도로 올라가게 합니다.
        float remaining = entry.Target - entry.Displayed;
        entry.Speed = remaining / Mathf.Max(0.01f, _slotRollDuration);
    }

    private SlotEntry GetOrCreateSlotEntry(TMP_Text targetText)
    {
        for (int i = 0; i < _slotEntries.Count; i++)
        {
            if (_slotEntries[i].Text == targetText)
                return _slotEntries[i];
        }

        SlotEntry entry = new SlotEntry { Text = targetText };
        _slotEntries.Add(entry);
        return entry;
    }

    public void SetAggregator(TcpDataAggregator targetAggregator)
    {
        if (_aggregator == targetAggregator)
            return;

        UnsubscribeAggregator();
        _aggregator = targetAggregator;
        SubscribeAggregator();
        RefreshAllTexts();
    }

    private void SubscribeAggregator()
    {
        if (_aggregator == null)
            _aggregator = TcpDataAggregator.Instance;

        if (_aggregator != null)
            _aggregator.TotalsChanged += OnTotalsChanged;
    }

    private void UnsubscribeAggregator()
    {
        if (_aggregator != null)
            _aggregator.TotalsChanged -= OnTotalsChanged;
    }

    private void SubscribeEarthState()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;

        if (_earthStateManager != null)
            _earthStateManager.StateChanged += OnEarthStateChanged;
    }

    private void UnsubscribeEarthState()
    {
        if (_earthStateManager != null)
            _earthStateManager.StateChanged -= OnEarthStateChanged;
    }

    private void RefreshAllTexts()
    {
        RefreshTcpTexts();
        RefreshEarthStateTexts(_earthStateManager != null ? _earthStateManager.CurrentState : null);
    }

    private void RefreshTcpTexts()
    {
        EnergyTotals totals = _aggregator != null ? _aggregator.GetEnergyTotals() : null;
        UpdateProductionText(_thermalPowerText, totals != null ? totals.thermalPower : 0, _thermalEfficiency);
        UpdateProductionText(_hydroPowerText, totals != null ? totals.hydroPower : 0, _hydroEfficiency);
        UpdateProductionText(_solarPowerText, totals != null ? totals.solarPower : 0, _solarEfficiency);
        UpdateProductionText(_windPowerText, totals != null ? totals.windPower : 0, _windEfficiency);
        UpdateProductionText(_hydrogenText, totals != null ? totals.hydrogen : 0, _hydrogenEfficiency);

        // 누적 에너지 생산량 = 5종 발전의 (count × efficiency) 합계. 단위 표기는 옆 라벨이 담당.
        float totalProduction = 0f;
        if (totals != null)
        {
            totalProduction =
                totals.thermalPower * _thermalEfficiency +
                totals.hydroPower * _hydroEfficiency +
                totals.solarPower * _solarEfficiency +
                totals.windPower * _windEfficiency +
                totals.hydrogen * _hydrogenEfficiency;
        }
        if (_totalEnergyProductionText != null)
            ApplySlotValue(_totalEnergyProductionText, totalProduction, v => string.Format(_totalEnergyFormat, Mathf.Round(v)));

        UpdateTargetText(_electricEnergyText, "전기에너지", totals != null ? totals.electricCount : 0, true);
        UpdateTargetText(_carbonText, "탄소", totals != null ? totals.totalCarbon : 0, true);
        UpdateTargetText(_powerGenerationText, "발전", totals != null ? totals.powerGeneration : 0, true);
        UpdateTargetText(_cityEcoScoreText, "도시친환경도", totals != null ? totals.cityEcoScore : 0);
        UpdateTargetText(_cityBuildingCountText, "도시 건물수", totals != null ? totals.totalCityBuildingCount : 0);
    }

    private void UpdateProductionText(TMP_Text targetText, int count, float efficiency)
    {
        if (targetText == null)
            return;

        float production = count * efficiency;
        ApplySlotValue(targetText, production, v => string.Format(_productionFormat, Mathf.Round(v)));
    }

    private void RefreshEarthStateTexts(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        int sustainability = snapshot.EcoLevel + snapshot.DevelopmentLevel;
        SetText(_sustainabilityLevelText, string.Format(_levelFormat, sustainability));
        ApplySlotValue(_developmentLevelText, snapshot.DevelopmentLevel, v => string.Format(_levelFormat, Mathf.RoundToInt(v)));
        ApplySlotValue(_ecoLevelText, snapshot.EcoLevel, v => string.Format(_levelFormat, Mathf.RoundToInt(v)));
        SetText(_stateNameText, snapshot.StateName);

        SetText(_carbonPpmText, string.Format(_carbonPpmFormat, snapshot.CarbonPpm + 280f));
        SetText(_temperatureText, string.Format(_temperatureFormat, snapshot.TemperatureDeltaC));
        SetText(_arcticIceText, string.Format(_arcticIceFormat, snapshot.ArcticIcePercent));
        SetText(_seaLevelText, string.Format(_seaLevelFormat, snapshot.SeaLevelRiseMeters * 100)); // 미터 단위를 센티미터로 변환

        SetTokenCountText(_currentCarbonText, snapshot.CurrentCarbon);
        SetTokenCountText(_currentPowerGenerationText, snapshot.CurrentPowerGeneration);
    }

    private void OnTotalsChanged(EnergyTotals totals)
    {
        RefreshTcpTexts();
    }

    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        RefreshEarthStateTexts(snapshot);
    }

    private void UpdateTargetText(TMP_Text targetText, string label, int value, bool animate = false)
    {
        if (targetText == null)
            return;

        if (animate)
        {
            ApplySlotValue(targetText, value, v => FormatValue(label, Mathf.RoundToInt(v)));
            return;
        }

        targetText.text = FormatValue(label, value);
    }

    // 현재 탄소/발전토큰 개수는 누적 수치와 달리 "N개" 형태로 단위까지 함께 표시합니다.
    private void SetTokenCountText(TMP_Text targetText, int value)
    {
        if (targetText == null)
            return;

        targetText.text = string.Format(_tokenCountFormat, value);
    }

    private string FormatValue(string label, int value)
    {
        if (_showLabel)
            return string.Format(_labeledValueFormat, label, value);

        return string.Format(_valueOnlyFormat, value);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;
        target.text = value;
    }
}
