using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// 한 판이 끝날 때마다 플레이 데이터를 CSV로 저장합니다. (분석용)
// - 플레이 중(Playing) 일정 간격으로 지구상태/누적 데이터를 샘플링해 두었다가,
//   게임 시간이 끝나는 시점(TimeOut)에 두 가지 파일로 기록합니다.
//   1) <시작시각>.csv        : 한 판의 시계열 (샘플 1개 = 1줄)
//   2) GameSummary.csv       : 판별 최종값 요약 (한 판 = 1줄, 계속 누적)
// - 리플레이가 끝나는 시점(Ended)에는 기록하지 않습니다. 실제 플레이 데이터만 남깁니다.
//
// 씬에 직접 배치할 필요는 없습니다. 실행 시 자동 생성되며,
// 씬에 수동으로 배치해 두었다면 그 인스턴스가 우선 사용됩니다.
public class GameDataCsvLogger : MonoBehaviour
{
    // 판별 시계열 파일 이름에 쓰는 형식입니다. (게임을 시작한 시각)
    private const string FileTimeFormat = "yyyyMMdd_HHmmss";
    // 판별 최종값이 계속 쌓이는 누적 파일 이름입니다.
    private const string SummaryFileName = "GameSummary.csv";
    private const string FileExtension = ".csv";

    // 한 시점의 플레이 데이터입니다. 시계열 한 줄에 그대로 대응합니다.
    private struct Sample
    {
        public float GameTime;

        public int EcoLevel;
        public int DevelopmentLevel;
        public int Sustainability;
        public string StateName;

        public int CurrentCarbon;
        public int CurrentPowerGeneration;

        public float CarbonPpm;
        public float TemperatureDeltaC;
        public float ArcticIcePercent;
        public float SeaLevelRiseMeters;

        public int ThermalPower;
        public int HydroPower;
        public int SolarPower;
        public int WindPower;
        public int Hydrogen;
        public int ElectricCount;

        public int TotalCarbon;
        public int PowerGeneration;
        public int CaptureCarbon;
        public int CityEcoScore;
        public int TotalCityBuildingCount;
    }

    private static GameDataCsvLogger s_instance;

    private readonly List<Sample> _samples = new List<Sample>();
    private GameManager _gameManager;
    private bool _isSubscribed;

    // 한 판을 시작한 실제 시각입니다. 시계열 파일 이름과 요약 줄에 사용합니다.
    private DateTime _gameStartedAt;
    // 마지막으로 샘플을 남긴 게임 시간(초)입니다. 샘플 간격 판정에 씁니다.
    private float _lastSampledGameTime;

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

        GameManager manager = GameManager.Instance;
        if (manager == null || manager.CurrentGameState != GameState.Playing)
            return;

        GameTimer timer = GameTimer.Instance;
        if (timer == null)
            return;

        // 게임 시간 기준으로 일정 간격마다 샘플을 남깁니다.
        // (게임 시간 배율이 바뀌어도 분석 자료의 간격이 일정하게 유지됩니다)
        float interval = GetSampleInterval();
        if (_samples.Count > 0 && timer.CurrentTime - _lastSampledGameTime < interval)
            return;

        AddSample(timer.CurrentTime);
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

    // 새 판이 시작되면 이전 판의 샘플을 버리고 시작 시각을 기록합니다.
    private void OnGameStarted()
    {
        _samples.Clear();
        _gameStartedAt = DateTime.Now;
        _lastSampledGameTime = 0f;
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

        // 종료 시점의 최종값을 마지막 줄로 한 번 더 남깁니다.
        GameTimer timer = GameTimer.Instance;
        AddSample(timer != null ? timer.gameTime : _lastSampledGameTime);

        WriteCsvFiles();
    }

    // 현재 지구상태와 누적 데이터를 한 줄 분량으로 모아 담습니다.
    private void AddSample(float gameTime)
    {
        EarthStateManager stateManager = EarthStateManager.Instance;
        TcpDataAggregator aggregator = TcpDataAggregator.Instance;

        if (stateManager == null || stateManager.CurrentState == null)
            return;

        EarthStateSnapshot state = stateManager.CurrentState;
        EnergyTotals totals = aggregator != null ? aggregator.GetEnergyTotals() : null;

        _samples.Add(new Sample
        {
            GameTime = gameTime,

            EcoLevel = state.EcoLevel,
            DevelopmentLevel = state.DevelopmentLevel,
            // 지속가능성은 친환경도 + 발전도로 계산합니다. (EarthStateSliderBinder와 동일한 정의)
            Sustainability = state.EcoLevel + state.DevelopmentLevel,
            StateName = state.StateName,

            CurrentCarbon = state.CurrentCarbon,
            CurrentPowerGeneration = state.CurrentPowerGeneration,

            CarbonPpm = state.CarbonPpm,
            TemperatureDeltaC = state.TemperatureDeltaC,
            ArcticIcePercent = state.ArcticIcePercent,
            SeaLevelRiseMeters = state.SeaLevelRiseMeters,

            ThermalPower = totals != null ? totals.thermalPower : 0,
            HydroPower = totals != null ? totals.hydroPower : 0,
            SolarPower = totals != null ? totals.solarPower : 0,
            WindPower = totals != null ? totals.windPower : 0,
            Hydrogen = totals != null ? totals.hydrogen : 0,
            ElectricCount = totals != null ? totals.electricCount : 0,

            TotalCarbon = totals != null ? totals.totalCarbon : 0,
            PowerGeneration = totals != null ? totals.powerGeneration : 0,
            CaptureCarbon = totals != null ? totals.captureCarbon : 0,
            CityEcoScore = totals != null ? totals.cityEcoScore : 0,
            TotalCityBuildingCount = totals != null ? totals.totalCityBuildingCount : 0,
        });

        _lastSampledGameTime = gameTime;
    }

    // 시계열 파일과 요약 누적 파일을 함께 기록합니다.
    private void WriteCsvFiles()
    {
        if (_samples.Count == 0)
        {
            Debug.LogWarning("[DataCsv] 저장할 샘플이 없어 CSV를 건너뜁니다.");
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

        string timelineName = _gameStartedAt.ToString(FileTimeFormat, CultureInfo.InvariantCulture) + FileExtension;
        string timelinePath = Path.Combine(folder, timelineName);

        if (!TryWriteTimeline(timelinePath))
            return;

        AppendSummary(Path.Combine(folder, SummaryFileName), timelineName);

        Debug.Log($"[DataCsv] 플레이 데이터 저장 완료. 샘플 {_samples.Count}줄 → {timelinePath}");
    }

    // 한 판의 시계열을 새 파일로 기록합니다.
    private bool TryWriteTimeline(string path)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine(string.Join(",",
            "게임시간(초)", "친환경도단계", "발전도단계", "지속가능성", "지구상태",
            "현재탄소토큰", "현재발전토큰",
            "탄소농도(ppm)", "온도변화(C)", "북극얼음(%)", "해수면상승(m)",
            "화력", "수력", "태양광", "풍력", "수소", "전기합계",
            "탄소토큰누적", "발전토큰누적", "탄소포집", "도시친환경점수", "도시건물수"));

        for (int i = 0; i < _samples.Count; i++)
            builder.AppendLine(BuildSampleLine(_samples[i]));

        try
        {
            // Excel이 한글 헤더를 깨뜨리지 않도록 BOM이 있는 UTF-8로 기록합니다.
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DataCsv] 시계열 CSV 저장 실패: {path} / {e.Message}");
            return false;
        }
    }

    // 판별 최종값 한 줄을 누적 파일에 덧붙입니다. 파일이 없으면 헤더부터 만듭니다.
    private void AppendSummary(string path, string timelineFileName)
    {
        Sample last = _samples[_samples.Count - 1];

        StringBuilder builder = new StringBuilder();

        bool isNewFile = !File.Exists(path);
        if (isNewFile)
        {
            builder.AppendLine(string.Join(",",
                "시작시각", "종료시각", "플레이시간(초)",
                "친환경도단계", "발전도단계", "지속가능성", "지구상태",
                "현재탄소토큰", "현재발전토큰",
                "탄소농도(ppm)", "온도변화(C)", "북극얼음(%)", "해수면상승(m)",
                "화력", "수력", "태양광", "풍력", "수소", "전기합계",
                "탄소토큰누적", "발전토큰누적", "탄소포집", "도시친환경점수", "도시건물수",
                "샘플수", "시계열파일"));
        }

        builder.Append(Escape(_gameStartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',');
        builder.Append(Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',');
        builder.Append(Number(last.GameTime, 1)).Append(',');
        builder.Append(BuildSampleValues(last)).Append(',');
        builder.Append(_samples.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
        builder.Append(Escape(timelineFileName));

        try
        {
            // 새 파일일 때만 BOM을 붙입니다. (이어쓸 때 BOM이 중간에 끼면 Excel이 줄을 깨뜨립니다)
            File.AppendAllText(path, builder.ToString() + Environment.NewLine, new UTF8Encoding(isNewFile));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DataCsv] 요약 CSV 저장 실패: {path} / {e.Message}");
        }
    }

    // 시계열 한 줄: 게임시간 + 나머지 값들
    private static string BuildSampleLine(Sample sample)
    {
        return Number(sample.GameTime, 1) + "," + BuildSampleValues(sample);
    }

    // 시계열과 요약이 같은 열 순서를 쓰도록 값 부분만 따로 만듭니다. (게임시간 제외)
    private static string BuildSampleValues(Sample sample)
    {
        return string.Join(",",
            sample.EcoLevel.ToString(CultureInfo.InvariantCulture),
            sample.DevelopmentLevel.ToString(CultureInfo.InvariantCulture),
            sample.Sustainability.ToString(CultureInfo.InvariantCulture),
            Escape(sample.StateName),
            sample.CurrentCarbon.ToString(CultureInfo.InvariantCulture),
            sample.CurrentPowerGeneration.ToString(CultureInfo.InvariantCulture),
            Number(sample.CarbonPpm, 2),
            Number(sample.TemperatureDeltaC, 3),
            Number(sample.ArcticIcePercent, 2),
            Number(sample.SeaLevelRiseMeters, 3),
            sample.ThermalPower.ToString(CultureInfo.InvariantCulture),
            sample.HydroPower.ToString(CultureInfo.InvariantCulture),
            sample.SolarPower.ToString(CultureInfo.InvariantCulture),
            sample.WindPower.ToString(CultureInfo.InvariantCulture),
            sample.Hydrogen.ToString(CultureInfo.InvariantCulture),
            sample.ElectricCount.ToString(CultureInfo.InvariantCulture),
            sample.TotalCarbon.ToString(CultureInfo.InvariantCulture),
            sample.PowerGeneration.ToString(CultureInfo.InvariantCulture),
            sample.CaptureCarbon.ToString(CultureInfo.InvariantCulture),
            sample.CityEcoScore.ToString(CultureInfo.InvariantCulture),
            sample.TotalCityBuildingCount.ToString(CultureInfo.InvariantCulture));
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

    // 샘플 간격(게임 시간 초)입니다. 0 이하가 들어오면 1초로 둡니다.
    private static float GetSampleInterval()
    {
        GameSettingData settings = GetSettings();
        if (settings == null || settings.dataCsvSampleIntervalSeconds <= 0f)
            return 1f;

        return settings.dataCsvSampleIntervalSeconds;
    }

    private static GameSettingData GetSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        return jsonManager != null ? jsonManager.gameSettingData : null;
    }
}
