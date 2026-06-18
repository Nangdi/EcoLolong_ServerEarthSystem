using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GameSettingData
{
    public bool useUnityOnTop;
    // 게임 플레이 중 GameTimer/그래프 갱신 속도. 1이면 실시간, 60이면 1초당 1분이 흐릅니다.
    public float gameTimeScale = 1f;

    // ----- DualMonitorSpan (ESC 설정창에서 실시간 변경/적용) -----
    // 듀얼 모니터 스팬 창을 적용할지 여부.
    public bool dualMonitorSpan = true;
    // 테두리 없는(borderless) 창으로 강제할지 여부.
    public bool dualMonitorBorderless = true;
    // true면 아래 수동 해상도/원점을 사용, false면 가상 화면(모니터 합산)을 자동 인식.
    public bool dualMonitorManual = false;
    public int dualMonitorWidth = 3840;
    public int dualMonitorHeight = 1080;
    public int dualMonitorOriginX = 0;
    public int dualMonitorOriginY = 0;

    // ----- 지구상태 레벨 판정 기준 (ESC 설정창에서 변경/저장, 1→5단계 순으로 정렬) -----
    // 친환경도: [0]=1단계 경계 ... [3]=4단계 경계. 탄소가 [3] 미만이면 5단계, [0] 이상이면 1단계.
    public int[] ecoCarbonThresholds = { 80, 55, 35, 15 };
    // 발전도: [0]=2단계 경계 ... [3]=5단계 경계. 발전이 [3] 이상이면 5단계, [0] 미만이면 1단계.
    public int[] developmentThresholds = { 160, 220, 280, 340 };

    // ----- 도시친환경도(cityEcoScore) 기반 친환경도 보정 기준 -----
    // cityEcoScore가 [상한] 이상이면 친환경도 +1, [하한] 이하이면 -1, 그 사이면 0.
    public int cityEcoOffsetUpperThreshold = 20;
    public int cityEcoOffsetLowerThreshold = -20;

    // ----- 탄소농도(ppm) 변화 속도 가중치 -----
    // 1=기본(1배율), 1보다 작으면 더 천천히, 크면 더 빠르게 오르거나 내립니다. (0이면 변화 정지)
    public float carbonPpmSpeedMultiplier = 1f;
}

public class GameDynamicData
{
}

public class PortJson
{
    public string com = "COM4";
    public int baudLate = 19200;
    // 서버가 수신 대기할 TCP 포트입니다. TcpDataAggregator.Start에서 이 값을 사용해 listener를 띄웁니다.
    public int tcpPort = 5000;
    // 동시에 접속할 수 있는 TCP 클라이언트의 최대 수입니다.
    public int maxClientCount = 3;
    // 씬 시작 시 TCP 서버를 자동으로 시작할지 여부입니다.
    public bool autoStart = true;
}

public class VideoSettingJson
{
    public string folderPath = "";
    public float stabilityCheckIntervalSeconds = 0.3f;
    public int stabilityCheckCount = 2;
    public float maxWaitSeconds = 30f;
    public bool loop = false;
    public bool playAudio = true;
    public float volume = 1f;
}

public class JsonManager : MonoBehaviour
{
    public static JsonManager instance;
    public GameSettingData gameSettingData = new GameSettingData();
    public PortJson portJson = new PortJson();
    public GameDynamicData gameDynamicData = new GameDynamicData();
    public VideoSettingJson videoSettingJson = new VideoSettingJson();

    private string _gameDataPath;
    private string _gameDynamicDataPath;
    private string _portPath;
    private string _videoSettingPath;

    public string GameDataPath => _gameDataPath;
    public string VideoSettingPath => _videoSettingPath;

    // 싱글톤을 초기화하고 JSON 파일에서 런타임 설정 데이터를 불러옵니다.
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _portPath = Path.Combine(Application.streamingAssetsPath, "port.json");
        _gameDynamicDataPath = Path.Combine(Application.streamingAssetsPath, "Setting.json");
        _gameDataPath = Path.Combine(Application.persistentDataPath, "gameSettingData.json");
        _videoSettingPath = Path.Combine(Application.streamingAssetsPath, "VideoSetting.json");

        gameSettingData ??= new GameSettingData();
        gameDynamicData ??= new GameDynamicData();
        portJson ??= new PortJson();
        videoSettingJson ??= new VideoSettingJson();

        gameSettingData = LoadData(_gameDataPath, gameSettingData);
        gameDynamicData = LoadData(_gameDynamicDataPath, gameDynamicData);
        portJson = LoadData(_portPath, portJson);
        videoSettingJson = LoadData(_videoSettingPath, videoSettingJson);
    }

    // VideoSetting.json 파일을 현재 메모리 값으로 다시 기록합니다.
    public void SaveVideoSetting()
    {
        SaveData(videoSettingJson, _videoSettingPath);
    }

    // 현재 게임 설정 데이터를 gameSettingData.json 파일에 저장합니다.
    public void SaveGameSettingData()
    {
        SaveData(gameSettingData, _gameDataPath);
    }

    // 지정한 경로에 JSON 파일을 생성하거나 덮어씁니다.
    public static void SaveData<T>(T jsonObject, string path) where T : new()
    {
        if (jsonObject == null)
            jsonObject = new T();

        string directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        string json = JsonUtility.ToJson(jsonObject, true);
        File.WriteAllText(path, json);
        Debug.Log($"Saved JSON: {path}");
    }

    // JSON 파일을 읽고, 파일이 없으면 기본값으로 새 파일을 만듭니다.
    public static T LoadData<T>(string path, T data) where T : new()
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"JSON file does not exist. Creating a new file: {path}");
            SaveData(data, path);
        }

        Debug.Log($"Loaded JSON: {path}");
        string json = File.ReadAllText(path);
        T jsonData = JsonUtility.FromJson<T>(json);
        return jsonData ?? new T();
    }
}
