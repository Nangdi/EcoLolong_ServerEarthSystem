using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GameSettingData
{
    // 창을 항상 다른 창 위에 띄울지 여부.
    [SettingField("항상 위에 표시", SettingGroup.Developer)]
    public bool useUnityOnTop;
    // 게임 플레이 중 GameTimer/그래프 갱신 속도. 1이면 실시간, 60이면 1초당 1분이 흐릅니다.
    [SettingField("게임 시간 배율", SettingGroup.Developer, "1 = 실시간, 60 = 1초에 1분 (테스트용 가속)")]
    public float gameTimeScale = 1f;
    // 한 판의 게임 총시간(초). 기본 900초(15분). 타이머의 타임아웃 기준으로 사용됩니다.
    [SettingField("게임 총시간(초)", SettingGroup.Admin, "한 판의 길이. 900 = 15분")]
    public float gameTotalTime = 900f;
    // 리플레이 재생 배율. 15면 실제 플레이 대비 15배 빠르게 리플레이가 진행됩니다.
    [SettingField("리플레이 재생 배율", SettingGroup.Admin, "15 = 실제 플레이보다 15배 빠르게 재생")]
    public float replayTimerSpeed = 15f;
    // VIDEO_UPLOAD(영상 업로드 완료) 수신 후 Scene2 캔버스를 띄우기까지 기다리는 시간(초). 0이면 즉시 전환합니다.
    [SettingField("Scene2 전환 대기(초)", SettingGroup.Admin, "영상 업로드 완료 후 이 시간만큼 대기한 뒤 Scene2로 전환. 0 = 즉시")]
    public float scene2TransitionDelay = 3f;

    // ----- 리플레이 화면 녹화 (ReplayScreenRecorder) -----
    // 리플레이가 시작되면 화면 전체를 mp4로 녹화하고, 리플레이가 끝나면 저장합니다.
    // 파일 이름은 녹화 시작 시각의 "yyyyMMdd_HHmmss.mp4" 형식입니다.
    [SettingField("리플레이 화면 녹화", SettingGroup.Admin, "true = 리플레이 구간을 화면 전체 녹화")]
    public bool recordReplay = true;
    // 녹화본 저장 폴더. 비워 두면 실행 파일 옆의 ReplayRecordings 폴더를 사용합니다.
    [SettingField("녹화본 저장 폴더", SettingGroup.Admin, "녹화된 mp4가 쌓이는 폴더. 비우면 실행 파일 옆 ReplayRecordings 폴더")]
    public string recordFolderPath = "";
    // 보관 기간(일). 프로그램이 켜질 때 이 기간이 지난 녹화본을 삭제합니다. 0 이하면 자동 삭제를 하지 않습니다.
    [SettingField("녹화 보관 기간(일)", SettingGroup.Admin, "프로그램 시작 시 이 기간이 지난 영상을 삭제. 0 = 삭제 안 함")]
    public int recordRetentionDays = 30;
    // 녹화 프레임레이트(FPS).
    [SettingField("녹화 프레임레이트(FPS)", SettingGroup.Developer)]
    public int recordFrameRate = 30;
    // ffmpeg.exe 경로. 비워 두면 StreamingAssets/ffmpeg/ffmpeg.exe → 시스템 PATH 순으로 찾습니다.
    [SettingField("ffmpeg.exe 프로그램 위치", SettingGroup.Developer, "저장 폴더가 아니라 녹화 도구(ffmpeg.exe) 파일 경로입니다. 비우면 StreamingAssets/ffmpeg/ffmpeg.exe → 시스템 PATH 순으로 자동 탐색")]
    public string recordFFmpegPath = "";
    // 시스템 소리까지 녹음하려면 dshow 오디오 장치 이름을 입력합니다. 비워 두면 영상만 녹화합니다.
    [SettingField("녹화 오디오 장치", SettingGroup.Developer, "dshow 장치 이름(예: 스테레오 믹스). 비우면 무음 녹화")]
    public string recordAudioDevice = "";

    // ----- 플레이 데이터 CSV 저장 (GameDataCsvLogger) -----
    // 한 판이 끝날 때마다 그 판의 최종 지구상태/누적 데이터를 GameSummary.csv에 한 줄씩 남깁니다.
    [SettingField("플레이 데이터 CSV 저장", SettingGroup.Admin, "true = 한 판이 끝날 때마다 최종 데이터를 한 줄 기록")]
    public bool dataCsvEnabled = true;
    // CSV 저장 폴더. 비워 두면 C:\kolon\Data를 사용합니다.
    [SettingField("CSV 저장 폴더", SettingGroup.Admin, "GameSummary.csv가 쌓이는 폴더. 비우면 C:\\kolon\\Data")]
    public string dataCsvFolderPath = @"C:\kolon\Data";

    // ----- 시작 / 리플레이 / 종료 키 (ESC 설정창의 키 설정 버튼에서 재지정) -----
    // UnityEngine.KeyCode 이름을 그대로 저장합니다. (예: "S", "R", "E", "F7", "Alpha1", "Space")
    [SettingField("시작 키", SettingGroup.Admin, "아래 \"키 설정\" 버튼으로 직접 눌러 지정할 수 있습니다")]
    public string startKey = "S";
    [SettingField("리플레이 키", SettingGroup.Admin)]
    public string replayKey = "R";
    [SettingField("종료(복귀) 키", SettingGroup.Admin)]
    public string endKey = "E";

    // ----- DualMonitorSpan (ESC 설정창에서 실시간 변경/적용) -----
    // 듀얼 모니터 스팬 창을 적용할지 여부.
    [SettingField("듀얼모니터 스팬 사용", SettingGroup.Developer)]
    public bool dualMonitorSpan = true;
    // 테두리 없는(borderless) 창으로 강제할지 여부.
    [SettingField("테두리 없는 창", SettingGroup.Developer)]
    public bool dualMonitorBorderless = true;
    // true면 아래 수동 해상도/원점을 사용, false면 가상 화면(모니터 합산)을 자동 인식.
    [SettingField("수동 해상도 사용", SettingGroup.Developer, "false = 모니터 합산 해상도를 자동 인식")]
    public bool dualMonitorManual = false;
    [SettingField("듀얼모니터 가로(px)", SettingGroup.Developer)]
    public int dualMonitorWidth = 3840;
    [SettingField("듀얼모니터 세로(px)", SettingGroup.Developer)]
    public int dualMonitorHeight = 1080;
    [SettingField("듀얼모니터 원점 X", SettingGroup.Developer)]
    public int dualMonitorOriginX = 0;
    [SettingField("듀얼모니터 원점 Y", SettingGroup.Developer)]
    public int dualMonitorOriginY = 0;

    // ----- 지구상태 레벨 판정 기준 (ESC 설정창에서 변경/저장, 1→5단계 순으로 정렬) -----
    // 친환경도: [0]=1단계 경계 ... [3]=4단계 경계. 탄소가 [3] 미만이면 5단계, [0] 이상이면 1단계.
    [SettingField("친환경도 기준(탄소)", SettingGroup.Developer, "콤마로 구분한 4개 경계값 (1→4단계 순)")]
    public int[] ecoCarbonThresholds = { 80, 55, 35, 15 };
    // 발전도: [0]=2단계 경계 ... [3]=5단계 경계. 발전이 [3] 이상이면 5단계, [0] 미만이면 1단계.
    [SettingField("발전도 기준", SettingGroup.Developer, "콤마로 구분한 4개 경계값 (2→5단계 순)")]
    public int[] developmentThresholds = { 160, 220, 280, 340 };

    // ----- 도시친환경도(cityEcoScore) 기반 친환경도 보정 기준 -----
    // cityEcoScore가 [상한] 이상이면 친환경도 +1, [하한] 이하이면 -1, 그 사이면 0.
    [SettingField("도시친환경 보정 상한", SettingGroup.Developer, "이 값 이상이면 친환경도 +1")]
    public int cityEcoOffsetUpperThreshold = 20;
    [SettingField("도시친환경 보정 하한", SettingGroup.Developer, "이 값 이하이면 친환경도 -1")]
    public int cityEcoOffsetLowerThreshold = -20;

    // ----- 탄소농도(ppm) 변화 속도 가중치 -----
    // 1=기본(1배율), 1보다 작으면 더 천천히, 크면 더 빠르게 오르거나 내립니다. (0이면 변화 정지)
    [SettingField("탄소농도 변화 배율", SettingGroup.Developer, "1 = 기본. 작을수록 천천히, 0이면 변화 정지")]
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
