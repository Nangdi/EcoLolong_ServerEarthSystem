using UnityEngine;
using UnityEngine.Serialization;

public class EarthStateGraphBinder : MonoBehaviour
{
    [Header("상태 연결")]
    [SerializeField] private EarthStateManager earthStateManager;

    [Header("그래프 연결")]
    [SerializeField] private ResourceGraphs carbonPpmGraph;
    [SerializeField] private ResourceGraphs temperatureGraph;
    [FormerlySerializedAs("CarbornGraph"), FormerlySerializedAs("CarbonGraph")]
    [SerializeField] private ResourceGraphs carbonGraph;
    [FormerlySerializedAs("ElectricityGraph")]
    [SerializeField] private ResourceGraphs electricityGraph;
    [FormerlySerializedAs("PowerGenerationGraph")]
    [SerializeField] private ResourceGraphs powerGenerationGraph;

    private int lastRecordedSecond = -1;

    // 인스펙터 연결이 비어 있어도 런타임에서 자동으로 상태 매니저를 찾습니다.
    private void Awake()
    {
        if (earthStateManager == null)
            earthStateManager = EarthStateManager.Instance;
    }

    // 지구 상태가 바뀔 때마다 탄소농도와 온도를 그래프에 기록하도록 연결합니다.
    private void OnEnable()
    {
        if (earthStateManager == null)
            earthStateManager = EarthStateManager.Instance;

        if (earthStateManager != null)
            earthStateManager.StateChanged += OnEarthStateChanged;

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStart += OnGameStart;
    }

    // 오브젝트가 비활성화될 때 이벤트 연결을 정리합니다.
    private void OnDisable()
    {
        if (earthStateManager != null)
            earthStateManager.StateChanged -= OnEarthStateChanged;

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
        if (currentSecond == lastRecordedSecond)
            return;

        lastRecordedSecond = currentSecond;
        if (carbonPpmGraph != null)
            carbonPpmGraph.AddPoint(snapshot.CarbonPpm);

        if (temperatureGraph != null)
            temperatureGraph.AddPoint(snapshot.TemperatureDeltaC);
        if (carbonGraph != null)
            carbonGraph.AddPoint(snapshot.CarbonCount);
        if (electricityGraph != null)
            electricityGraph.AddPoint(snapshot.ElectricCount);
        if (powerGenerationGraph != null)
            powerGenerationGraph.AddPoint(snapshot.PowerGenerationCount);
    }

    // 새 게임이 시작되면 그래프 기록 초 카운트를 다시 초기화합니다.
    private void OnGameStart()
    {
        lastRecordedSecond = -1;
    }
}
