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
            _totalEnergyProductionText.text = string.Format(_totalEnergyFormat, totalProduction);

        UpdateTargetText(_electricEnergyText, "전기에너지", totals != null ? totals.electricCount : 0);
        UpdateTargetText(_carbonText, "탄소", totals != null ? totals.totalCarbon : 0);
        UpdateTargetText(_powerGenerationText, "발전", totals != null ? totals.powerGeneration : 0);
        UpdateTargetText(_cityEcoScoreText, "도시친환경도", totals != null ? totals.cityEcoScore : 0);
        UpdateTargetText(_cityBuildingCountText, "도시 건물수", totals != null ? totals.totalCityBuildingCount : 0);
    }

    private void UpdateProductionText(TMP_Text targetText, int count, float efficiency)
    {
        if (targetText == null)
            return;

        float production = count * efficiency;
        targetText.text = string.Format(_productionFormat, production);
    }

    private void RefreshEarthStateTexts(EarthStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        int sustainability = snapshot.EcoLevel + snapshot.DevelopmentLevel;
        SetText(_sustainabilityLevelText, string.Format(_levelFormat, sustainability));
        SetText(_developmentLevelText, string.Format(_levelFormat, snapshot.DevelopmentLevel));
        SetText(_ecoLevelText, string.Format(_levelFormat, snapshot.EcoLevel));
        SetText(_stateNameText, snapshot.StateName);

        SetText(_carbonPpmText, string.Format(_carbonPpmFormat, snapshot.CarbonPpm + 280f));
        SetText(_temperatureText, string.Format(_temperatureFormat, snapshot.TemperatureDeltaC));
        SetText(_arcticIceText, string.Format(_arcticIceFormat, snapshot.ArcticIcePercent));
        SetText(_seaLevelText, string.Format(_seaLevelFormat, snapshot.SeaLevelRiseMeters * 100)); // 미터 단위를 센티미터로 변환

        UpdateTargetText(_currentCarbonText, "현재탄소", snapshot.CurrentCarbon);
        UpdateTargetText(_currentPowerGenerationText, "현재발전", snapshot.CurrentPowerGeneration);
    }

    private void OnTotalsChanged(EnergyTotals totals)
    {
        RefreshTcpTexts();
    }

    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        RefreshEarthStateTexts(snapshot);
    }

    private void UpdateTargetText(TMP_Text targetText, string label, int value)
    {
        if (targetText == null)
            return;

        targetText.text = FormatValue(label, value);
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
