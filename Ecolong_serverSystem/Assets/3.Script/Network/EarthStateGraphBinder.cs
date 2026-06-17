using UnityEngine;
using UnityEngine.Serialization;

public class EarthStateGraphBinder : MonoBehaviour
{
    [Header("상태 연결")]
    [FormerlySerializedAs("earthStateManager")]
    [SerializeField] private EarthStateManager _earthStateManager;

    [Header("그래프 연결")]
    [FormerlySerializedAs("carbonPpmGraph")]
    [SerializeField] private ResourceGraphs _carbonPpmGraph;
    [FormerlySerializedAs("temperatureGraph")]
    [SerializeField] private ResourceGraphs _temperatureGraph;
    [FormerlySerializedAs("CarbornGraph"), FormerlySerializedAs("CarbonGraph"), FormerlySerializedAs("carbonGraph")]
    [SerializeField] private ResourceGraphs _carbonGraph;
    [FormerlySerializedAs("ElectricityGraph"), FormerlySerializedAs("electricityGraph")]
    [SerializeField] private ResourceGraphs _electricityGraph;
    [FormerlySerializedAs("PowerGenerationGraph"), FormerlySerializedAs("powerGenerationGraph")]
    [SerializeField] private ResourceGraphs _powerGenerationGraph;

    private int _lastRecordedSecond = -1;

    // 인스펙터 연결이 비어 있어도 런타임에서 자동으로 상태 매니저를 찾습니다.
    private void Awake()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;
    }

    // 지구 상태가 바뀔 때마다 탄소농도와 온도를 그래프에 기록하도록 연결합니다.
    private void OnEnable()
    {
        if (_earthStateManager == null)
            _earthStateManager = EarthStateManager.Instance;

        if (_earthStateManager != null)
            _earthStateManager.StateChanged += OnEarthStateChanged;

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStart += OnGameStart;
    }

    // 오브젝트가 비활성화될 때 이벤트 연결을 정리합니다.
    private void OnDisable()
    {
        if (_earthStateManager != null)
            _earthStateManager.StateChanged -= OnEarthStateChanged;

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStart -= OnGameStart;
    }

    // 상태 변경 시 탄소농도와 온도를 각 ResourceGraphs에 추가합니다.
    private void OnEarthStateChanged(EarthStateSnapshot snapshot)
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameState.Playing)
            return;

        if (GameTimer.Instance == null)
            return;

        int currentSecond = Mathf.FloorToInt(GameTimer.Instance.CurrentTime);
        if (currentSecond == _lastRecordedSecond)
            return;

        _lastRecordedSecond = currentSecond;
        if (_carbonPpmGraph != null)
            _carbonPpmGraph.AddPoint(snapshot.CarbonPpm);

        if (_temperatureGraph != null)
            _temperatureGraph.AddPoint(snapshot.TemperatureDeltaC);
        if (_carbonGraph != null)
            _carbonGraph.AddPoint(snapshot.CurrentCarbon);
        if (_electricityGraph != null)
            _electricityGraph.AddPoint(snapshot.ElectricCount);
        if (_powerGenerationGraph != null)
            _powerGenerationGraph.AddPoint(snapshot.PowerGenerationCount);
    }

    // 새 게임이 시작되면 그래프 기록 초 카운트를 다시 초기화합니다.
    private void OnGameStart()
    {
        _lastRecordedSecond = -1;
    }
}
