using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// 한 판이 끝날 때마다 그 판의 최종 플레이 데이터를 CSV 한 줄로 남깁니다. (분석용)
// - 저장 파일은 GameSummary.csv 하나이며, 한 판이 끝날 때마다 줄이 계속 쌓입니다.
// - 게임 시간이 끝나는 시점(TimeOut)에만 기록합니다. 리플레이 종료(Ended)에는 기록하지 않습니다.
//
// 씬에 직접 배치할 필요는 없습니다. 실행 시 자동 생성되며,
// 씬에 수동으로 배치해 두었다면 그 인스턴스가 우선 사용됩니다.
public class GameDataCsvLogger : MonoBehaviour
{
    // 판별 최종값이 계속 쌓이는 누적 파일 이름입니다.
    private const string SummaryFileName = "GameSummary.csv";
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static GameDataCsvLogger s_instance;

    private GameManager _gameManager;
    private bool _isSubscribed;

    // 한 판을 시작한 실제 시각입니다. 요약 줄의 시작시각 열에 사용합니다.
    private DateTime _gameStartedAt;

    // 씬에 배치하지 않아도 동작하도록 씬 로드 직후 자동으로 인스턴스를 만듭니다.
    // JsonManager가 Awake에서 설정을 이미 읽어 둔 뒤이므로 이 시점에 설정을 참조할 수 있습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null)
            return;

        if (FindObjectOfType<GameDataCsvLogger>() != null)
            return;

        GameObject host = new GameObject(nameof(GameDataCsvLogger));
        host.AddComponent<GameDataCsvLogger>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
    }

    private void Update()
    {
        // GameManager가 늦게 초기화되는 케이스를 위해 구독을 반복 시도합니다.
        TrySubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (s_instance == this)
            s_instance = null;
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        _gameManager = GameManager.Instance;
        if (_gameManager == null)
            return;

        _gameManager.OnGameStart += OnGameStarted;
        _gameManager.OnGameEnd += OnGameEnded;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _gameManager == null)
            return;

        _gameManager.OnGameStart -= OnGameStarted;
        _gameManager.OnGameEnd -= OnGameEnded;
        _isSubscribed = false;
    }

    // 새 판이 시작된 실제 시각을 기록해 둡니다.
    private void OnGameStarted()
    {
        _gameStartedAt = DateTime.Now;
    }

    // 게임 시간이 끝나는 시점(TimeOut)에 호출됩니다.
    // GameManager는 이 이벤트를 발행한 뒤에 데이터를 초기화하므로, 여기서 읽는 값이 그 판의 최종값입니다.
    private void OnGameEnded()
    {
        GameManager manager = GameManager.Instance;

        // 리플레이가 끝난 시점(Ended)에는 저장하지 않습니다. 실제 플레이 종료만 기록합니다.
        if (manager == null || manager.CurrentGameState != GameState.TimeOut)
            return;

        GameSettingData settings = GetSettings();
        if (settings != null && !settings.dataCsvEnabled)
        {
            Debug.Log("[DataCsv] dataCsvEnabled=false 이므로 CSV를 저장하지 않습니다.");
            return;
        }

        AppendSummaryRow();
    }

    // 이번 판의 최종값을 GameSummary.csv에 한 줄 덧붙입니다. 파일이 없으면 헤더부터 만듭니다.
    private void AppendSummaryRow()
    {
        EarthStateManager stateManager = EarthStateManager.Instance;
        if (stateManager == null || stateManager.CurrentState == null)
        {
            Debug.LogWarning("[DataCsv] 지구상태를 읽을 수 없어 CSV 저장을 건너뜁니다.");
            return;
        }

        string folder = ResolveDataFolder();

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DataCsv] 저장 폴더를 만들지 못했습니다: {folder} / {e.Message}");
            return;
        }

        EarthStateSnapshot state = stateManager.CurrentState;
        TcpDataAggregator aggregator = TcpDataAggregator.Instance;
        EnergyTotals totals = aggregator != null ? aggregator.GetEnergyTotals() : new EnergyTotals();

        // 타임아웃 시점에는 GameTimer가 CurrentTime을 0으로 되돌린 뒤라, 총 게임시간을 플레이 시간으로 씁니다.
        GameTimer timer = GameTimer.Instance;
        float playSeconds = timer != null ? timer.gameTime : 0f;

        string path = Path.Combine(folder, SummaryFileName);
        bool isNewFile = !File.Exists(path);

        StringBuilder builder = new StringBuilder();

        if (isNewFile)
        {
            builder.AppendLine(string.Join(",",
                "시작시각", "종료시각", "플레이시간(초)",
                "친환경도단계", "발전도단계", "지속가능성", "지구상태",
                "현재탄소토큰", "현재발전토큰",
                "탄소농도(ppm)", "온도변화(C)", "북극얼음(%)", "해수면상승(m)",
                "화력", "수력", "태양광", "풍력", "수소", "전기합계",
                "탄소토큰누적", "발전토큰누적", "탄소포집", "도시친환경점수", "도시건물수"));
        }

        builder.AppendLine(string.Join(",",
            Escape(_gameStartedAt.ToString(TimeFormat, CultureInfo.InvariantCulture)),
            Escape(DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture)),
            Number(playSeconds, 1),

            state.EcoLevel.ToString(CultureInfo.InvariantCulture),
            state.DevelopmentLevel.ToString(CultureInfo.InvariantCulture),
            // 지속가능성은 친환경도 + 발전도로 계산합니다. (EarthStateSliderBinder와 동일한 정의)
            (state.EcoLevel + state.DevelopmentLevel).ToString(CultureInfo.InvariantCulture),
            Escape(state.StateName),

            state.CurrentCarbon.ToString(CultureInfo.InvariantCulture),
            state.CurrentPowerGeneration.ToString(CultureInfo.InvariantCulture),

            Number(state.CarbonPpm, 2),
            Number(state.TemperatureDeltaC, 3),
            Number(state.ArcticIcePercent, 2),
            Number(state.SeaLevelRiseMeters, 3),

            totals.thermalPower.ToString(CultureInfo.InvariantCulture),
            totals.hydroPower.ToString(CultureInfo.InvariantCulture),
            totals.solarPower.ToString(CultureInfo.InvariantCulture),
            totals.windPower.ToString(CultureInfo.InvariantCulture),
            totals.hydrogen.ToString(CultureInfo.InvariantCulture),
            totals.electricCount.ToString(CultureInfo.InvariantCulture),

            totals.totalCarbon.ToString(CultureInfo.InvariantCulture),
            totals.powerGeneration.ToString(CultureInfo.InvariantCulture),
            totals.captureCarbon.ToString(CultureInfo.InvariantCulture),
            totals.cityEcoScore.ToString(CultureInfo.InvariantCulture),
            totals.totalCityBuildingCount.ToString(CultureInfo.InvariantCulture)));

        try
        {
            // Excel이 한글 헤더를 깨뜨리지 않도록 새 파일일 때만 BOM을 붙입니다.
            // (이어쓸 때 BOM이 줄 중간에 끼면 그 줄이 깨집니다)
            File.AppendAllText(path, builder.ToString(), new UTF8Encoding(isNewFile));
            Debug.Log($"[DataCsv] 플레이 데이터 저장 완료 → {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DataCsv] CSV 저장 실패: {path} / {e.Message}");
        }
    }

    // 소수 자릿수를 고정해 기록합니다. 지역 설정과 무관하게 항상 점(.)을 소수점으로 씁니다.
    private static string Number(float value, int decimals)
    {
        return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    // 쉼표나 따옴표가 들어간 값이 열을 밀지 않도록 CSV 규칙대로 감쌉니다.
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.IndexOf(',') < 0 && value.IndexOf('"') < 0 && value.IndexOf('\n') < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // CSV를 저장할 폴더입니다. 설정이 비어 있으면 C:\kolon\Data를 사용합니다.
    public static string ResolveDataFolder()
    {
        GameSettingData settings = GetSettings();
        if (settings != null && !string.IsNullOrEmpty(settings.dataCsvFolderPath))
            return settings.dataCsvFolderPath;

        return @"C:\kolon\Data";
    }

    private static GameSettingData GetSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        return jsonManager != null ? jsonManager.gameSettingData : null;
    }
}
